using SkiaSharp;

// ---------------------------------------------------------------------------
// Nexus Explorer icon & MSIX asset generator.
//
// Draws the app badge (dark rounded square + git-graph style "N") as vectors
// with SkiaSharp and writes:
//   media/                 -> icon-*.png + app.ico (exe / window / tray icon)
//   packaging/Assets/      -> every MSIX visual asset the Store manifest needs
//   packaging/StoreListing -> square logos for the Partner Center listing
//
// Usage (from the repo root):  dotnet run --project tools/IconGen
// ---------------------------------------------------------------------------

var repoRoot = FindRepoRoot();
var mediaDir = Path.Combine(repoRoot, "media");
var assetsDir = Path.Combine(repoRoot, "packaging", "Assets");
var listingDir = Path.Combine(repoRoot, "packaging", "StoreListing");
Directory.CreateDirectory(mediaDir);
Directory.CreateDirectory(assetsDir);
Directory.CreateDirectory(listingDir);

var written = 0;

// ---- 1. Classic icons (exe, window, tray) ---------------------------------
foreach (var sz in new[] { 16, 32, 48, 64, 128, 256, 512 })
    Save(RenderSquare(sz, 1f), Path.Combine(mediaDir, $"icon-{sz}.png"));
WriteIco(Path.Combine(mediaDir, "app.ico"), [16, 20, 24, 32, 40, 48, 64, 128, 256]);

// ---- 2. MSIX assets ---------------------------------------------------------
// Scale qualifiers: 100% 125% 150% 200% 400%.
var scales = new[] { (100, 1.00f), (125, 1.25f), (150, 1.50f), (200, 2.00f), (400, 4.00f) };

// App list / taskbar icon (Square44x44Logo). The badge fills the frame.
foreach (var (q, f) in scales)
    Save(RenderSquare(Px(44, f), 1f), Asset($"Square44x44Logo.scale-{q}.png"));

// Target-size variants: used for taskbar, Start's all-apps list, search, Explorer, etc.
// Plain, unplated (dark taskbar) and light-unplated (light taskbar) are all required,
// otherwise Windows draws the icon on an accent-colored plate.
foreach (var t in new[] { 16, 20, 24, 30, 32, 36, 40, 48, 60, 64, 72, 80, 96, 256 })
{
    var bmp = RenderSquare(t, 1f);
    Save(bmp, Asset($"Square44x44Logo.targetsize-{t}.png"));
    Save(bmp, Asset($"Square44x44Logo.targetsize-{t}_altform-unplated.png"));
    Save(bmp, Asset($"Square44x44Logo.targetsize-{t}_altform-lightunplated.png"));
}

// Tiles: the badge sits centered with padding, on a transparent background.
foreach (var (q, f) in scales)
{
    Save(RenderSquare(Px(71, f), 0.70f), Asset($"SmallTile.scale-{q}.png"));
    Save(RenderSquare(Px(150, f), 0.62f), Asset($"Square150x150Logo.scale-{q}.png"));
    Save(RenderSquare(Px(310, f), 0.55f), Asset($"LargeTile.scale-{q}.png"));
    Save(RenderWide(Px(310, f), Px(150, f), 0.66f), Asset($"Wide310x150Logo.scale-{q}.png"));
    Save(RenderWide(Px(620, f), Px(300, f), 0.50f), Asset($"SplashScreen.scale-{q}.png"));
    Save(RenderSquare(Px(50, f), 1f), Asset($"StoreLogo.scale-{q}.png"));
}

// ---- 3. Partner Center listing logos ---------------------------------------
Save(RenderSquare(300, 1f), Path.Combine(listingDir, "StoreLogo-300x300.png"));
Save(RenderSquare(1080, 0.80f, opaqueBackground: true), Path.Combine(listingDir, "BoxArt-1080x1080.png"));
Save(RenderSquare(2160, 0.80f, opaqueBackground: true), Path.Combine(listingDir, "BoxArt-2160x2160.png"));

Console.WriteLine($"Done. {written} files written.");
Console.WriteLine($"  media:          {mediaDir}");
Console.WriteLine($"  msix assets:    {assetsDir}");
Console.WriteLine($"  store listing:  {listingDir}");
return;

// ===========================================================================
// Helpers
// ===========================================================================

string Asset(string name) => Path.Combine(assetsDir, name);

static int Px(int baseSize, float factor) => (int)MathF.Round(baseSize * factor);

void Save(SKBitmap bmp, string path)
{
    using var img = SKImage.FromBitmap(bmp);
    using var data = img.Encode(SKEncodedImageFormat.Png, 100);
    using var fs = File.Create(path);
    data.SaveTo(fs);
    written++;
}

// Square canvas; the badge occupies `fill` (0..1) of the side, centered.
SKBitmap RenderSquare(int size, float fill, bool opaqueBackground = false)
{
    var bmp = new SKBitmap(size, size, SKColorType.Rgba8888, SKAlphaType.Premul);
    using var c = new SKCanvas(bmp);
    c.Clear(opaqueBackground ? new SKColor(0x0d, 0x15, 0x26) : SKColors.Transparent);
    var badge = size * fill;
    DrawBadge(c, (size - badge) / 2f, (size - badge) / 2f, badge);
    return bmp;
}

// Wide canvas; the badge height is `fill` of the canvas height, centered.
SKBitmap RenderWide(int w, int h, float fill)
{
    var bmp = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
    using var c = new SKCanvas(bmp);
    c.Clear(SKColors.Transparent);
    var badge = h * fill;
    DrawBadge(c, (w - badge) / 2f, (h - badge) / 2f, badge);
    return bmp;
}

