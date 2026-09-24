using System.Text.Encodings.Web;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Http;
using Mhop.Data;
using Mhop.Services;
using Mhop.Web;

namespace Mhop.Endpoints;

public static class AssessmentEndpoints
{
    public static void MapAssessment(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/assessments");
        g.MapGet("/scales", GetScales);
        g.MapPost("", Submit);
        g.MapGet("/mine", Mine);
    }

    private static IResult GetScales()
    {
        // 与 FastAPI 输出对齐：snake_case 键名，bands 为数组（Python tuple → JSON array）
        var items = Scales.All.Values.Select(s => new
        {
            key = s.Key,
            name = s.Name,
            intro = s.Intro,
            max = s.Max,
            options = s.Options,
            questions = s.Questions,
            bands = s.Bands.Select(b => new object[] { b.Ceiling, b.Label, b.Code }),
            crisis_index = s.CrisisIndex,
        });
        return Results.Json(items);
    }

    private static async Task<IResult> Submit(
        AssessmentIn body, HttpContext ctx, Database db,
        CurrentUserProvider users, AiService ai)
    {
        int? score = null;
        var level = "";
        var levelCode = "";
        var crisis = Moderation.DetectCrisis(body.FreeText);

        if (Scales.All.TryGetValue(body.AssessmentType, out var scale))
        {
            var n = scale.Questions.Count;
            var values = new List<int>(n);
            for (var i = 0; i < n; i++)
            {
                if (!body.Answers.TryGetValue(i.ToString(), out var v))
                    return HttpResults.Error(400, "请完成量表全部题目");
                values.Add(v);
            }
            if (values.Any(v => v is < 0 or > 3))
                return HttpResults.Error(400, "量表作答值非法");
            score = values.Sum();
            (level, levelCode) = Scales.ScoreBand(body.AssessmentType, score.Value);
            var ci = scale.CrisisIndex;
            if (ci is not null && values[ci.Value] > 0)
                crisis = true;
        }
        else if (body.AssessmentType != "free")
            return HttpResults.Error(400, "未知的评估类型");
        else if (body.FreeText.Trim().Length == 0)
            return HttpResults.Error(400, "请先描述你最近的状态与感受");

        var (result, _) = await ai.AssessAsync(
            body.AssessmentType, score, level, body.FreeText.Trim(), crisis);

        var saved = false;
        int? recordId = null;
        DateTime? createdAt = null;
        if (body.SaveToCloud)
        {
            var current = users.Required(ctx);
            if (current is null)
                return HttpResults.Error(401, "登录后才能保存到云端");

            var relaxedJson = new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            };
            var input = JsonSerializer.Serialize(
                new { answers = body.Answers, free_text = body.FreeText }, relaxedJson);
            var now = DateTime.Now;
            using var conn = db.Open();
            var id = conn.ExecuteScalar<long>(
                "INSERT INTO assessments (user_id, assessment_type, input_data, ai_result, score, level, created_at) " +
                "VALUES (@Uid, @Type, @Input, @Result, @Score, @Level, @Now); SELECT last_insert_rowid();",
                new
                {
                    Uid = current.Id,
                    Type = body.AssessmentType,
                    Input = input,
                    Result = result,
                    Score = score,
                    Level = level,
                    Now = now,
                });
            recordId = (int)id;
            createdAt = now;
            saved = true;
        }

        return Results.Json(new AssessmentOut
        {
            Id = recordId,
            AssessmentType = body.AssessmentType,
            Score = score,
            Level = level,
            LevelCode = levelCode,
            Crisis = crisis,
            AiResult = result,
            SavedCloud = saved,
            CreatedAt = createdAt,
        });
    }

    private static IResult Mine(HttpContext ctx, Database db, CurrentUserProvider users)
    {
        var current = users.Required(ctx);
        if (current is null) return HttpResults.Error(401, "请先登录");
        using var conn = db.Open();
        var rows = conn.Query<Assessment>(
            "SELECT * FROM assessments WHERE user_id = @Uid ORDER BY created_at DESC",
            new { Uid = current.Id }).ToList();
        var out_ = rows.Select(r => new AssessmentOut
        {
            Id = r.Id,
            AssessmentType = r.AssessmentType,
            Score = r.Score,
            Level = r.Level ?? "",
            LevelCode = "",
            Crisis = false,
            AiResult = r.AiResult,
            SavedCloud = true,
            CreatedAt = r.CreatedAt,
        });
        return Results.Json(out_);
    }
}
