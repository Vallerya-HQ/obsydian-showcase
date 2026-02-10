using Obsydian.Core;
using Obsydian.Core.Math;
using Obsydian.Core.Scenes;
using Obsydian.Core.Time;
using Obsydian.Graphics;
using Obsydian.Graphics.Particles;
using Obsydian.Graphics.Tilemap;
using Obsydian.Input;
using Obsydian.Physics;
using Obsydian.Platform.Desktop.Rendering;
using Obsydian.UI;
using Obsydian.UI.Dialogue;
using Obsydian.UI.Widgets;
using Showcase.Common;
using Texture = Obsydian.Graphics.Texture;

// Generate test audio files before the window opens
TestSoundGenerator.EnsureTestSounds(AppDomain.CurrentDomain.BaseDirectory);

var app = new FullDemoApp();
app.Run();

// ─── The App ───────────────────────────────────────────────────────────────────

sealed class FullDemoApp : ShowcaseApp
{
    private SceneManager _scenes = null!;

    public FullDemoApp() : base(new EngineConfig
    {
        Title = "Obsydian Full Demo — All Systems",
        WindowWidth = 1280,
        WindowHeight = 720,
        Version = "0.3.0"
    })
    { }

    protected override Color ClearColor => new(15, 15, 25);

    protected override void OnLoad()
    {
        _scenes = new SceneManager();
        _scenes.Push(new DemoTitleScene(this));
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

// ─── Title Scene with Particles ──────────────────────────────────────────────

sealed class DemoTitleScene : IScene
{
    private readonly FullDemoApp _app;
    private ScreenTransition _transition = null!;
    private ParticleEmitter _sparkles = null!;
    private float _blinkTimer;
    private bool _showPrompt = true;

    public DemoTitleScene(FullDemoApp app) => _app = app;

    public void Enter()
    {
        _transition = new ScreenTransition { FadeInDuration = 0.5f };
        _transition.FadeIn();

        _sparkles = new ParticleEmitter(new ParticleConfig
        {
            EmitRate = 15f,
            LifeMin = 1f, LifeMax = 3f,
            SpeedMin = 10f, SpeedMax = 40f,
            AngleMin = 220f, AngleMax = 320f,
            ScaleStart = 1.5f, ScaleEnd = 0f,
            ColorStart = Color.Gold,
            ColorEnd = new Color(255, 100, 0, 0),
            EmitRadius = 200f,
            Gravity = new Vec2(0, -5f)
        });
        _sparkles.Position = new Vec2(640, 500);
    }

    public void Exit() { }

    public void Update(float dt)
    {
        _transition.Update(dt);
        _sparkles.Update(dt);

        _blinkTimer += dt;
        if (_blinkTimer >= 0.6f) { _blinkTimer -= 0.6f; _showPrompt = !_showPrompt; }

        if (_transition.IsActive) return;

        if (_app.Input.IsKeyPressed(Key.Enter) || _app.Input.IsKeyPressed(Key.Space))
        {
            _transition.Start(
                onMidpoint: () => _app.Scenes.Switch(new GameplayDemoScene(_app)));
        }

        if (_app.Input.IsKeyPressed(Key.Escape))
            _app.Window.Close();
    }

    public void Render(float dt)
    {
        var r = _app.Renderer;

        // Particles behind title
        _sparkles.Draw(r);

        r.DrawText("OBSYDIAN ENGINE", new Vec2(360, 160), Color.White, 5f);
        r.DrawText("v0.3.0 — Full Systems Demo", new Vec2(430, 230), new Color(100, 255, 100), 2.5f);

        var features = new[]
        {
            "Particle System",
            "Dialogue Tree + Typewriter",
            "Screen Transitions (Fade)",
            "Game Clock / Time of Day",
            "Render Layer Sorting",
            "Debug Overlay (F3)",
            "Gamepad Support",
            "Save Migration Chain"
        };

        float y = 300;
        foreach (var f in features)
        {
            r.DrawText($"  * {f}", new Vec2(400, y), new Color(180, 200, 255), 2f);
            y += 22;
        }

        if (_showPrompt)
            r.DrawText("Press ENTER to start", new Vec2(440, 600), new Color(255, 255, 100), 3f);

        _transition.Draw(r, 1280, 720);
    }
}

// ─── Gameplay Demo Scene ─────────────────────────────────────────────────────

sealed class GameplayDemoScene : IScene
{
    private readonly FullDemoApp _app;

    // World
    private Tilemap? _tilemap;
    private Texture? _tilesetTexture;
    private Camera2D _camera = null!;
    private RenderLayerManager _layers = null!;

    // Player
    private Vec2 _playerPos;
    private Vec2 _playerSize = new(14, 14);
    private float _playerSpeed = 150f;
    private int _playerFacing;

    // NPC + Dialogue
    private Vec2 _npcPos;
    private DialogueBox _dialogueBox = null!;
    private DialogueTree _npcDialogue = null!;

    // Systems
    private GameClock _clock = null!;
    private DebugOverlay _debug = null!;
    private ScreenTransition _transition = null!;
    private ParticleEmitter _torchParticles = null!;

    // UI
    private float _health = 1f;
    private int _coins;
    private bool _paused;

    public GameplayDemoScene(FullDemoApp app) => _app = app;

    public void Enter()
    {
        _tilesetTexture = CreateTilesetTexture(_app.Renderer.Gl);
        _tilemap = BuildMap(_tilesetTexture);

        _camera = new Camera2D(1280, 720) { Zoom = 3f, FollowSmoothing = 8f };
        _layers = new RenderLayerManager();

        _playerPos = new Vec2(10 * 16, 8 * 16);
        _npcPos = new Vec2(18 * 16, 6 * 16);
        _camera.LookAt(_playerPos);

        // Game clock — starts at 8 AM
        _clock = new GameClock { TimeScale = 15f };
        _clock.SetTime(8, 0);

        // Debug overlay
        _debug = new DebugOverlay();
        _debug.AddDynamic(() => $"Clock: {_clock.TimeString}");
        _debug.AddDynamic(() => $"Phase: {_clock.Phase}");
        _debug.AddDynamic(() => $"Coins: {_coins}");
        _debug.AddDynamic(() => $"Particles: {_torchParticles.ActiveCount}");
        _debug.AddDynamic(() => $"Draw Cmds: {_layers.CommandCount}");

        // Screen transition (fade in)
        _transition = new ScreenTransition { FadeInDuration = 0.4f };
        _transition.FadeIn();

        // Torch particles
        _torchParticles = new ParticleEmitter(new ParticleConfig
        {
            EmitRate = 20f,
            LifeMin = 0.3f, LifeMax = 0.8f,
            SpeedMin = 15f, SpeedMax = 35f,
            AngleMin = 250f, AngleMax = 290f,
            ScaleStart = 1.2f, ScaleEnd = 0f,
            ColorStart = new Color(255, 160, 40),
            ColorEnd = new Color(255, 60, 0, 0),
            EmitRadius = 2f
        });
        _torchParticles.Position = new Vec2(5 * 16 + 8, 12 * 16);

        // Start background music
        _app.Audio.PlayMusic("sfx/music.wav", volume: 0.4f);

        // Dialogue
        _dialogueBox = new DialogueBox
        {
            Bounds = new Rect(200, 520, 880, 140),
            TextScale = 2f,
            Visible = false
        };
        _dialogueBox.Runner.OnDialogueEnded += () => _dialogueBox.Visible = false;

        _npcDialogue = BuildDialogueTree();
    }

    public void Exit()
    {
        _tilesetTexture?.Dispose();
    }

    public void Update(float dt)
    {
        _transition.Update(dt);
        _debug.Update(_app.Input);
        _clock.Update(dt);
        _torchParticles.Update(dt);

        // Dialogue box handles its own input when visible
        if (_dialogueBox.Visible)
        {
            _dialogueBox.Update(dt, _app.Input);
            return;
        }

        if (_app.Input.IsKeyPressed(Key.Escape))
        {
            _paused = !_paused;
            return;
        }

        if (_paused)
        {
            if (_app.Input.IsKeyPressed(Key.Q))
            {
                _transition.Start(
                    onMidpoint: () => _app.Scenes.Switch(new DemoTitleScene(_app)));
            }
            return;
        }

        // Player movement (keyboard + gamepad)
        var dir = Vec2.Zero;
        if (_app.Input.IsKeyDown(Key.W) || _app.Input.IsKeyDown(Key.Up)) dir += Vec2.Up;
        if (_app.Input.IsKeyDown(Key.S) || _app.Input.IsKeyDown(Key.Down)) dir += Vec2.Down;
        if (_app.Input.IsKeyDown(Key.A) || _app.Input.IsKeyDown(Key.Left)) dir += Vec2.Left;
        if (_app.Input.IsKeyDown(Key.D) || _app.Input.IsKeyDown(Key.Right)) dir += Vec2.Right;

        // Gamepad left stick
        var stick = _app.Input.Gamepad.LeftStick;
        if (stick.LengthSquared > 0.01f)
            dir += stick;

        if (dir.LengthSquared > 0)
        {
            dir = dir.Normalized;
            var velocity = dir * _playerSpeed * dt;

            if (MathF.Abs(dir.X) > MathF.Abs(dir.Y))
                _playerFacing = dir.X < 0 ? 2 : 3;
            else
                _playerFacing = dir.Y < 0 ? 1 : 0;

            _playerPos = TileCollision.MoveAndSlide(
                _playerPos, _playerSize, velocity,
                IsSolidTile, 16, 16);
        }

        // NPC interaction
        var distToNpc = Vec2.Distance(_playerPos, _npcPos);
        bool interact = _app.Input.IsKeyPressed(Key.E) || _app.Input.Gamepad.IsButtonPressed(GamepadButton.A);
        if (distToNpc < 24 && interact)
        {
            _app.Audio.PlaySound("sfx/interact.wav", volume: 0.5f);
            _dialogueBox.StartDialogue(_npcDialogue);
        }

        // Coin collection
        int tx = (int)(_playerPos.X / 16);
        int ty = (int)(_playerPos.Y / 16);
        var decorLayer = _tilemap?.GetLayer("decor");
        if (decorLayer != null && decorLayer.InBounds(tx, ty))
        {
            var tile = decorLayer[tx, ty];
            if (tile.TileId == 7)
            {
                decorLayer[tx, ty] = new Tile(0);
                _coins++;
                if (_health < 1f) _health = System.Math.Min(1f, _health + 0.1f);
                _app.Audio.PlaySound("sfx/coin.wav", volume: 0.6f, pitch: 0.1f);
            }
        }

        _camera.Follow(_playerPos + _playerSize / 2, dt);
        _camera.ClampToWorld(_tilemap!.WorldWidth, _tilemap.WorldHeight);
    }

    public void Render(float dt)
    {
        var r = _app.Renderer;

        // Time-of-day tint
        var dayTint = GetDayTint();

        // World rendering with camera
        r.SetCamera(_camera.Position, _camera.Zoom);

        // Draw tilemap
        TilemapRenderer.Draw(r, _tilemap!, _camera);

        // Torch particles (world space)
        _torchParticles.Draw(r);

        // NPC
        r.DrawRect(new Rect(_npcPos.X, _npcPos.Y, 16, 16), new Color(80, 120, 255));
        r.DrawRect(new Rect(_npcPos.X + 2, _npcPos.Y + 2, 12, 12), new Color(120, 160, 255));

        var distToNpc = Vec2.Distance(_playerPos, _npcPos);
        if (distToNpc < 24 && !_dialogueBox.Visible)
            r.DrawText("E", new Vec2(_npcPos.X + 4, _npcPos.Y - 14), Color.White, 2f);

        // Player
        var playerColor = new Color(80, 200, 80);
        r.DrawRect(new Rect(_playerPos.X, _playerPos.Y, _playerSize.X, _playerSize.Y), playerColor);

        var eyeOffset = _playerFacing switch
        {
            0 => new Vec2(5, 10),
            1 => new Vec2(5, 2),
            2 => new Vec2(1, 5),
            3 => new Vec2(10, 5),
            _ => Vec2.Zero
        };
        r.DrawRect(new Rect(_playerPos.X + eyeOffset.X, _playerPos.Y + eyeOffset.Y, 3, 3), Color.White);

        // Day/night overlay (world space)
        if (dayTint.A > 0)
        {
            float worldW = _tilemap!.WorldWidth;
            float worldH = _tilemap.WorldHeight;
            r.DrawRect(new Rect(0, 0, worldW, worldH), dayTint);
        }

        // ─── HUD (screen-space) ──────────────────────────────────────────────
        r.SetCamera(Vec2.Zero);

        // Clock display
        r.DrawText(_clock.TimeString, new Vec2(1160, 10), Color.Gold, 2.5f);
        r.DrawText($"{_clock.Phase}", new Vec2(1130, 35), new Color(180, 180, 180), 1.5f);

        // Health bar
        r.DrawText("HP", new Vec2(10, 10), Color.White, 2f);
        r.DrawRect(new Rect(40, 10, 102, 14), new Color(40, 40, 40));
        var hpWidth = 100 * _health;
        if (hpWidth > 0)
            r.DrawRect(new Rect(41, 11, hpWidth, 12), _health > 0.3f ? Color.Green : Color.Red);
        r.DrawRect(new Rect(40, 10, 102, 14), Color.White, filled: false);

        // Coins
        r.DrawText($"Coins: {_coins}", new Vec2(160, 10), Color.Gold, 2f);

        // Controls
        r.DrawText("WASD=Move  E=Talk  ESC=Pause  F3=Debug", new Vec2(350, 700), new Color(100, 100, 100), 2f);

        // Dialogue box (screen space)
        _dialogueBox.Draw(r);

        // Pause overlay
        if (_paused)
        {
            r.DrawRect(new Rect(0, 0, 1280, 720), Color.Black.WithAlpha(150));
            r.DrawText("PAUSED", new Vec2(520, 280), Color.Gold, 5f);
            r.DrawText("Press ESC to resume", new Vec2(470, 380), Color.White.WithAlpha(200), 3f);
            r.DrawText("Press Q to quit to title", new Vec2(440, 430), Color.White.WithAlpha(200), 3f);
        }

        // Debug overlay (on top of everything)
        _debug.Draw(r, _app.Engine.Time);

        // Screen transition (very last)
        _transition.Draw(r, 1280, 720);
    }

    private Color GetDayTint() => _clock.Phase switch
    {
        DayPhase.Dawn => new Color(100, 80, 50, 40),
        DayPhase.Morning => Color.Transparent,
        DayPhase.Afternoon => Color.Transparent,
        DayPhase.Evening => new Color(80, 50, 30, 60),
        DayPhase.Night => new Color(20, 20, 60, 120),
        _ => Color.Transparent
    };

    private bool IsSolidTile(int x, int y)
    {
        if (_tilemap == null) return false;
        foreach (var layer in _tilemap.Layers)
        {
            if (layer.GetTileSafe(x, y).Solid) return true;
        }
        return false;
    }

    private static DialogueTree BuildDialogueTree()
    {
        var tree = new DialogueTree { Id = "npc_elder", StartNodeId = "greet" };

        tree.Nodes["greet"] = new DialogueNode
        {
            Id = "greet", Speaker = "Elder",
            Text = "Welcome, traveler! This land is a demonstration of the Obsydian engine.",
            Choices =
            [
                new DialogueChoice { Text = "Tell me about the engine.", TargetNodeId = "about" },
                new DialogueChoice { Text = "Any tips?", TargetNodeId = "tips" },
                new DialogueChoice { Text = "Goodbye.", TargetNodeId = "bye" }
            ]
        };

        tree.Nodes["about"] = new DialogueNode
        {
            Id = "about", Speaker = "Elder",
            Text = "This engine uses ECS architecture, modular design, and no submodules. Each system is a separate library!",
            NextNodeId = "greet"
        };

        tree.Nodes["tips"] = new DialogueNode
        {
            Id = "tips", Speaker = "Elder",
            Text = "Collect the yellow coins for health. Press F3 to see the debug overlay. Watch the time of day change!",
            NextNodeId = "greet"
        };

        tree.Nodes["bye"] = new DialogueNode
        {
            Id = "bye", Speaker = "Elder",
            Text = "Safe travels! May the particles guide your way."
        };

        return tree;
    }

    // ─── Procedural tileset (same as EngineDemo) ─────────────────────────────

    private static Texture CreateTilesetTexture(Silk.NET.OpenGL.GL gl)
    {
        int tw = 16, th = 16, cols = 8;
        var pixels = new byte[cols * tw * th * 4];

        void SetPixel(int tileIdx, int x, int y, byte r, byte g, byte b, byte a = 255)
        {
            int px = tileIdx * tw + x;
            int idx = (y * cols * tw + px) * 4;
            if (idx >= 0 && idx + 3 < pixels.Length)
            { pixels[idx] = r; pixels[idx + 1] = g; pixels[idx + 2] = b; pixels[idx + 3] = a; }
        }

        void FillTile(int tileIdx, byte r, byte g, byte b)
        {
            for (int y = 0; y < th; y++)
            for (int x = 0; x < tw; x++)
                SetPixel(tileIdx, x, y, r, g, b);
        }

        FillTile(0, 60, 140, 50); // grass
        for (int i = 0; i < 12; i++)
            SetPixel(0, (i * 7 + 3) % 16, (i * 5 + 2) % 16, 80, 170, 60);

        FillTile(1, 80, 80, 90); // wall
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 16; x++)
            if (y % 4 == 0 || (x + (y / 4 % 2) * 4) % 8 == 0)
                SetPixel(1, x, y, 60, 60, 70);

        FillTile(2, 40, 80, 180); // water
        for (int i = 0; i < 8; i++)
        { SetPixel(2, (i * 5 + 1) % 14 + 1, (i * 3 + 4) % 14 + 1, 60, 110, 210); }

        FillTile(3, 200, 180, 130); // sand

        FillTile(4, 60, 140, 50); // tree trunk on grass
        for (int y = 6; y < 16; y++) for (int x = 6; x < 10; x++) SetPixel(4, x, y, 100, 60, 30);

        FillTile(5, 60, 140, 50); // tree top
        for (int y = 0; y < 12; y++) for (int x = 2; x < 14; x++)
            if ((x - 8) * (x - 8) + (y - 5) * (y - 5) < 30)
                SetPixel(5, x, y, 30, 100, 25);

        FillTile(6, 60, 140, 50); // coin
        for (int y = 4; y < 12; y++) for (int x = 4; x < 12; x++)
            if ((x - 8) * (x - 8) + (y - 8) * (y - 8) < 14)
                SetPixel(6, x, y, 255, 220, 50);
        SetPixel(6, 6, 6, 255, 250, 150); SetPixel(6, 7, 6, 255, 250, 150);

        FillTile(7, 60, 140, 50); // flower
        SetPixel(7, 8, 9, 40, 120, 30); SetPixel(7, 8, 8, 40, 120, 30);
        SetPixel(7, 8, 7, 255, 100, 150);
        SetPixel(7, 7, 6, 255, 130, 170); SetPixel(7, 9, 6, 255, 130, 170);
        SetPixel(7, 8, 5, 255, 130, 170); SetPixel(7, 7, 8, 255, 130, 170);
        SetPixel(7, 9, 8, 255, 130, 170);

        return GlTexture.Create(gl, cols * tw, th, pixels, "__demo_tileset");
    }

    private static Tilemap BuildMap(Texture tileset)
    {
        int mapW = 30, mapH = 20;
        var map = new Tilemap(tileset, 16, 16, mapW, mapH);

        var ground = map.AddLayer("ground");
        ground.Fill(new Tile(1));
        for (int x = 5; x < 25; x++) ground.SetTile(x, 10, new Tile(4));
        for (int y = 4; y < 16; y++) ground.SetTile(15, y, new Tile(4));

        for (int y = 14; y < 18; y++)
        for (int x = 22; x < 28; x++)
            ground.SetTile(x, y, new Tile(3, Solid: true));

        var walls = map.AddLayer("walls");
        for (int x = 0; x < mapW; x++) { walls.SetTile(x, 0, new Tile(2, Solid: true)); walls.SetTile(x, mapH - 1, new Tile(2, Solid: true)); }
        for (int y = 0; y < mapH; y++) { walls.SetTile(0, y, new Tile(2, Solid: true)); walls.SetTile(mapW - 1, y, new Tile(2, Solid: true)); }
        for (int x = 5; x < 10; x++) walls.SetTile(x, 5, new Tile(2, Solid: true));
        for (int y = 5; y < 9; y++) walls.SetTile(5, y, new Tile(2, Solid: true));

        var decor = map.AddLayer("decor");
        int[,] trees = { { 3, 3 }, { 12, 3 }, { 20, 4 }, { 25, 8 }, { 7, 14 }, { 3, 17 }, { 22, 2 } };
        for (int i = 0; i < trees.GetLength(0); i++)
        {
            decor.SetTile(trees[i, 0], trees[i, 1], new Tile(6, Solid: true));
            decor.SetTile(trees[i, 0], trees[i, 1] + 1, new Tile(5, Solid: true));
        }

        decor.SetTile(8, 8, new Tile(8)); decor.SetTile(13, 12, new Tile(8));
        decor.SetTile(17, 7, new Tile(8)); decor.SetTile(24, 11, new Tile(8));

        decor.SetTile(10, 10, new Tile(7)); decor.SetTile(15, 6, new Tile(7));
        decor.SetTile(20, 10, new Tile(7)); decor.SetTile(15, 14, new Tile(7));
        decor.SetTile(8, 12, new Tile(7)); decor.SetTile(25, 6, new Tile(7));
        decor.SetTile(12, 16, new Tile(7)); decor.SetTile(18, 3, new Tile(7));

        return map;
    }
}

// ─── Test Sound Generator ────────────────────────────────────────────────────

static class TestSoundGenerator
{
    public static void EnsureTestSounds(string baseDir)
    {
        var sfxDir = Path.Combine(baseDir, "sfx");
        Directory.CreateDirectory(sfxDir);

        GenerateWav(Path.Combine(sfxDir, "coin.wav"), 0.15f, 880f, volume: 0.5f);
        GenerateWav(Path.Combine(sfxDir, "interact.wav"), 0.2f, 440f, volume: 0.4f);
        GenerateChordWav(Path.Combine(sfxDir, "music.wav"), 8f, [261.6f, 329.6f, 392f], volume: 0.25f);
    }

