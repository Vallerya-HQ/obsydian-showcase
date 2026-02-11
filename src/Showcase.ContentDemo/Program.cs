using Obsydian.Content;
using Obsydian.Content.Data;
using Obsydian.Content.Localization;
using Obsydian.Content.Validation;
using Obsydian.Core;
using Obsydian.Core.Math;
using Obsydian.Core.Scenes;
using Obsydian.Graphics;
using Obsydian.Input;
using Showcase.Common;

var app = new ContentDemoApp();
app.Run();

// ─── Data types ────────────────────────────────────────────────────────────────

sealed class ItemData : IGameData
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public int Damage { get; init; }
    public int Value { get; init; }
    public string Rarity { get; init; } = "common";
}

sealed class EnemyData : IGameData
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public int Hp { get; init; }
    public int Damage { get; init; }
    public int Xp { get; init; }
}

// ─── The App ───────────────────────────────────────────────────────────────────

sealed class ContentDemoApp : ShowcaseApp
{
    public SceneManager Scenes { get; private set; } = null!;
    public LocalizedContentManager Localized { get; private set; } = null!;
    public DataLoader Data { get; private set; } = null!;
    public ContentManifest Manifest { get; private set; } = null!;

    public ContentDemoApp() : base(new EngineConfig
    {
        Title = "Obsydian Content Pipeline Demo",
        WindowWidth = 1280,
        WindowHeight = 720,
        Version = "0.1.0"
    })
    { }

    protected override Color ClearColor => new(18, 18, 28);

    protected override void OnLoad()
    {
        // Register loaders for our data types
        Content.RegisterLoader(new GameDataLoader<ItemData>());
        Content.RegisterLoader(new GameDataLoader<EnemyData>());
        Content.RegisterLoader(new StringTableLoader());

        // Set up localized content manager
        Localized = new LocalizedContentManager(Content);

        // Set up typed data loader
        Data = new DataLoader(Content);
        Data.Register<GameDataCollection<ItemData>>("content/data/items.json");
        Data.Register<GameDataCollection<EnemyData>>("content/data/enemies.json");

        // Scan content directory to build manifest
        var contentRoot = System.IO.Path.Combine(Content.RootPath, "content");
        Manifest = ContentManifest.ScanDirectory(contentRoot);

        Scenes = new SceneManager();
        Scenes.Push(new DemoScene(this));
    }

    protected override void OnUpdate(float dt) => Scenes.Update(dt);
    protected override void OnRender(float dt) => Scenes.Render(dt);
    protected override SceneManager? GetSceneManager() => Scenes;
}

// ─── Demo Scene ────────────────────────────────────────────────────────────────

sealed class DemoScene : IScene
{
    private readonly ContentDemoApp _app;
    private int _tab; // 0=manifest, 1=data, 2=locale, 3=scope
    private bool _isJapanese;
    private ContentScope? _activeScope;
    private bool _scopeActive;
    private float _timer;

    // Loaded data
    private GameDataCollection<ItemData>? _items;
    private GameDataCollection<EnemyData>? _enemies;
    private ContentValidator? _validator;

    // Colors
    static readonly Color Bg = new(25, 25, 40);
    static readonly Color Panel = new(35, 35, 55);
    static readonly Color Accent = new(80, 160, 255);
    static readonly Color AccentDim = new(50, 100, 180);
    static readonly Color TextDim = new(120, 120, 140);
    static readonly Color TextBright = new(220, 220, 230);
    static readonly Color Success = new(80, 200, 120);
    static readonly Color Warning = new(255, 200, 60);
    static readonly Color Rare = new(100, 180, 255);
    static readonly Color Legendary = new(255, 165, 0);

    public DemoScene(ContentDemoApp app) => _app = app;

    public void Enter()
    {
        // Load game data via the typed DataLoader
        _items = _app.Data.Load<GameDataCollection<ItemData>>();
        _enemies = _app.Data.Load<GameDataCollection<EnemyData>>();

        // Run content validation
        var contentRoot = Path.Combine(_app.Content.RootPath, "content");
        _validator = new ContentValidator();
        _validator.ValidateManifest(_app.Manifest, contentRoot);
    }

    public void Exit() => _activeScope?.Dispose();

