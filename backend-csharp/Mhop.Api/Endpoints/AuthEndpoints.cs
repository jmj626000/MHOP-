using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.AspNetCore.Http;
using Mhop.Data;
using Mhop.Security;
using Mhop.Services;
using Mhop.Web;

namespace Mhop.Endpoints;

public static class AuthEndpoints
{
    private static readonly Regex PhoneRe = new(@"^1[3-9]\d{9}$", RegexOptions.Compiled);
    private static readonly Regex UsernameEmailRe = new(@"[^a-zA-Z0-9_一-龥]", RegexOptions.Compiled);

    public static void MapAuth(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/auth");
        g.MapPost("/register", Register);
        g.MapPost("/login", Login);
        g.MapPost("/email-code", SendEmailCode);
        g.MapPost("/login-email", LoginByEmail);
        g.MapGet("/me", Me);
        g.MapPut("/profile", UpdateProfile);
        g.MapPut("/me/phone", BindPhone);
        g.MapGet("/users/{userId:int}", GetUserPublic);
    }

    private static TokenOut Token(JwtTokens jwt, User user, bool newAccount = false) =>
        new()
        {
            AccessToken = jwt.Create(user.Id, user.Role),
            NewAccount = newAccount,
            User = UserOut.From(user),
        };

    private static IResult Register(
        RegisterIn body, Database db, JwtTokens jwt)
    {
        var username = body.Username.Trim();
        if (username.Length is < 2 or > 32)
            return HttpResults.Error(422, "用户名长度需为 2-32 个字符");
        if (body.Password.Length is < 6 or > 64)
            return HttpResults.Error(422, "密码长度需为 6-64 个字符");

        using var conn = db.Open();
        if (conn.QueryFirstOrDefault<User>(
                "SELECT * FROM users WHERE username = @U", new { U = username }) is not null)
            return HttpResults.Error(400, "用户名已存在");

        var now = DateTime.Now;
        var id = conn.ExecuteScalar<long>(
            "INSERT INTO users (username, password_hash, role, status, avatar, badge, created_at) " +
            "VALUES (@U, @P, 'user', 'active', '', '', @Now); SELECT last_insert_rowid();",
            new { U = username, P = PasswordHasher.Hash(body.Password), Now = now });
        var user = conn.QueryFirst<User>("SELECT * FROM users WHERE id = @Id", new { Id = (int)id });
        return Results.Json(Token(jwt, user));
    }

    private static IResult Login(LoginIn body, Database db, JwtTokens jwt)
    {
        using var conn = db.Open();
        var user = conn.QueryFirstOrDefault<User>(
            "SELECT * FROM users WHERE username = @U", new { U = body.Username.Trim() });
        if (user is null || !PasswordHasher.Verify(body.Password, user.PasswordHash))
            return HttpResults.Error(401, "用户名或密码错误");
        if (user.Status != "active")
            return HttpResults.Error(403, "账号已被停用");
        return Results.Json(Token(jwt, user));
    }

    private static IResult SendEmailCode(
        EmailCodeIn body, HttpContext ctx, EmailCodeService email)
    {
        var addr = body.Email.Trim().ToLowerInvariant();
        if (!EmailCodeService.ValidEmail(addr))
            return HttpResults.Error(400, "邮箱格式不正确");

        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        if (!email.CheckIpRate(ip))
            return HttpResults.Error(429, "请求过于频繁，请稍后再试");

        var issued = email.Issue(addr);
        if (issued.Code is null)
            return HttpResults.Error(429, $"发送太频繁，请 {issued.RetryAfter} 秒后重试");

        bool delivered;
        try
        {
            delivered = email.Deliver(addr, issued.Code);
        }
        catch
        {
            return HttpResults.Error(502, "验证码邮件发送失败，请稍后重试或联系管理员");
        }

        var resp = new Dictionary<string, object>
        {
            ["sent"] = true,
            ["ttl"] = EmailCodeService.CodeTtl,
            ["resend_after"] = EmailCodeService.ResendInterval,
        };
        if (!delivered)
        {
            // 开发模式：把验证码直接返回，方便本机联调
            resp["dev_mode"] = true;
            resp["dev_code"] = issued.Code;
        }
        return Results.Json(resp);
    }

