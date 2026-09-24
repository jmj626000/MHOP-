using Microsoft.AspNetCore.Http;
using Mhop.Services;
using Mhop.Web;

namespace Mhop.Endpoints;

public static class UploadEndpoints
{
    public static void MapUpload(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/upload");
        g.MapPost("/avatar", UploadAvatar);
        g.MapPost("/image", UploadImage);
    }

    private static async Task<IResult> Handle(IFormFile file, bool square, ImageService images)
    {
        await using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var data = ms.ToArray();

        if (data.Length > ImageService.MaxSize)
            return HttpResults.Error(400, "图片大小不能超过 5MB");
        if (!ImageService.AllowedTypes.Contains(file.ContentType))
            return HttpResults.Error(400, "仅支持 JPG/PNG/WebP/GIF 格式");
        try
        {
            var url = images.Save(data, square ? "avatars" : "posts", square);
            return Results.Json(new { url });
        }
        catch (BadImageException)
        {
            return HttpResults.Error(400, "不支持的图片格式");
        }
    }

    private static Task<IResult> UploadAvatar(HttpContext ctx, IFormFile? file,
        CurrentUserProvider users, ImageService images)
    {
        var current = users.Required(ctx);
        if (current is null) return Task.FromResult(HttpResults.Error(401, "请先登录"));
        if (file is null) return Task.FromResult(HttpResults.Error(400, "请选择要上传的图片"));
        return Handle(file, true, images);
    }

    private static Task<IResult> UploadImage(HttpContext ctx, IFormFile? file,
        CurrentUserProvider users, ImageService images)
    {
        var current = users.Required(ctx);
        if (current is null) return Task.FromResult(HttpResults.Error(401, "请先登录"));
        if (file is null) return Task.FromResult(HttpResults.Error(400, "请选择要上传的图片"));
        return Handle(file, false, images);
    }
}