    public void Update(float dt)
    {
        _timer += dt;

        if (_app.Input.IsKeyPressed(Key.Escape))
            _app.Window.Close();

        // Tab switching
        if (_app.Input.IsKeyPressed(Key.D1)) _tab = 0;
        if (_app.Input.IsKeyPressed(Key.D2)) _tab = 1;
        if (_app.Input.IsKeyPressed(Key.D3)) _tab = 2;
        if (_app.Input.IsKeyPressed(Key.D4)) _tab = 3;

        // Toggle language
        if (_app.Input.IsKeyPressed(Key.L))
        {
            _isJapanese = !_isJapanese;
            _app.Localized.SetLanguage(_isJapanese ? LanguageCode.Ja : LanguageCode.Default);
        }

        // Toggle scope
        if (_app.Input.IsKeyPressed(Key.S))
        {
            if (_scopeActive)
            {
                _activeScope?.Dispose();
                _activeScope = null;
                _scopeActive = false;
            }
            else
            {
                _activeScope = new ContentScope(_app.Content, "DemoScope");
                // Load items through the scope so they're tracked
                _activeScope.Load<GameDataCollection<ItemData>>("content/data/items.json");
                _activeScope.Load<GameDataCollection<EnemyData>>("content/data/enemies.json");
                _scopeActive = true;
            }
        }
    }

    public void Render(float dt)
    {
        var r = _app.Renderer;

        // ── Header ──
        r.DrawRect(new Rect(0, 0, 1280, 60), Panel);

        var title = Str("title");
        var subtitle = Str("subtitle");
        r.DrawText(title, new Vec2(20, 8), Accent, 4f);
        r.DrawText(subtitle, new Vec2(20, 38), TextDim, 2f);

        // ── Tab bar ──
        r.DrawRect(new Rect(0, 60, 1280, 32), new Color(30, 30, 48));
        string[] tabLabels = [Str("tab_manifest"), Str("tab_data"), Str("tab_locale"), Str("tab_scope")];
        for (int i = 0; i < tabLabels.Length; i++)
        {
            float tx = 20 + i * 200;
            var tabColor = i == _tab ? Accent : TextDim;
            var indicator = i == _tab ? ">" : " ";
            r.DrawText($"{indicator} {i + 1}. {tabLabels[i]}", new Vec2(tx, 66), tabColor, 2.5f);
        }

        // ── Content area ──
        float cy = 110;

        switch (_tab)
        {
            case 0: RenderManifestTab(r, cy); break;
            case 1: RenderDataTab(r, cy); break;
            case 2: RenderLocaleTab(r, cy); break;
            case 3: RenderScopeTab(r, cy); break;
        }

        // ── Footer ──
        r.DrawRect(new Rect(0, 690, 1280, 30), Panel);
        var hint = Str("hint_controls");
        r.DrawText(hint, new Vec2(20, 696), TextDim, 2f);

        // Cache stats
        r.DrawText($"Cache: {_app.Content.CachedCount} assets", new Vec2(1050, 696), AccentDim, 2f);
    }

    // ── Tab: Manifest ──────────────────────────────────────────────────────────