void WriteIco(string path, int[] sizes)
{
    var blobs = sizes.Select(sz =>
    {
        using var img = SKImage.FromBitmap(RenderSquare(sz, 1f));
        return img.Encode(SKEncodedImageFormat.Png, 100).ToArray();
    }).ToList();

    using var fs = File.Create(path);
    using var bw = new BinaryWriter(fs);
    bw.Write((short)0);            // reserved
    bw.Write((short)1);            // type: icon
    bw.Write((short)sizes.Length); // image count
    var offset = 6 + 16 * sizes.Length;
    for (var i = 0; i < sizes.Length; i++)
    {
        var sz = sizes[i];
        bw.Write((byte)(sz >= 256 ? 0 : sz)); // width  (0 means 256)
        bw.Write((byte)(sz >= 256 ? 0 : sz)); // height
        bw.Write((byte)0);   // palette size
        bw.Write((byte)0);   // reserved
        bw.Write((short)1);  // color planes
        bw.Write((short)32); // bits per pixel
        bw.Write(blobs[i].Length);
        bw.Write(offset);
        offset += blobs[i].Length;
    }
    foreach (var b in blobs) bw.Write(b);
    written++;
}

static string FindRepoRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "NexusExplorer.slnx")))
        dir = dir.Parent;
    return dir?.FullName ?? throw new InvalidOperationException("Run from inside the NexusExplorer repo.");
}

// ===========================================================================
// The artwork. Designed on a 1024 grid, drawn at (x, y) with side `size`.
// ===========================================================================
static void DrawBadge(SKCanvas c, float x, float y, float size)
{
    const float D = 1024f;
    c.Save();
    c.Translate(x, y);
    c.Scale(size / D);

    var r = D * 0.215f;
    var badge = new SKRect(0, 0, D, D);

    // Background: vertical dark-navy gradient.
    using (var bg = new SKPaint { IsAntialias = true })
    {
        bg.Shader = SKShader.CreateLinearGradient(
            new SKPoint(0, 0), new SKPoint(0, D),
            [new SKColor(0x25, 0x33, 0x4d), new SKColor(0x0d, 0x15, 0x26)],
            [0f, 1f], SKShaderTileMode.Clamp);
        c.DrawRoundRect(badge, r, r, bg);
    }
    // Soft top highlight.
    using (var hi = new SKPaint { IsAntialias = true })
    {
        hi.Shader = SKShader.CreateLinearGradient(
            new SKPoint(0, 0), new SKPoint(0, D * 0.5f),
            [new SKColor(0xff, 0xff, 0xff, 36), new SKColor(0xff, 0xff, 0xff, 0)],
            [0f, 1f], SKShaderTileMode.Clamp);
        c.DrawRoundRect(badge, r, r, hi);
    }
    // Thin inner border.
    using (var border = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = D * 0.006f })
    {
        border.Color = new SKColor(0xff, 0xff, 0xff, 22);
        var inset = badge;
        inset.Inflate(-border.StrokeWidth, -border.StrokeWidth);
        c.DrawRoundRect(inset, r, r, border);
    }

    // Node layout of the "N".
    float lx = D * 0.30f, rx = D * 0.70f, ty = D * 0.28f, by = D * 0.72f;
    var tl = new SKPoint(lx, ty);
    var tr = new SKPoint(rx, ty);
    var bl = new SKPoint(lx, by);
    var br = new SKPoint(rx, by);
    var stroke = D * 0.052f;

    using var gradient = SKShader.CreateLinearGradient(
        new SKPoint(lx, by), new SKPoint(rx, ty),
        [new SKColor(0x1d, 0x4e, 0xd8), new SKColor(0x3b, 0x82, 0xf6), new SKColor(0x2d, 0xd4, 0xbf)],
        [0f, 0.5f, 1f], SKShaderTileMode.Clamp);

    using var path = new SKPath();
    path.MoveTo(bl); path.LineTo(tl); // left stem
    path.MoveTo(tl); path.LineTo(br); // diagonal
    path.MoveTo(tr); path.LineTo(br); // right stem
    // Short counter-diagonal: the small "graph" crossing in the middle.
    path.MoveTo(lx + (rx - lx) * 0.30f, ty + (by - ty) * 0.62f);
    path.LineTo(lx + (rx - lx) * 0.70f, ty + (by - ty) * 0.38f);

    using (var shadow = new SKPaint
    {
        IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = stroke,
        StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round,
        Color = new SKColor(0, 0, 0, 110),
        ImageFilter = SKImageFilter.CreateBlur(D * 0.012f, D * 0.012f)
    })
    {
        c.Save();
        c.Translate(0, D * 0.012f);
        c.DrawPath(path, shadow);
        c.Restore();
    }

    using (var sp = new SKPaint
    {
        IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = stroke,
        StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round, Shader = gradient
    })
        c.DrawPath(path, sp);

    // Ring nodes.
    var ringR = D * 0.066f;
    var ringStroke = D * 0.030f;
    foreach (var p in new[] { tl, tr, bl, br })
    {
        using (var sh = new SKPaint
        {
            IsAntialias = true, Color = new SKColor(0, 0, 0, 120),
            ImageFilter = SKImageFilter.CreateBlur(D * 0.012f, D * 0.012f)
        })
            c.DrawCircle(p.X, p.Y + D * 0.012f, ringR, sh);

        using (var hole = new SKPaint { IsAntialias = true, Color = new SKColor(0x12, 0x1c, 0x30) })
            c.DrawCircle(p.X, p.Y, ringR, hole);

        using (var ring = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = ringStroke, Shader = gradient
        })
            c.DrawCircle(p.X, p.Y, ringR, ring);
    }

    c.Restore();
}
