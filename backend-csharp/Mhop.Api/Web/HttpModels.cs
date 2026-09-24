using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Mhop.Data;
using Mhop.Security;
using Dapper;

namespace Mhop.Web;

/// <summary>naive 本地时间序列化为 Python isoformat 风格：2026-09-24T20:47:55.123456（不带 Z）。</summary>
public sealed class NaiveDateTimeConverter : JsonConverter<DateTime>
{
    internal const string Format = "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF";

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        DateTime.Parse(reader.GetString() ?? "");

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString(Format, System.Globalization.CultureInfo.InvariantCulture));
}

public sealed class NaiveNullableDateTimeConverter : JsonConverter<DateTime?>
{
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        return DateTime.Parse(reader.GetString() ?? "");
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteStringValue(value.Value.ToString(NaiveDateTimeConverter.Format,
            System.Globalization.CultureInfo.InvariantCulture));
    }
}

public sealed record ApiError([property: JsonPropertyName("detail")] string Detail);

public static class HttpResults
{
    public static IResult Error(int status, string detail) =>
        Results.Json(new ApiError(detail), statusCode: status);
}

/// <summary>从 Authorization 头解析当前用户（对应 deps.py 三个依赖）。</summary>
public sealed class CurrentUserProvider
{
    private readonly Database _db;
    private readonly JwtTokens _jwt;

    public CurrentUserProvider(Database db, JwtTokens jwt)
    {
        _db = db;
        _jwt = jwt;
    }

    public User? Optional(HttpContext ctx)
    {
        var header = ctx.Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(header) || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return null;
        var parsed = _jwt.ValidateToken(header["Bearer ".Length..].Trim());
        if (parsed is null) return null;
        using var conn = _db.Open();
        var user = conn.QueryFirstOrDefault<User>("SELECT * FROM users WHERE id = @Id",
            new { Id = parsed.Value.UserId });
        if (user is null || user.Status != "active") return null;
        return user;
    }

    public User? Required(HttpContext ctx) => Optional(ctx);

    public User? Admin(HttpContext ctx) => Optional(ctx) is { Role: "admin" } u ? u : null;
}
