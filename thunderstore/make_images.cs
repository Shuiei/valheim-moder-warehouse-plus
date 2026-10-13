// Draws the package's pictures (a .NET file-based app; from the repository's root:
// dotnet run --file thunderstore/make_images.cs):
//   thunderstore/icon.png         256 × 256: three stacked wooden chests with a sign, and a gold plus
//   thunderstore/nexus-banner.png 1280 × 720: the Nexus Mods page's main picture, the icon with the
//                                 mod's name and what it does (DejaVu Sans, as Linux has it)
#:package SkiaSharp@4.153.1
#:package SkiaSharp.NativeAssets.Linux@4.153.1
#:package SkiaSharp.NativeAssets.Win32@4.153.1
#:package SkiaSharp.NativeAssets.macOS@4.153.1
#:property UsesGameDlls=false
using SkiaSharp;

var green = new SKColor(28, 36, 30);
var frame = new SKColor(78, 96, 72);
var gold = new SKColor(214, 176, 72);

using var icon = Icon();
Save(icon, "thunderstore/icon.png");
using var banner = Banner(icon);
Save(banner, "thunderstore/nexus-banner.png");
return 0;

SKImage Icon()
{
    using var surface = SKSurface.Create(new SKImageInfo(256, 256, SKColorType.Rgba8888, SKAlphaType.Premul));
    var c = surface.Canvas;
    c.Clear(SKColors.Transparent);
    RoundRect(c, 8, 8, 248, 248, 40, green, frame, 4);
    Chest(c, 40, 150, 82, 60);
    Chest(c, 126, 150, 82, 60);
    Chest(c, 83, 86, 82, 60);
    // A sign on a post, left of the stack.
    Rect(c, 48, 92, 54, 146, new SKColor(96, 64, 34));
    RoundRect(c, 26, 60, 78, 96, 4, new SKColor(196, 160, 104), new SKColor(96, 64, 34), 3);
    foreach (float y in new[] { 72f, 84f })
    {
        Line(c, 34, y, 70, y, new SKColor(80, 56, 32), 3);
    }

    // The plus.
    using (var fill = Paint(gold))
    using (var edge = Stroke(new SKColor(120, 92, 30), 4))
    {
        c.DrawOval(new SKRect(168, 28, 236, 96), fill);
        c.DrawOval(new SKRect(170, 30, 234, 94), edge);
    }

    Rect(c, 197, 40, 207, 84, new SKColor(40, 30, 14));
    Rect(c, 180, 57, 224, 67, new SKColor(40, 30, 14));
    return surface.Snapshot();
}

// A wooden chest: body, lid line, iron bands and a lock.
void Chest(SKCanvas c, float x, float y, float w, float h)
{
    var wood = new SKColor(140, 92, 48);
    var dark = new SKColor(72, 44, 22);
    RoundRect(c, x, y, x + w, y + h, 6, wood, dark, 4);
    Line(c, x, y + (h * 0.38f), x + w, y + (h * 0.38f), dark, 4);
    foreach (float band in new[] { x + (w * 0.2f), x + (w * 0.8f) })
    {
        Rect(c, band - 4, y, band + 4, y + h, new SKColor(96, 100, 108));
    }

    float cx = x + (w / 2);
    using var lockPaint = Paint(gold);
    c.DrawRoundRect(new SKRect(cx - 8, y + (h * 0.3f), cx + 8, y + (h * 0.3f) + 18), 3, 3, lockPaint);
}

SKImage Banner(SKImage iconImage)
{
    using var surface = SKSurface.Create(new SKImageInfo(1280, 720, SKColorType.Rgba8888, SKAlphaType.Premul));
    var c = surface.Canvas;
    c.Clear(green);
    using (var edge = Stroke(frame, 6))
    {
        c.DrawRoundRect(new SKRect(27, 27, 1253, 693), 48, 48, edge);
    }

    using (var paint = new SKPaint { IsAntialias = true })
    {
        c.DrawImage(iconImage, new SKRect(70, 190, 410, 530), new SKSamplingOptions(SKCubicResampler.Mitchell), paint);
    }

    using var bold = Typeface("DejaVu Sans", SKFontStyle.Bold, "/usr/share/fonts/dejavu-sans-fonts/DejaVuSans-Bold.ttf");
    using var regular = Typeface("DejaVu Sans", SKFontStyle.Normal, "/usr/share/fonts/dejavu-sans-fonts/DejaVuSans.ttf");
    const float X = 450;
    Text(c, "Moder Warehouse", X, 160, bold, 66, new SKColor(240, 232, 214));
    Text(c, "Plus", X, 236, bold, 66, gold);
    string[] lines =
    [
        "Sorts your deposits into the right chests",
        "Withdraw, craft and * wildcard from a sign",
        "Stock and statistics boards",
        "Server-only: players need no mod",
    ];
    for (int i = 0; i < lines.Length; i++)
    {
        Text(c, "• " + lines[i], X, 350 + (i * 48), regular, 30, new SKColor(214, 206, 190));
    }

    Text(c, "Fork of Moder Warehouse by Cris Haani · works with ContainerSigns", X, 565, regular, 21, new SKColor(150, 160, 140));
    return surface.Snapshot();
}

// A font by name, else from its file (the name is not always registered), else the default.
static SKTypeface Typeface(string family, SKFontStyle style, string file)
{
    var byName = SKTypeface.FromFamilyName(family, style);
    if (byName.FamilyName == family)
    {
        return byName;
    }

    byName.Dispose();
    return File.Exists(file) ? SKTypeface.FromFile(file) : SKTypeface.Default;
}

// Text whose top is at y (as the eye reads a layout), not its baseline.
static void Text(SKCanvas c, string text, float x, float y, SKTypeface face, float size, SKColor color)
{
    using var font = new SKFont(face, size);
    using var paint = Paint(color);
    c.DrawText(text, x, y - font.Metrics.Ascent, SKTextAlign.Left, font, paint);
}

// A rounded rectangle filled, its outline drawn inside its edge.
static void RoundRect(SKCanvas c, float left, float top, float right, float bottom, float radius, SKColor fill, SKColor outline, float width)
{
    using (var paint = Paint(fill))
    {
        c.DrawRoundRect(new SKRect(left, top, right, bottom), radius, radius, paint);
    }

    using var edge = Stroke(outline, width);
    float inset = width / 2;
    c.DrawRoundRect(new SKRect(left + inset, top + inset, right - inset, bottom - inset), radius - inset, radius - inset, edge);
}

static void Rect(SKCanvas c, float left, float top, float right, float bottom, SKColor color)
{
    using var paint = Paint(color);
    c.DrawRect(new SKRect(left, top, right, bottom), paint);
}

static void Line(SKCanvas c, float x0, float y0, float x1, float y1, SKColor color, float width)
{
    using var paint = Stroke(color, width);
    c.DrawLine(x0, y0, x1, y1, paint);
}

static SKPaint Paint(SKColor color) => new() { Color = color, IsAntialias = true, Style = SKPaintStyle.Fill };

static SKPaint Stroke(SKColor color, float width) => new() { Color = color, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = width };

static void Save(SKImage image, string path)
{
    using var data = image.Encode(SKEncodedImageFormat.Png, 100);
    using var file = File.Create(path);
    data.SaveTo(file);
    Console.WriteLine($"{path}: {image.Width} × {image.Height}");
}
