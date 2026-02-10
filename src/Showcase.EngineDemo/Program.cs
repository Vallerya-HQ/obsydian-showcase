using Obsydian.Core;
using Obsydian.Core.Math;
using Obsydian.Core.Scenes;
using Obsydian.Graphics;
using Obsydian.Graphics.Tilemap;
using Obsydian.Input;
using Obsydian.Physics;
using Obsydian.Platform.Desktop.Rendering;
using Obsydian.UI.Widgets;
using Showcase.Common;
using Silk.NET.OpenGL;
using Texture = Obsydian.Graphics.Texture;
using GL = Silk.NET.OpenGL.GL;

var app = new EngineDemoApp();
app.Run();

// ─── The App ───────────────────────────────────────────────────────────────────

sealed class EngineDemoApp : ShowcaseApp
{
    private SceneManager _scenes = null!;

    public EngineDemoApp() : base(new EngineConfig
    {
        Title = "Obsydian Engine Demo",
        WindowWidth = 1280,
        WindowHeight = 720,
        Version = "0.2.0"
    })
    { }

    protected override Color ClearColor => new(20, 20, 30);

    protected override void OnLoad()
    {
        _scenes = new SceneManager();
        _scenes.Push(new TitleScene(this));
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

// ─── Title Screen Scene ────────────────────────────────────────────────────────

sealed class TitleScene : IScene
{
    private readonly EngineDemoApp _app;
    private float _blinkTimer;
    private bool _showPrompt = true;

    public TitleScene(EngineDemoApp app) => _app = app;

    public void Enter() { }
    public void Exit() { }

    public void Update(float dt)
    {
        _blinkTimer += dt;
        if (_blinkTimer >= 0.6f)
        {
            _blinkTimer -= 0.6f;
            _showPrompt = !_showPrompt;
        }

        if (_app.Input.IsKeyPressed(Key.Enter) || _app.Input.IsKeyPressed(Key.Space))
        {
            _app.Scenes.Switch(new GameplayScene(_app));
        }

        if (_app.Input.IsKeyPressed(Key.Escape))
            _app.Window.Close();
    }

    public void Render(float dt)
    {
        var r = _app.Renderer;

        // Title
        r.DrawText("OBSYDIAN ENGINE", new Vec2(380, 180), Color.White, 5f);
        r.DrawText("v0.2.0", new Vec2(590, 240), new Color(150, 150, 150), 2f);

        // Feature list
        var features = new[]
        {
            "Tilemap rendering with collision",
            "Camera2D with smooth follow",
            "Scene management (title / gameplay / pause)",
            "Text rendering (built-in 5x7 pixel font)",
            "UI widgets (panel, label, progress bar)",
            "Tile-based collision response",
            "ECS architecture"
        };

        float y = 320;
        foreach (var f in features)
        {
            r.DrawText($"  * {f}", new Vec2(320, y), new Color(180, 200, 255), 2f);
            y += 22;
        }

        // Prompt
        if (_showPrompt)
            r.DrawText("Press ENTER to start", new Vec2(460, 600), new Color(255, 255, 100), 3f);
    }
}

// ─── Gameplay Scene ────────────────────────────────────────────────────────────

sealed class GameplayScene : IScene
{
    private readonly EngineDemoApp _app;

    // World
    private Tilemap? _tilemap;
    private Texture? _tilesetTexture;
    private Camera2D _camera = null!;

    // Player
    private Vec2 _playerPos;
    private Vec2 _playerSize = new(14, 14);
    private float _playerSpeed = 150f;
    private int _playerFacing; // 0=down, 1=up, 2=left, 3=right
    private float _animTimer;

    // NPC
    private Vec2 _npcPos;
    private bool _showDialogue;
    private float _dialogueTimer;

    // UI
    private float _health = 1f;
    private int _coins;
    private float _playTime;

    // Pause overlay
    private bool _paused;

    public GameplayScene(EngineDemoApp app) => _app = app;

    public void Enter()
    {
        _tilesetTexture = CreateTilesetTexture(_app.Renderer.Gl);
        _tilemap = BuildMap(_tilesetTexture);

        _camera = new Camera2D(1280, 720)
        {
            FollowSmoothing = 8f
        };

        // Spawn player in center-ish of the map
        _playerPos = new Vec2(10 * 16, 8 * 16);
        _npcPos = new Vec2(18 * 16, 6 * 16);

        _camera.LookAt(_playerPos);
    }

    public void Exit()
    {
        _tilesetTexture?.Dispose();
    }

    public void Update(float dt)
    {
        if (_app.Input.IsKeyPressed(Key.Escape))
        {
            if (_paused)
                _paused = false;
            else
                _paused = true;
            return;
        }

        if (_paused)
        {
            if (_app.Input.IsKeyPressed(Key.Q))
                _app.Scenes.Switch(new TitleScene(_app));
            return;
        }

        _playTime += dt;

        // Player movement
        var dir = Vec2.Zero;
        if (_app.Input.IsKeyDown(Key.W) || _app.Input.IsKeyDown(Key.Up)) dir += Vec2.Up;
        if (_app.Input.IsKeyDown(Key.S) || _app.Input.IsKeyDown(Key.Down)) dir += Vec2.Down;
        if (_app.Input.IsKeyDown(Key.A) || _app.Input.IsKeyDown(Key.Left)) dir += Vec2.Left;
        if (_app.Input.IsKeyDown(Key.D) || _app.Input.IsKeyDown(Key.Right)) dir += Vec2.Right;

        if (dir.LengthSquared > 0)
        {
            dir = dir.Normalized;
            var velocity = dir * _playerSpeed * dt;

            // Update facing direction
            if (MathF.Abs(dir.X) > MathF.Abs(dir.Y))
                _playerFacing = dir.X < 0 ? 2 : 3;
            else
                _playerFacing = dir.Y < 0 ? 1 : 0;

            // Collision with tilemap
            _playerPos = TileCollision.MoveAndSlide(
                _playerPos, _playerSize, velocity,
                IsSolidTile, 16, 16);

            _animTimer += dt;
        }

        // NPC interaction
        var distToNpc = Vec2.Distance(_playerPos, _npcPos);
        if (distToNpc < 24 && _app.Input.IsKeyPressed(Key.E))
        {
            _showDialogue = !_showDialogue;
            _dialogueTimer = 0;
        }
        if (_showDialogue)
        {
            _dialogueTimer += dt;
            if (_dialogueTimer > 4f)
                _showDialogue = false;
        }

        // Coin collection (simple — collect when walking near specific tiles)
        int tx = (int)(_playerPos.X / 16);
        int ty = (int)(_playerPos.Y / 16);
        var decorLayer = _tilemap?.GetLayer("decor");
        if (decorLayer != null && decorLayer.InBounds(tx, ty))
        {
            var tile = decorLayer[tx, ty];
            if (tile.TileId == 7) // coin tile
            {
                decorLayer[tx, ty] = new Tile(0);
                _coins++;
            }
        }

        // Camera follow
        _camera.Follow(_playerPos + _playerSize / 2, dt);
        _camera.ClampToWorld(_tilemap!.WorldWidth, _tilemap.WorldHeight);
    }

    public void Render(float dt)
    {
        var r = _app.Renderer;

        // Set camera for world rendering
        r.SetCamera(_camera.Position, _camera.Zoom);

        // Draw tilemap
        TilemapRenderer.Draw(r, _tilemap!, _camera);

        // Draw NPC (blue square)
        r.DrawRect(new Rect(_npcPos.X, _npcPos.Y, 16, 16), new Color(80, 120, 255));
        r.DrawRect(new Rect(_npcPos.X + 2, _npcPos.Y + 2, 12, 12), new Color(120, 160, 255));

        // NPC interaction indicator
        var distToNpc = Vec2.Distance(_playerPos, _npcPos);
        if (distToNpc < 24 && !_showDialogue)
        {
            r.DrawText("E", new Vec2(_npcPos.X + 4, _npcPos.Y - 14), Color.White, 2f);
        }

        // Draw player (green square with facing indicator)
        var playerColor = new Color(80, 200, 80);
        r.DrawRect(new Rect(_playerPos.X, _playerPos.Y, _playerSize.X, _playerSize.Y), playerColor);

        // Facing indicator (small white dot showing direction)
        var eyeOffset = _playerFacing switch
        {
            0 => new Vec2(5, 10), // down
            1 => new Vec2(5, 2),  // up
            2 => new Vec2(1, 5),  // left
            3 => new Vec2(10, 5), // right
            _ => Vec2.Zero
        };
        r.DrawRect(new Rect(_playerPos.X + eyeOffset.X, _playerPos.Y + eyeOffset.Y, 3, 3), Color.White);

        // ─── HUD (screen-space, reset camera) ─────────────────────────────
        r.SetCamera(Vec2.Zero);

        // Health bar
        r.DrawText("HP", new Vec2(10, 10), Color.White, 2f);
        r.DrawRect(new Rect(40, 10, 102, 14), new Color(40, 40, 40));
        var hpWidth = 100 * _health;
        if (hpWidth > 0)
            r.DrawRect(new Rect(41, 11, hpWidth, 12), _health > 0.3f ? Color.Green : Color.Red);
        r.DrawRect(new Rect(40, 10, 102, 14), Color.White, filled: false);

        // Coins
        r.DrawText($"Coins: {_coins}", new Vec2(160, 10), new Color(255, 220, 50), 2f);

        // Time
        var mins = (int)(_playTime / 60);
        var secs = (int)(_playTime % 60);
        r.DrawText($"Time: {mins}:{secs:D2}", new Vec2(300, 10), new Color(180, 180, 180), 2f);

        // Position debug
        r.DrawText($"Pos: ({_playerPos.X:F0}, {_playerPos.Y:F0})", new Vec2(10, 700), new Color(120, 120, 120), 2f);
        r.DrawText($"FPS: {_app.Engine.Time.Fps:F0}", new Vec2(1180, 10), new Color(120, 120, 120), 2f);

        // Controls help
        r.DrawText("WASD=Move  E=Talk  ESC=Pause", new Vec2(420, 700), new Color(100, 100, 100), 2f);

        // Dialogue box
        if (_showDialogue)
        {
            r.DrawRect(new Rect(240, 500, 800, 100), new Color(10, 10, 30, 220));
            r.DrawRect(new Rect(240, 500, 800, 100), new Color(100, 100, 200), filled: false);
            r.DrawText("Elder NPC", new Vec2(260, 510), new Color(100, 180, 255), 2f);
            r.DrawText("Welcome to the Obsydian demo world!", new Vec2(260, 540), Color.White, 2f);
            r.DrawText("Collect the yellow coins scattered around.", new Vec2(260, 565), new Color(200, 200, 200), 2f);
        }

        // Pause overlay
        if (_paused)
        {
            r.DrawRect(new Rect(0, 0, 1280, 720), new Color(0, 0, 0, 150));
            r.DrawText("PAUSED", new Vec2(520, 280), Color.White, 5f);
            r.DrawText("Press ESC to resume", new Vec2(470, 380), new Color(200, 200, 200), 3f);
            r.DrawText("Press Q to quit to title", new Vec2(440, 430), new Color(200, 200, 200), 3f);
        }
    }

    private bool IsSolidTile(int x, int y)
    {
        if (_tilemap == null) return false;
        foreach (var layer in _tilemap.Layers)
        {
            var tile = layer.GetTileSafe(x, y);
            if (tile.Solid) return true;
        }
        return false;
    }

    // ─── Procedural tileset texture (no asset files needed) ────────────────────

    private static Texture CreateTilesetTexture(GL gl)
    {
        // 8 tiles, each 16x16, laid out horizontally: 128x16 texture
        // Tile 1: grass (green)
        // Tile 2: wall (dark gray)
        // Tile 3: water (blue)
        // Tile 4: sand/path (tan)
        // Tile 5: tree trunk (brown)
        // Tile 6: tree top (dark green)
        // Tile 7: coin (yellow)
        // Tile 8: flower (pink on green)
        int tw = 16, th = 16, cols = 8;
        var pixels = new byte[cols * tw * th * 4];

        void SetPixel(int tileIdx, int x, int y, byte r, byte g, byte b, byte a = 255)
        {
            int px = tileIdx * tw + x;
            int py = y;
            int idx = (py * cols * tw + px) * 4;
            if (idx >= 0 && idx + 3 < pixels.Length)
            {
                pixels[idx] = r; pixels[idx + 1] = g; pixels[idx + 2] = b; pixels[idx + 3] = a;
            }
        }

        void FillTile(int tileIdx, byte r, byte g, byte b)
        {
            for (int y = 0; y < th; y++)
            for (int x = 0; x < tw; x++)
                SetPixel(tileIdx, x, y, r, g, b);
        }

        // Tile 1: Grass
        FillTile(0, 60, 140, 50);
        // Add grass detail
        for (int i = 0; i < 12; i++)
        {
            int gx = (i * 7 + 3) % 16;
            int gy = (i * 5 + 2) % 16;
            SetPixel(0, gx, gy, 80, 170, 60);
        }

        // Tile 2: Wall
        FillTile(1, 80, 80, 90);
        // Brick pattern
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 16; x++)
        {
            if (y % 4 == 0 || (x + (y / 4 % 2) * 4) % 8 == 0)
                SetPixel(1, x, y, 60, 60, 70);
        }

        // Tile 3: Water
        FillTile(2, 40, 80, 180);
        for (int i = 0; i < 8; i++)
        {
            int wx = (i * 5 + 1) % 14 + 1;
            int wy = (i * 3 + 4) % 14 + 1;
            SetPixel(2, wx, wy, 60, 110, 210);
            SetPixel(2, wx + 1, wy, 60, 110, 210);
        }

        // Tile 4: Sand/path
        FillTile(3, 200, 180, 130);
        for (int i = 0; i < 10; i++)
        {
            int sx = (i * 7 + 5) % 16;
            int sy = (i * 3 + 1) % 16;
            SetPixel(3, sx, sy, 180, 160, 110);
        }

        // Tile 5: Tree trunk
        FillTile(4, 60, 140, 50); // grass background
        for (int y = 6; y < 16; y++)
        for (int x = 6; x < 10; x++)
            SetPixel(4, x, y, 100, 60, 30);

        // Tile 6: Tree top (leafy)
        FillTile(5, 60, 140, 50); // grass background
        for (int y = 0; y < 12; y++)
        for (int x = 2; x < 14; x++)
        {
            int cx = 8, cy = 5;
            if ((x - cx) * (x - cx) + (y - cy) * (y - cy) < 30)
                SetPixel(5, x, y, 30, 100, 25);
        }

        // Tile 7: Coin
        FillTile(6, 60, 140, 50); // grass background
        for (int y = 4; y < 12; y++)
        for (int x = 4; x < 12; x++)
        {
            int cx = 8, cy = 8;
            if ((x - cx) * (x - cx) + (y - cy) * (y - cy) < 14)
                SetPixel(6, x, y, 255, 220, 50);
        }
        // coin highlight
        SetPixel(6, 6, 6, 255, 250, 150);
        SetPixel(6, 7, 6, 255, 250, 150);

        // Tile 8: Flower
        FillTile(7, 60, 140, 50); // grass background
        SetPixel(7, 8, 9, 40, 120, 30); // stem
        SetPixel(7, 8, 8, 40, 120, 30);
        SetPixel(7, 8, 7, 255, 100, 150); // center
        SetPixel(7, 7, 6, 255, 130, 170);
        SetPixel(7, 9, 6, 255, 130, 170);
        SetPixel(7, 8, 5, 255, 130, 170);
        SetPixel(7, 7, 8, 255, 130, 170);
        SetPixel(7, 9, 8, 255, 130, 170);

        return GlTexture.Create(gl, cols * tw, th, pixels, "__demo_tileset");
    }

    private static Tilemap BuildMap(Texture tileset)
    {
        int mapW = 30, mapH = 20;
        var map = new Tilemap(tileset, 16, 16, mapW, mapH);

        // Ground layer — fill with grass
        var ground = map.AddLayer("ground");
        ground.Fill(new Tile(1));

        // Add paths
        for (int x = 5; x < 25; x++)
            ground.SetTile(x, 10, new Tile(4));
        for (int y = 4; y < 16; y++)
            ground.SetTile(15, y, new Tile(4));

        // Water area (bottom-right)
        for (int y = 14; y < 18; y++)
        for (int x = 22; x < 28; x++)
            ground.SetTile(x, y, new Tile(3, Solid: true));

        // Walls layer — border walls
        var walls = map.AddLayer("walls");

        // Border walls
        for (int x = 0; x < mapW; x++)
        {
            walls.SetTile(x, 0, new Tile(2, Solid: true));
            walls.SetTile(x, mapH - 1, new Tile(2, Solid: true));
        }
        for (int y = 0; y < mapH; y++)
        {
            walls.SetTile(0, y, new Tile(2, Solid: true));
            walls.SetTile(mapW - 1, y, new Tile(2, Solid: true));
        }

        // Some interior walls
        for (int x = 5; x < 10; x++)
            walls.SetTile(x, 5, new Tile(2, Solid: true));
        for (int y = 5; y < 9; y++)
            walls.SetTile(5, y, new Tile(2, Solid: true));

        // Decoration layer — trees, flowers, coins
        var decor = map.AddLayer("decor");

        // Trees (trunk + top)
        int[,] treePositions = { { 3, 3 }, { 12, 3 }, { 20, 4 }, { 25, 8 }, { 7, 14 }, { 3, 17 }, { 22, 2 } };
        for (int i = 0; i < treePositions.GetLength(0); i++)
        {
            int tx = treePositions[i, 0], ty = treePositions[i, 1];
            decor.SetTile(tx, ty, new Tile(6, Solid: true)); // tree top
            decor.SetTile(tx, ty + 1, new Tile(5, Solid: true)); // trunk
        }

        // Flowers
        decor.SetTile(8, 8, new Tile(8));
        decor.SetTile(13, 12, new Tile(8));
        decor.SetTile(17, 7, new Tile(8));
        decor.SetTile(24, 11, new Tile(8));

        // Coins
        decor.SetTile(10, 10, new Tile(7));
        decor.SetTile(15, 6, new Tile(7));
        decor.SetTile(20, 10, new Tile(7));
        decor.SetTile(15, 14, new Tile(7));
        decor.SetTile(8, 12, new Tile(7));
        decor.SetTile(25, 6, new Tile(7));
        decor.SetTile(12, 16, new Tile(7));
        decor.SetTile(18, 3, new Tile(7));

        return map;
    }
}