    void RenderManifestTab(IRenderer r, float y)
    {
        var manifest = _app.Manifest;

        // Summary box
        DrawPanel(r, 20, y, 600, 160);
        r.DrawText("CONTENT MANIFEST", new Vec2(40, y + 10), Accent, 3f);
        r.DrawText($"{Str("label_total")}: {manifest.Count}", new Vec2(40, y + 40), TextBright, 2.5f);

        var textures = manifest.GetByType("Texture").Count();
        var audio = manifest.GetByType("Audio").Count();
        var data = manifest.GetByType("Data").Count();
        r.DrawText($"{Str("label_textures")}: {textures}   {Str("label_audio")}: {audio}   {Str("label_data")}: {data}",
            new Vec2(40, y + 65), TextDim, 2f);

        // Validation result
        var validColor = _validator?.HasErrors == true ? Warning : Success;
        var validText = _validator?.HasErrors == true
            ? $"Validation: {_validator.Issues.Count} issues"
            : "Validation: All clear";
        r.DrawText(validText, new Vec2(40, y + 95), validColor, 2.5f);

        if (_validator is not null)
        {
            int warnCount = _validator.Issues.Count(i => i.Severity == IssueSeverity.Warning);
            int errCount = _validator.Issues.Count(i => i.Severity == IssueSeverity.Error);
            r.DrawText($"  Errors: {errCount}  Warnings: {warnCount}", new Vec2(40, y + 120), TextDim, 2f);
        }

        // Asset list
        DrawPanel(r, 20, y + 180, 600, 380);
        r.DrawText("ALL ASSETS", new Vec2(40, y + 190), Accent, 2.5f);

        float row = y + 220;
        foreach (var entry in manifest.Entries.Values.Take(15))
        {
            var typeColor = entry.TypeTag switch
            {
                "Texture" => new Color(100, 200, 100),
                "Audio" => new Color(200, 150, 100),
                "Data" => Rare,
                _ => TextDim
            };

            r.DrawText($"[{entry.TypeTag,-8}]", new Vec2(40, row), typeColor, 2f);
            r.DrawText(entry.Path, new Vec2(180, row), TextBright, 2f);
            r.DrawText($"{entry.SizeBytes}B", new Vec2(520, row), TextDim, 2f);
            row += 22;
        }

        // Search demo
        DrawPanel(r, 640, y, 620, 160);
        r.DrawText("SEARCH: \"data/*\"", new Vec2(660, y + 10), Accent, 2.5f);
        float sr = y + 40;
        foreach (var match in manifest.Search("data/*"))
        {
            r.DrawText($"  {match.Path}", new Vec2(660, sr), Success, 2f);
            sr += 22;
        }

        r.DrawText("SEARCH: \"*.json\"", new Vec2(660, sr + 10), Accent, 2.5f);
        sr += 40;
        foreach (var match in manifest.Search("*.json"))
        {
            r.DrawText($"  {match.Path}", new Vec2(660, sr), Success, 2f);
            sr += 22;
        }

        // Type filter demo
        DrawPanel(r, 640, y + 180, 620, 180);
        r.DrawText("FILTER BY TYPE", new Vec2(660, y + 190), Accent, 2.5f);
        float fr = y + 220;
        foreach (var typeTag in new[] { "Data", "Texture", "Audio" })
        {
            var count = manifest.GetByType(typeTag).Count();
            var barW = count * 80f;
            r.DrawRect(new Rect(660, fr, barW, 16), AccentDim);
            r.DrawText($"{typeTag}: {count}", new Vec2(660 + barW + 10, fr - 2), TextBright, 2f);
            fr += 30;
        }
    }

    // ── Tab: Game Data ─────────────────────────────────────────────────────────

