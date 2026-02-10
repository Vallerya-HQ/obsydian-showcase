using Obsydian.Core;
using Obsydian.Core.Math;
using Obsydian.Graphics;
using Obsydian.Input;
using Obsydian.Platform.Desktop.Rendering;
using Showcase.Common;
using Texture = Obsydian.Graphics.Texture;

var app = new AtlasDemoApp();
app.Run();

// ─── The App ───────────────────────────────────────────────────────────────────

sealed class AtlasDemoApp : ShowcaseApp
{
    private SpriteSheet _atlas = null!;
    private Texture _rawAtlasTexture = null!;
    private AtlasBuildResult _buildResult = null!;

    public AtlasDemoApp() : base(new EngineConfig
    {
        Title = "Obsydian TextureAtlas Demo",
        WindowWidth = 1280,
        WindowHeight = 720,
        Version = "0.1.0"
    })
    { }

    protected override Color ClearColor => new(25, 25, 40);

    protected override void OnLoad()
    {
        // Generate procedural RGBA pixel arrays of different sizes and colors
        var regions = new (string name, int w, int h, Color color)[]
        {
            ("player",    32, 32, new Color(80, 200, 80)),
            ("enemy",     32, 32, new Color(200, 60, 60)),
            ("tree",      24, 48, new Color(30, 120, 30)),
            ("rock",      20, 16, new Color(140, 140, 150)),
            ("chest",     16, 16, Color.Gold),
            ("potion_r",  12, 16, new Color(200, 40, 40)),
            ("potion_b",  12, 16, new Color(40, 80, 200)),
            ("sword",     8,  24, new Color(180, 180, 200)),
            ("shield",    16, 18, new Color(100, 80, 50)),
            ("coin",      8,  8,  new Color(255, 220, 50)),
            ("gem",       10, 10, new Color(100, 200, 255)),
            ("skull",     14, 14, new Color(220, 220, 210)),
            ("heart",     12, 12, Color.Red),
            ("star",      10, 10, new Color(255, 255, 100)),
            ("arrow",     6,  16, new Color(160, 120, 60)),
            ("key",       8,  14, Color.Gold),
        };

        var atlas = new TextureAtlas(256, 256);

        foreach (var (name, w, h, color) in regions)
        {
            var pixels = GenerateRegionPixels(w, h, color);
            atlas.AddRegion(name, w, h, pixels);
        }

        _buildResult = atlas.Pack();
        _rawAtlasTexture = GlTexture.Create(Gl, _buildResult.Width, _buildResult.Height, _buildResult.Pixels, "__atlas_raw");

        _atlas = atlas.Build((pixels, w, h) =>
            GlTexture.Create(Gl, w, h, pixels, "__atlas_packed"));
    }

    protected override void OnUpdate(float dt)
    {
        if (Input.IsKeyPressed(Key.Escape))
            Window.Close();
    }

    protected override void OnRender(float dt)
    {
        var r = Renderer;

        // Title
        r.DrawText("TextureAtlas Demo", new Vec2(20, 15), Color.White, 3f);
        r.DrawText($"{_buildResult.Regions.Count} regions packed into {_buildResult.Width}x{_buildResult.Height} atlas",
            new Vec2(20, 50), new Color(150, 150, 150), 2f);

        // Draw each region individually in a grid
        r.DrawText("Individual Regions:", new Vec2(20, 90), new Color(100, 255, 100), 2f);

        float gridX = 20;
        float gridY = 120;
        float maxRowH = 0;

        foreach (var region in _buildResult.Regions)
        {
            var src = region.SourceRect;
            if (gridX + src.Width * 2 > 800)
            {
                gridX = 20;
                gridY += maxRowH * 2 + 30;
                maxRowH = 0;
            }

            // Draw scaled-up region using DrawSprite
            r.DrawSprite(_atlas.Texture, new Vec2(gridX, gridY), src, new Vec2(2, 2), 0f, Color.White);

            // Label
            r.DrawText(region.Name, new Vec2(gridX, gridY + src.Height * 2 + 2), new Color(180, 180, 180), 1f);

            gridX += System.Math.Max(src.Width * 2, 50) + 10;
            maxRowH = System.Math.Max(maxRowH, src.Height);
        }

        // Draw the raw atlas texture in the right side
        r.DrawText("Raw Atlas (256x256):", new Vec2(850, 90), new Color(255, 200, 100), 2f);

        // Draw atlas background
        r.DrawRect(new Rect(850, 120, 256, 256), new Color(40, 40, 60));

        // Draw the raw atlas at 1:1
        r.DrawSprite(_rawAtlasTexture, new Vec2(850, 120));

        // Outline
        r.DrawRect(new Rect(850, 120, 256, 256), new Color(100, 100, 100), filled: false);

        // Draw region outlines on the atlas visualization
        foreach (var region in _buildResult.Regions)
        {
            var src = region.SourceRect;
            r.DrawRect(new Rect(850 + src.X, 120 + src.Y, src.Width, src.Height),
                new Color(255, 255, 255, 60), filled: false);
        }

        // Controls
        r.DrawText("ESC=Quit", new Vec2(20, 700), new Color(100, 100, 100), 2f);
    }

    private static byte[] GenerateRegionPixels(int w, int h, Color baseColor)
    {
        var pixels = new byte[w * h * 4];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            int idx = (y * w + x) * 4;
            bool border = x == 0 || y == 0 || x == w - 1 || y == h - 1;

            if (border)
            {
                pixels[idx] = (byte)(baseColor.R / 2);
                pixels[idx + 1] = (byte)(baseColor.G / 2);
                pixels[idx + 2] = (byte)(baseColor.B / 2);
                pixels[idx + 3] = 255;
            }
            else
            {
                byte shade = (byte)(200 + 55 * y / h);
                pixels[idx] = (byte)(baseColor.R * shade / 255);
                pixels[idx + 1] = (byte)(baseColor.G * shade / 255);
                pixels[idx + 2] = (byte)(baseColor.B * shade / 255);
                pixels[idx + 3] = 255;
            }
        }
        return pixels;
    }
}