    static void GenerateWav(string path, float duration, float frequency, float volume)
    {
        if (File.Exists(path)) return;

        const int sampleRate = 22050;
        int numSamples = (int)(sampleRate * duration);
        var samples = new short[numSamples];

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = 1f;
            if (t < 0.01f) envelope = t / 0.01f;
            float remaining = duration - t;
            if (remaining < 0.05f) envelope = remaining / 0.05f;

            samples[i] = (short)(MathF.Sin(2 * MathF.PI * frequency * t) * volume * 32767 * envelope);
        }

        WriteWav(path, samples, sampleRate);
    }

    static void GenerateChordWav(string path, float duration, float[] frequencies, float volume)
    {
        if (File.Exists(path)) return;

        const int sampleRate = 22050;
        int numSamples = (int)(sampleRate * duration);
        var samples = new short[numSamples];
        float perNote = volume / frequencies.Length;

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = 1f;
            if (t < 0.5f) envelope = t / 0.5f;
            float remaining = duration - t;
            if (remaining < 1f) envelope = remaining / 1f;

            float sample = 0f;
            foreach (var freq in frequencies)
                sample += MathF.Sin(2 * MathF.PI * freq * t);

            samples[i] = (short)(sample * perNote * 32767 * envelope);
        }

        WriteWav(path, samples, sampleRate);
    }

    static void WriteWav(string path, short[] samples, int sampleRate)
    {
        int dataSize = samples.Length * 2;
        using var fs = File.Create(path);
        using var bw = new BinaryWriter(fs);

        // RIFF header
        bw.Write("RIFF"u8);
        bw.Write(36 + dataSize);
        bw.Write("WAVE"u8);

        // fmt chunk
        bw.Write("fmt "u8);
        bw.Write(16);
        bw.Write((short)1);  // PCM
        bw.Write((short)1);  // mono
        bw.Write(sampleRate);
        bw.Write(sampleRate * 2);  // byte rate
        bw.Write((short)2);  // block align
        bw.Write((short)16); // bits per sample

        // data chunk
        bw.Write("data"u8);
        bw.Write(dataSize);
        foreach (var s in samples)
            bw.Write(s);
    }
}