    void RenderDataTab(IRenderer r, float y)
    {
        // Items panel
        DrawPanel(r, 20, y, 600, 560);
        r.DrawText($"{Str("label_items")} ({_items?.Count ?? 0})", new Vec2(40, y + 10), Accent, 3f);
        r.DrawText("Loaded via: DataLoader + GameDataCollection<ItemData>", new Vec2(40, y + 40), TextDim, 2f);

        float row = y + 70;
        if (_items is not null)
        {
            foreach (var item in _items.Items)
            {
                var rarityColor = item.Rarity switch
                {
                    "common" => TextBright,
                    "uncommon" => Success,
                    "rare" => Rare,
                    "legendary" => Legendary,
                    _ => TextDim
                };

                r.DrawText(item.Name, new Vec2(40, row), rarityColor, 2.5f);
                r.DrawText($"DMG:{item.Damage}", new Vec2(300, row), item.Damage > 0 ? new Color(255, 100, 100) : TextDim, 2f);
                r.DrawText($"VAL:{item.Value}g", new Vec2(410, row), Warning, 2f);
                r.DrawText($"[{item.Rarity}]", new Vec2(520, row), rarityColor, 2f);
                row += 32;
            }
        }

        // ID lookup demo
        row += 10;
        r.DrawRect(new Rect(30, row, 580, 2), AccentDim);
        row += 12;
        r.DrawText("ID LOOKUP DEMO", new Vec2(40, row), Accent, 2.5f);
        row += 28;

        var lookup = _items?.GetById("sword_flame");
        if (lookup is not null)
        {
            r.DrawText($"GetById(\"sword_flame\") ->", new Vec2(40, row), TextDim, 2f);
            row += 22;
            r.DrawText($"  Name: {lookup.Name}, Damage: {lookup.Damage}, Rarity: {lookup.Rarity}", new Vec2(40, row), Success, 2f);
        }
        row += 28;
        var exists = _items?.Contains("potion_heal") ?? false;
        r.DrawText($"Contains(\"potion_heal\") -> {exists}", new Vec2(40, row), exists ? Success : new Color(255, 80, 80), 2f);
        row += 22;
        var missing = _items?.Contains("excalibur") ?? false;
        r.DrawText($"Contains(\"excalibur\") -> {missing}", new Vec2(40, row), missing ? Success : new Color(255, 80, 80), 2f);

        // Enemies panel
        DrawPanel(r, 640, y, 620, 560);
        r.DrawText($"{Str("label_enemies")} ({_enemies?.Count ?? 0})", new Vec2(660, y + 10), Accent, 3f);
        r.DrawText("Loaded via: DataLoader + GameDataCollection<EnemyData>", new Vec2(660, y + 40), TextDim, 2f);

        row = y + 70;
        if (_enemies is not null)
        {
            foreach (var enemy in _enemies.Items)
            {
                // HP bar
                float hpFrac = enemy.Hp / 120f;
                float barW = 120 * hpFrac;
                var hpColor = hpFrac > 0.6f ? new Color(200, 60, 60) : hpFrac > 0.3f ? Warning : Success;

                r.DrawText(enemy.Name, new Vec2(660, row), TextBright, 2.5f);
                r.DrawRect(new Rect(860, row + 4, 120, 12), new Color(40, 40, 60));
                r.DrawRect(new Rect(860, row + 4, barW, 12), hpColor);
                r.DrawText($"HP:{enemy.Hp}", new Vec2(990, row), TextDim, 2f);
                r.DrawText($"DMG:{enemy.Damage}", new Vec2(1060, row), new Color(255, 100, 100), 2f);
                r.DrawText($"XP:{enemy.Xp}", new Vec2(1150, row), Success, 2f);
                row += 36;
            }
        }

        // Data registration info
        row += 20;
        r.DrawRect(new Rect(650, row, 600, 2), AccentDim);
        row += 12;
        r.DrawText("REGISTERED DATA TYPES", new Vec2(660, row), Accent, 2.5f);
        row += 28;
        foreach (var (typeName, assetPath) in _app.Data.GetRegistrations())
        {
            r.DrawText($"{typeName}", new Vec2(660, row), Rare, 2f);
            r.DrawText($"-> {assetPath}", new Vec2(900, row), TextDim, 2f);
            row += 22;
        }
    }

    // ── Tab: Localization ──────────────────────────────────────────────────────

    void RenderLocaleTab(IRenderer r, float y)
    {
        DrawPanel(r, 20, y, 1240, 200);
        r.DrawText("LOCALIZATION SYSTEM", new Vec2(40, y + 10), Accent, 3f);

        var langText = Str("msg_language");
        var langColor = _isJapanese ? new Color(255, 100, 150) : Success;
        r.DrawText(langText, new Vec2(40, y + 45), langColor, 3f);

        r.DrawText($"LanguageCode: {_app.Localized.CurrentLanguage}", new Vec2(40, y + 80), TextDim, 2f);
        r.DrawText($"Tag: \"{_app.Localized.CurrentLanguageTag}\"", new Vec2(350, y + 80), TextDim, 2f);
        r.DrawText("Press L to toggle English <-> Japanese", new Vec2(40, y + 105), AccentDim, 2f);

        r.DrawText("Fallback chain: asset.ja.json -> asset.json", new Vec2(40, y + 140), TextDim, 2f);
        var fileUsed = _isJapanese ? "strings/ui.ja.json" : "strings/ui.json";
        r.DrawText($"Currently loaded: {fileUsed}", new Vec2(40, y + 165), Success, 2f);

        // Show all string keys and their resolved values
        DrawPanel(r, 20, y + 220, 1240, 340);
        r.DrawText("STRING TABLE CONTENTS", new Vec2(40, y + 230), Accent, 2.5f);
        r.DrawText("Key", new Vec2(40, y + 260), AccentDim, 2f);
        r.DrawText("Value", new Vec2(340, y + 260), AccentDim, 2f);

        float row = y + 285;
        string[] keys = ["title", "subtitle", "tab_manifest", "tab_data", "tab_locale", "tab_scope",
            "label_items", "label_enemies", "msg_loaded", "msg_language", "hint_controls"];

        foreach (var key in keys)
        {
            var val = Str(key);
            r.DrawText(key, new Vec2(40, row), TextDim, 2f);
            r.DrawText(val, new Vec2(340, row), TextBright, 2f);
            row += 24;
        }
    }

    // ── Tab: Scopes ────────────────────────────────────────────────────────────

