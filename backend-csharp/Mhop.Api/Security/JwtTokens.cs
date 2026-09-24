using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Mhop.Configuration;

namespace Mhop.Security;

/// <summary>
/// HS256 JWT，载荷 sub/role/exp/iat 与 PyJWT 生成的令牌结构一致，
/// 同一 JWT_SECRET 下 Python/C# 两版签发的令牌可互相校验。
/// </summary>
public sealed class JwtTokens
{
    private readonly byte[] _key;
    private readonly JwtSecurityTokenHandler _handler;
    private readonly int _expireHours;

    public JwtTokens(AppSettings settings)
    {
        _key = Encoding.UTF8.GetBytes(settings.JwtSecret);
        _expireHours = settings.JwtExpireHours;
        _handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
    }

    public string Create(int userId, string role)
    {
        var creds = new SigningCredentials(new SymmetricSecurityKey(_key), SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new("sub", userId.ToString()),
            new("role", role),
        };
        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddHours(_expireHours),
            signingCredentials: creds);
        return _handler.WriteToken(token);
    }

    /// <summary>校验令牌；失败返回 null（与 Python decode_token 行为一致）。
    /// 注意：方法名不能叫 TryParse —— 否则最小 API 会把 JwtTokens 当成可绑定参数类型。</summary>
    public (int UserId, string Role)? ValidateToken(string token)
    {
        try
        {
            var parameters = new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(_key),
                ClockSkew = TimeSpan.FromSeconds(30),
            };
            var principal = _handler.ValidateToken(token, parameters, out _);
            var sub = principal.FindFirst("sub")?.Value;
            var role = principal.FindFirst("role")?.Value;
            if (sub is null || role is null || !int.TryParse(sub, out var userId)) return null;
            return (userId, role);
        }
        catch
        {
            return null;
        }
    }
}
