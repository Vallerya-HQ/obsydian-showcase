using Obsydian.Content;
using Obsydian.Core;
using Obsydian.Core.Math;
using Obsydian.Core.Scenes;
using Obsydian.Input;
using Showcase.Common;

var app = new LoadingDemoApp();
app.Run();

// ─── Fake asset type for demo ──────────────────────────────────────────────────

sealed class FakeAsset
{
    public string Name { get; init; } = "";
}

sealed class FakeAssetLoader : IAssetLoader<FakeAsset>
{
    public FakeAsset Load(string fullPath)
    {
        // Simulate slow loading (200ms per asset)
        Thread.Sleep(200);
        return new FakeAsset { Name = Path.GetFileNameWithoutExtension(fullPath) };
    }
}

// ─── The App ───────────────────────────────────────────────────────────────────

sealed class LoadingDemoApp : ShowcaseApp
{
    private SceneManager _scenes = null!;

    public LoadingDemoApp() : base(new EngineConfig
    {
        Title = "Obsydian Async Loading Demo",
        WindowWidth = 1280,
        WindowHeight = 720,
        Version = "0.1.0"
    })
    { }

    protected override Color ClearColor => new(15, 15, 30);

    protected override void OnLoad()
    {
        // Register the fake loader
        Content.RegisterLoader(new FakeAssetLoader());

        _scenes = new SceneManager();
        _scenes.Push(new LoadingScene(this));
    }

    protected override void OnUpdate(float dt)
    {
        _scenes.Update(dt);
    }

    protected override void OnRender(float dt)
    {
        _scenes.Render(dt);
    }

    public SceneManager Scenes => _scenes;
}

// ─── Loading Scene ─────────────────────────────────────────────────────────────

sealed class LoadingScene : IScene
{
    private readonly LoadingDemoApp _app;
    private volatile string _lastLoaded = "";
    private volatile int _loaded;
    private float _spinAngle;
    private bool _done;
    private float _doneDelay;

    private static readonly string[] AssetNames =
    [
        "textures/player", "textures/enemy", "textures/npc",
        "textures/tileset", "textures/particles",
        "audio/music", "audio/sfx_coin", "audio/sfx_hit",
        "data/level_01", "data/dialogue"
    ];

    private int TotalCount => AssetNames.Length;
    private float Progress => TotalCount > 0 ? (float)_loaded / TotalCount : 1f;
    private bool IsComplete => _loaded >= TotalCount;

    public LoadingScene(LoadingDemoApp app) => _app = app;

    public void Enter()
    {
        _loaded = 0;
        _done = false;

        // Load sequentially on a single background thread to avoid
        // concurrent Dictionary access in ContentManager
        Task.Run(() =>
        {
            foreach (var asset in AssetNames)
            {
                _app.Content.Load<FakeAsset>(asset);
                Interlocked.Increment(ref _loaded);
                _lastLoaded = Path.GetFileName(asset);
            }
        });
    }

    public void Exit() { }

    public void Update(float dt)
    {
        _spinAngle += dt * 360f;

        if (_app.Input.IsKeyPressed(Key.Escape))
            _app.Window.Close();

        if (IsComplete && !_done)
        {
            _done = true;
            _doneDelay = 0;
        }

        if (_done)
        {
            _doneDelay += dt;
            if (_doneDelay > 1f)
                _app.Scenes.Switch(new MainScene(_app));
        }
    }

    public void Render(float dt)
    {
        var r = _app.Renderer;

        r.DrawText("ASYNC LOADING DEMO", new Vec2(440, 100), Color.White, 4f);
        r.DrawText("Background loading with progress tracking", new Vec2(380, 150), new Color(150, 150, 150), 2f);

        // Progress bar
        float barW = 600, barH = 30;
        float barX = (1280 - barW) / 2;
        float barY = 320;

        r.DrawRect(new Rect(barX - 2, barY - 2, barW + 4, barH + 4), new Color(80, 80, 80));
        r.DrawRect(new Rect(barX, barY, barW, barH), new Color(30, 30, 30));

        float fillW = barW * Progress;
        if (fillW > 0)
        {
            var barColor = _done ? Color.Green : new Color(60, 140, 255);
            r.DrawRect(new Rect(barX, barY, fillW, barH), barColor);
        }

        // Progress text
        var pctText = $"{Progress * 100:F0}%";
        r.DrawText(pctText, new Vec2(barX + barW / 2 - 20, barY + 4), Color.White, 3f);

        // Counter
        r.DrawText($"{_loaded} / {TotalCount} assets loaded",
            new Vec2(barX, barY + 50), new Color(180, 180, 180), 2f);

        // Last loaded asset
        if (!string.IsNullOrEmpty(_lastLoaded))
            r.DrawText($"Loading: {_lastLoaded}", new Vec2(barX, barY + 80), new Color(100, 200, 100), 2f);

        // Spinning indicator
        if (!_done)
        {
            float cx = 640, cy = 260;
            float radius = 12;
            for (int i = 0; i < 8; i++)
            {
                float angle = (_spinAngle + i * 45f) * MathF.PI / 180f;
                float dx = MathF.Cos(angle) * radius;
                float dy = MathF.Sin(angle) * radius;
                byte alpha = (byte)(255 - i * 28);
                r.DrawRect(new Rect(cx + dx - 2, cy + dy - 2, 4, 4), Color.White.WithAlpha(alpha));
            }
        }
        else
        {
            r.DrawText("Complete!", new Vec2(560, 440), Color.Gold, 3f);
        }

        r.DrawText("ESC=Quit", new Vec2(20, 700), new Color(100, 100, 100), 2f);
    }
}

// ─── Main Scene (post-loading) ─────────────────────────────────────────────────

sealed class MainScene : IScene
{
    private readonly LoadingDemoApp _app;
    private float _pulseTimer;

    public MainScene(LoadingDemoApp app) => _app = app;

    public void Enter() { }
    public void Exit() { }

    public void Update(float dt)
    {
        _pulseTimer += dt;

        if (_app.Input.IsKeyPressed(Key.Escape))
            _app.Window.Close();

        if (_app.Input.IsKeyPressed(Key.R))
            _app.Scenes.Switch(new LoadingScene(_app));
    }

    public void Render(float dt)
    {
        var r = _app.Renderer;

        r.DrawText("ALL ASSETS LOADED!", new Vec2(380, 250), Color.Gold, 5f);

        byte alpha = (byte)(180 + 75 * MathF.Sin(_pulseTimer * 3f));
        r.DrawText("10 assets loaded via AsyncContentQueue", new Vec2(340, 340), Color.White.WithAlpha(alpha), 2.5f);

        r.DrawText("This scene would normally use the loaded assets", new Vec2(310, 400), new Color(150, 150, 150), 2f);
        r.DrawText("for rendering game content.", new Vec2(430, 425), new Color(150, 150, 150), 2f);

        r.DrawText("Press R to reload  |  ESC to quit", new Vec2(400, 550), new Color(100, 100, 100), 2f);
    }
}