    private static IResult LoginByEmail(
        EmailLoginIn body, Database db, JwtTokens jwt, EmailCodeService email)
    {
        var addr = body.Email.Trim().ToLowerInvariant();
        if (!EmailCodeService.ValidEmail(addr))
            return HttpResults.Error(400, "邮箱格式不正确");
        if (!email.Verify(addr, body.Code))
            return HttpResults.Error(400, "验证码错误或已过期");

        using var conn = db.Open();
        var user = conn.QueryFirstOrDefault<User>("SELECT * FROM users WHERE email = @E", new { E = addr });
        var isNew = false;
        if (user is null)
        {
            // 邮箱首次登录自动注册：用户名取 @ 前段，过滤非法字符，拼 4 位随机后缀
            var local = addr.Split('@')[0];
            var baseName = UsernameEmailRe.Replace(local, "");
            if (baseName.Length == 0) baseName = "user";
            if (baseName.Length > 24) baseName = baseName[..24];

            string username = "";
            var ok = false;
            for (var i = 0; i < 10; i++)
            {
                username = $"{baseName}_{Random.Shared.Next(10000):D4}";
                if (conn.QueryFirstOrDefault<int?>(
                        "SELECT id FROM users WHERE username = @U", new { U = username }) is null)
                {
                    ok = true;
                    break;
                }
            }
            if (!ok)
                return HttpResults.Error(500, "注册失败，请稍后重试");

            var id = conn.ExecuteScalar<long>(
                "INSERT INTO users (username, password_hash, email, role, status, avatar, badge, created_at) " +
                "VALUES (@U, @P, @E, 'user', 'active', '', '', @Now); SELECT last_insert_rowid();",
                new
                {
                    U = username,
                    P = PasswordHasher.Hash(RandomUrlSafeToken(24)),
                    E = addr,
                    Now = DateTime.Now,
                });
            user = conn.QueryFirst<User>("SELECT * FROM users WHERE id = @Id", new { Id = (int)id });
            isNew = true;
        }
        if (user.Status != "active")
            return HttpResults.Error(403, "账号已被停用");
        return Results.Json(Token(jwt, user, isNew));
    }

    /// <summary>对应 Python secrets.token_urlsafe(n)：URL 安全随机密码（不可登录，仅占位）。</summary>
    private static string RandomUrlSafeToken(int byteLength)
    {
        var bytes = RandomNumberGenerator.GetBytes(byteLength);
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private static IResult Me(HttpContext ctx, Database db, CurrentUserProvider users)
    {
        var user = users.Required(ctx);
        if (user is null) return HttpResults.Error(401, "请先登录");
        return Results.Json(UserOut.From(user));
    }

    private static IResult UpdateProfile(
        ProfileUpdateIn body, HttpContext ctx, Database db, CurrentUserProvider users)
    {
        var current = users.Required(ctx);
        if (current is null) return HttpResults.Error(401, "请先登录");

        var username = body.Username.Trim();
        if (username.Length is < 2 or > 32)
            return HttpResults.Error(400, "用户名至少 2 个字符");

        using var conn = db.Open();
        if (conn.QueryFirstOrDefault<User>(
                "SELECT * FROM users WHERE username = @U AND id != @Id",
                new { U = username, current.Id }) is not null)
            return HttpResults.Error(400, "用户名已被占用");

        var avatar = body.Avatar ?? "";
        if (avatar.Length > 255) avatar = avatar[..255];
        conn.Execute("UPDATE users SET username = @U, avatar = @A WHERE id = @Id",
            new { U = username, A = avatar, current.Id });
        var updated = conn.QueryFirst<User>("SELECT * FROM users WHERE id = @Id", new { current.Id });
        return Results.Json(UserOut.From(updated));
    }

    private static IResult BindPhone(
        PhoneBindIn body, HttpContext ctx, Database db, CurrentUserProvider users)
    {
        var current = users.Required(ctx);
        if (current is null) return HttpResults.Error(401, "请先登录");

        var phone = body.Phone.Trim();
        if (!PhoneRe.IsMatch(phone))
            return HttpResults.Error(422, "手机号格式不正确");

        using var conn = db.Open();
        if (conn.QueryFirstOrDefault<User>(
                "SELECT * FROM users WHERE phone = @P AND id != @Id",
                new { P = phone, current.Id }) is not null)
            return HttpResults.Error(400, "该手机号已被其他账号绑定");

        conn.Execute("UPDATE users SET phone = @P WHERE id = @Id", new { P = phone, current.Id });
        var updated = conn.QueryFirst<User>("SELECT * FROM users WHERE id = @Id", new { current.Id });
        return Results.Json(UserOut.From(updated));
    }

    private static IResult GetUserPublic(int userId, Database db)
    {
        using var conn = db.Open();
        var user = conn.QueryFirstOrDefault<User>("SELECT * FROM users WHERE id = @Id", new { Id = userId });
        if (user is null) return HttpResults.Error(404, "用户不存在");
        var out_ = UserOut.From(user);
        out_.Phone = null; // 手机号仅本人与后台可见
        return Results.Json(out_);
    }
}