    void RenderScopeTab(IRenderer r, float y)
    {
        DrawPanel(r, 20, y, 1240, 200);
        r.DrawText("CONTENT SCOPE & REF COUNTING", new Vec2(40, y + 10), Accent, 3f);
        r.DrawText("Press S to toggle a ContentScope on/off", new Vec2(40, y + 45), AccentDim, 2.5f);

        // Scope status
        var statusText = _scopeActive ? Str("msg_scope_active") : Str("msg_scope_released");
        var statusColor = _scopeActive ? Success : Warning;
        float pulse = _scopeActive ? 255 : (byte)(150 + 50 * MathF.Sin(_timer * 3f));
        r.DrawText(statusText, new Vec2(40, y + 80), statusColor.WithAlpha((byte)pulse), 3f);

        // Visual indicator
        float indicatorX = 40;
        for (int i = 0; i < 5; i++)
        {
            var c = _scopeActive ? Accent : new Color(50, 50, 70);
            r.DrawRect(new Rect(indicatorX, y + 120, 60, 30), c);
            r.DrawText($"A{i + 1}", new Vec2(indicatorX + 14, y + 126), _scopeActive ? Color.White : TextDim, 2f);
            indicatorX += 80;
        }
        r.DrawText(_scopeActive ? "LOADED" : "RELEASED", new Vec2(indicatorX + 10, y + 126),
            _scopeActive ? Success : TextDim, 2f);

        // Scope details
        if (_activeScope is not null)
        {
            r.DrawText($"Scope Name: \"{_activeScope.Name}\"", new Vec2(40, y + 165), TextDim, 2f);
            r.DrawText($"Assets tracked: {_activeScope.AssetCount}", new Vec2(400, y + 165), TextBright, 2f);
        }

        // Ref count view
        DrawPanel(r, 20, y + 220, 1240, 340);
        r.DrawText("REFERENCE COUNTS", new Vec2(40, y + 230), Accent, 2.5f);
        r.DrawText("Asset", new Vec2(40, y + 260), AccentDim, 2f);
        r.DrawText("Refs", new Vec2(700, y + 260), AccentDim, 2f);
        r.DrawText("Status", new Vec2(800, y + 260), AccentDim, 2f);

        float row = y + 285;
        string[] trackedAssets = [
            "content/data/items.json",
            "content/data/enemies.json",
            "content/strings/ui.json",
        ];
        if (_isJapanese)
            trackedAssets = [.. trackedAssets, "content/strings/ui.ja.json"];

        foreach (var path in trackedAssets)
        {
            var refCount = _app.Content.GetRefCount<GameDataCollection<ItemData>>(path);
            if (refCount == 0)
                refCount = _app.Content.GetRefCount<GameDataCollection<EnemyData>>(path);
            if (refCount == 0)
                refCount = _app.Content.GetRefCount<StringTable>(path);

            var cached = _app.Content.GetCachedKeys().Any(k => k.EndsWith($":{path}"));

            r.DrawText(path, new Vec2(40, row), TextBright, 2f);
            r.DrawText($"{refCount}", new Vec2(720, row), refCount > 0 ? Accent : TextDim, 2.5f);

            var statusStr = cached ? "CACHED" : "EVICTED";
            var statusClr = cached ? Success : new Color(255, 80, 80);
            r.DrawText(statusStr, new Vec2(800, row), statusClr, 2f);
            row += 28;
        }

        // Total cache stats
        row += 10;
        r.DrawRect(new Rect(30, row, 1220, 2), AccentDim);
        row += 12;
        r.DrawText($"Total cached assets: {_app.Content.CachedCount}", new Vec2(40, row), TextBright, 2.5f);

        row += 28;
        r.DrawText("Scope loads assets with ContentLifetime.Scoped", new Vec2(40, row), TextDim, 2f);
        row += 22;
        r.DrawText("Disposing the scope calls Release<T>() for each tracked asset", new Vec2(40, row), TextDim, 2f);
        row += 22;
        r.DrawText("When ref count hits 0, asset is evicted from cache", new Vec2(40, row), TextDim, 2f);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    string Str(string key) => _app.Localized.LoadString($"content/strings/ui.json:{key}");

    static void DrawPanel(IRenderer r, float x, float y, float w, float h)
    {
        r.DrawRect(new Rect(x, y, w, h), Panel);
        r.DrawRect(new Rect(x, y, w, 2), AccentDim); // Top border
    }
}
