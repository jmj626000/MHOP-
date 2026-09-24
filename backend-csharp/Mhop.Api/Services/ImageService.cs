using Mhop.Configuration;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace Mhop.Services;

/// <summary>
/// 图片上传处理（对应 Python upload.py，Pillow → ImageSharp）：
/// 头像居中正方形裁剪 200×200；帖子图等比缩到宽 800；统一输出 quality=82 的 webp。
/// </summary>
public sealed class ImageService
{
    public static readonly HashSet<string> AllowedTypes =
        new(StringComparer.OrdinalIgnoreCase)
        { "image/jpeg", "image/png", "image/webp", "image/gif" };

    public const int MaxSize = 5 * 1024 * 1024; // 5MB
    private const int ThumbMaxWidth = 800;
    private const int AvatarSize = 200;

    private readonly string _root;

    public ImageService(AppSettings settings)
    {
        _root = settings.UploadDirAbs;
        Directory.CreateDirectory(Path.Combine(_root, "avatars"));
        Directory.CreateDirectory(Path.Combine(_root, "posts"));
    }

    /// <summary>返回 URL 路径 /uploads/{subDir}/xxx.webp；格式不支持时抛 ImageFormatException。</summary>
    public string Save(byte[] data, string subDir, bool square)
    {
        try
        {
            using var image = Image.Load(data);
            image.Mutate(ctx =>
            {
                if (square)
                {
                    var side = Math.Min(image.Width, image.Height);
                    var x = (image.Width - side) / 2;
                    var y = (image.Height - side) / 2;
                    ctx.Crop(new Rectangle(x, y, side, side));
                    ctx.Resize(AvatarSize, AvatarSize);
                }
                else if (image.Width > ThumbMaxWidth)
                {
                    var ratio = (double)ThumbMaxWidth / image.Width;
                    ctx.Resize(ThumbMaxWidth, Math.Max(1, (int)(image.Height * ratio)));
                }
            });

            var filename = Guid.NewGuid().ToString("N") + ".webp";
            var filepath = Path.Combine(_root, subDir, filename);
            image.Save(filepath, new WebpEncoder { Quality = 82 });
            return $"/uploads/{subDir}/{filename}";
        }
        catch (UnknownImageFormatException)
        {
            throw new BadImageException();
        }
        catch (InvalidImageContentException)
        {
            throw new BadImageException();
        }
    }
}

public sealed class BadImageException : Exception;
