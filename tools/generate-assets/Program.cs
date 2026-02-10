using StbImageWriteSharp;

// Output directories
string showcaseRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
string engineDemoContent = Path.Combine(showcaseRoot, "src", "Showcase.EngineDemo", "content");
string fullDemoContent = Path.Combine(showcaseRoot, "src", "Showcase.FullDemo", "content");
string fullDemoSfx = Path.Combine(fullDemoContent, "sfx");

Directory.CreateDirectory(engineDemoContent);
Directory.CreateDirectory(fullDemoContent);
Directory.CreateDirectory(fullDemoSfx);

Console.WriteLine($"Showcase root: {showcaseRoot}");

// ─── Generate tileset PNG ──────────────────────────────────────────────────
var pixels = GenerateTilesetPixels();
int tw = 16, th = 16, cols = 8;
int texW = cols * tw, texH = th;

WritePng(Path.Combine(engineDemoContent, "tileset.png"), texW, texH, pixels);
WritePng(Path.Combine(fullDemoContent, "tileset.png"), texW, texH, pixels);
Console.WriteLine("Generated tileset.png for both demos.");

// ─── Generate WAV files ────────────────────────────────────────────────────
GenerateWav(Path.Combine(fullDemoSfx, "coin.wav"), 0.15f, 880f, volume: 0.5f);
GenerateWav(Path.Combine(fullDemoSfx, "interact.wav"), 0.2f, 440f, volume: 0.4f);
GenerateChordWav(Path.Combine(fullDemoSfx, "music.wav"), 8f, [261.6f, 329.6f, 392f], volume: 0.25f);
Console.WriteLine("Generated WAV files for FullDemo.");

Console.WriteLine("Done!");

// ─── Tileset pixel generation (matches demo procedural code exactly) ──────
static byte[] GenerateTilesetPixels()
{
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
    for (int i = 0; i < 12; i++)
        SetPixel(0, (i * 7 + 3) % 16, (i * 5 + 2) % 16, 80, 170, 60);

    // Tile 2: Wall
    FillTile(1, 80, 80, 90);
    for (int y = 0; y < 16; y++)
    for (int x = 0; x < 16; x++)
        if (y % 4 == 0 || (x + (y / 4 % 2) * 4) % 8 == 0)
            SetPixel(1, x, y, 60, 60, 70);

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
        SetPixel(3, (i * 7 + 5) % 16, (i * 3 + 1) % 16, 180, 160, 110);

    // Tile 5: Tree trunk
    FillTile(4, 60, 140, 50);
    for (int y = 6; y < 16; y++)
    for (int x = 6; x < 10; x++)
        SetPixel(4, x, y, 100, 60, 30);

    // Tile 6: Tree top
    FillTile(5, 60, 140, 50);
    for (int y = 0; y < 12; y++)
    for (int x = 2; x < 14; x++)
        if ((x - 8) * (x - 8) + (y - 5) * (y - 5) < 30)
            SetPixel(5, x, y, 30, 100, 25);

    // Tile 7: Coin
    FillTile(6, 60, 140, 50);
    for (int y = 4; y < 12; y++)
    for (int x = 4; x < 12; x++)
        if ((x - 8) * (x - 8) + (y - 8) * (y - 8) < 14)
            SetPixel(6, x, y, 255, 220, 50);
    SetPixel(6, 6, 6, 255, 250, 150);
    SetPixel(6, 7, 6, 255, 250, 150);

    // Tile 8: Flower
    FillTile(7, 60, 140, 50);
    SetPixel(7, 8, 9, 40, 120, 30);
    SetPixel(7, 8, 8, 40, 120, 30);
    SetPixel(7, 8, 7, 255, 100, 150);
    SetPixel(7, 7, 6, 255, 130, 170);
    SetPixel(7, 9, 6, 255, 130, 170);
    SetPixel(7, 8, 5, 255, 130, 170);
    SetPixel(7, 7, 8, 255, 130, 170);
    SetPixel(7, 9, 8, 255, 130, 170);

    return pixels;
}

// ─── PNG writing ───────────────────────────────────────────────────────────
static void WritePng(string path, int width, int height, byte[] rgbaPixels)
{
    using var fs = File.Create(path);
    var writer = new ImageWriter();
    writer.WritePng(rgbaPixels, width, height, ColorComponents.RedGreenBlueAlpha, fs);
}

// ─── WAV generation (matches TestSoundGenerator exactly) ───────────────────
static void GenerateWav(string path, float duration, float frequency, float volume)
{
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

    bw.Write("RIFF"u8);
    bw.Write(36 + dataSize);
    bw.Write("WAVE"u8);

    bw.Write("fmt "u8);
    bw.Write(16);
    bw.Write((short)1);
    bw.Write((short)1);
    bw.Write(sampleRate);
    bw.Write(sampleRate * 2);
    bw.Write((short)2);
    bw.Write((short)16);

    bw.Write("data"u8);
    bw.Write(dataSize);
    foreach (var s in samples)
        bw.Write(s);
}
