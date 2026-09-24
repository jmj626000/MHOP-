using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using MailKit.Net.Smtp;
using Mhop.Configuration;
using MimeKit;

namespace Mhop.Services;

public sealed record IssueResult(string? Code, int RetryAfter);

/// <summary>
/// 邮箱登录验证码（对应 Python email_code.py）：
/// 6 位数字、10 分钟有效、一次性、最多错 5 次；同邮箱 60s 间隔、同 IP 每小时 20 次。
/// 未配置 SMTP 时为开发模式：验证码写日志并返回给调用方。
/// </summary>
public sealed class EmailCodeService
{
    public const int CodeTtl = 600;
    public const int ResendInterval = 60;
    public const int MaxAttempts = 5;
    public const int IpHourlyLimit = 20;

    private static readonly Regex EmailRe =
        new(@"^[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}$", RegexOptions.Compiled);

    private sealed record Entry(string Code, double ExpireAt, int Attempts);

    private readonly object _gate = new();
    private readonly ConcurrentDictionary<string, Entry> _codes = new();
    private readonly ConcurrentDictionary<string, double> _lastSent = new();
    private readonly ConcurrentDictionary<string, List<double>> _ipWindow = new();
    private readonly AppSettings _settings;
    private readonly ILogger<EmailCodeService> _logger;

    public EmailCodeService(AppSettings settings, ILogger<EmailCodeService> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public static bool ValidEmail(string? email) =>
        !string.IsNullOrEmpty(email) && email.Length <= 254 && EmailRe.IsMatch(email);

    public bool CheckIpRate(string ip)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        lock (_gate)
        {
            var arr = _ipWindow.GetOrAdd(ip, []).Where(t => now - t < 3600).ToList();
            var ok = arr.Count < IpHourlyLimit;
            if (ok) arr.Add(now);
            _ipWindow[ip] = arr;
            return ok;
        }
    }

    public IssueResult Issue(string email)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        lock (_gate)
        {
            var wait = ResendInterval - (now - _lastSent.GetValueOrDefault(email, 0));
            if (wait > 0) return new IssueResult(null, (int)wait + 1);
            var code = Random.Shared.Next(0, 1_000_000).ToString("D6");
            _codes[email] = new Entry(code, now + CodeTtl, 0);
            _lastSent[email] = now;
            return new IssueResult(code, 0);
        }
    }

    public bool Verify(string email, string? code)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        lock (_gate)
        {
            if (!_codes.TryGetValue(email, out var item)) return false;
            if (now > item.ExpireAt || item.Attempts >= MaxAttempts)
            {
                _codes.TryRemove(email, out _);
                return false;
            }
            if (!FixedTimeEquals(item.Code, (code ?? "").Trim()))
            {
                _codes[email] = item with { Attempts = item.Attempts + 1 };
                return false;
            }
            _codes.TryRemove(email, out _);
            return true;
        }
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        var ba = System.Text.Encoding.UTF8.GetBytes(a);
        var bb = System.Text.Encoding.UTF8.GetBytes(b);
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(ba, bb);
    }

    public bool SmtpConfigured() => !string.IsNullOrEmpty(_settings.SmtpHost);

    /// <summary>发送成功（或开发模式跳过）返回 false 表示当前为开发模式。</summary>
    public bool Deliver(string to, string code)
    {
        if (!SmtpConfigured())
        {
            _logger.LogWarning("[开发模式] 邮箱验证码登录：{Email} 的验证码为 {Code}（配置 SMTP 后将真正发邮件）", to, code);
            return false;
        }
        SendEmail(to, code);
        _logger.LogInformation("登录验证码已发送至 {Email}", to);
        return true;
    }

    private void SendEmail(string to, string code)
    {
        var app = _settings.AppName;
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(_settings.SmtpFrom.Length > 0 ? _settings.SmtpFrom : _settings.SmtpUser));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = $"【{app}】登录验证码 {code}";

        var text =
            $"你正在登录{app}。\n\n" +
            $"你的登录验证码是：{code}\n" +
            "验证码 10 分钟内有效，请勿泄露给他人。如非本人操作，请忽略本邮件。\n\n" +
            $"—— {app}";
        var html =
            $"<div style=\"max-width:480px;margin:0 auto;font-family:'Microsoft YaHei',Arial,sans-serif\">" +
            $"<h2 style=\"color:#2f8f83;margin-bottom:8px\">{app}</h2>" +
            "<p style=\"color:#555\">你正在登录，本次验证码为：</p>" +
            $"<div style=\"margin:20px 0;font-size:34px;font-weight:700;letter-spacing:8px;color:#2f8f83\">{code}</div>" +
            "<p style=\"color:#888;font-size:13px\">验证码 10 分钟内有效，请勿泄露给他人。如非本人操作，请忽略本邮件。</p>" +
            "<hr style=\"border:none;border-top:1px solid #eee;margin:24px 0\">" +
            "<p style=\"color:#aaa;font-size:12px\">本邮件由系统自动发送，请勿回复。如遇紧急心理困扰，请拨打全国心理援助热线 12356。</p>" +
            "</div>";

        message.Body = new BodyBuilder { TextBody = text, HtmlBody = html }.ToMessageBody();

        using var client = new SmtpClient();
        if (_settings.SmtpUseSsl)
            client.Connect(_settings.SmtpHost, _settings.SmtpPort, MailKit.Security.SecureSocketOptions.SslOnConnect);
        else
        {
            client.Connect(_settings.SmtpHost, _settings.SmtpPort, MailKit.Security.SecureSocketOptions.StartTls);
        }
        if (!string.IsNullOrEmpty(_settings.SmtpUser))
            client.Authenticate(_settings.SmtpUser, _settings.SmtpPassword);
        client.Send(message);
        client.Disconnect(true);
    }
}
