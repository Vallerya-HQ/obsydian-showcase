using Obsydian.Core;
using Obsydian.Core.Math;
using Obsydian.Input;
using Showcase.Common;

namespace Showcase.HelloWindow;

/// <summary>
/// Phase 1 showcase: Opens a 1280x720 window, clears to CornflowerBlue,
/// draws a colored rectangle that moves with arrow keys, Escape to close.
/// </summary>
public class HelloWindowDemo : ShowcaseApp
{
    private Vec2 _rectPosition = new(590, 310);
    private const float RectWidth = 100;
    private const float RectHeight = 100;
    private const float MoveSpeed = 300f;

    public HelloWindowDemo() : base(new EngineConfig
    {
        Title = "Obsydian - Hello Window",
        WindowWidth = 1280,
        WindowHeight = 720
    })
    { }

    protected override void OnUpdate(float dt)
    {
        if (Input.IsKeyDown(Key.Escape))
            Window.Close();

        var dx = 0f;
        var dy = 0f;

        if (Input.IsKeyDown(Key.Left) || Input.IsKeyDown(Key.A)) dx -= 1;
        if (Input.IsKeyDown(Key.Right) || Input.IsKeyDown(Key.D)) dx += 1;
        if (Input.IsKeyDown(Key.Up) || Input.IsKeyDown(Key.W)) dy -= 1;
        if (Input.IsKeyDown(Key.Down) || Input.IsKeyDown(Key.S)) dy += 1;

        // Normalize diagonal movement
        if (dx != 0 && dy != 0)
        {
            dx *= 0.7071f;
            dy *= 0.7071f;
        }

        _rectPosition = new Vec2(
            _rectPosition.X + dx * MoveSpeed * dt,
            _rectPosition.Y + dy * MoveSpeed * dt
        );

        // Clamp to window bounds
        _rectPosition = new Vec2(
            System.Math.Clamp(_rectPosition.X, 0, 1280 - RectWidth),
            System.Math.Clamp(_rectPosition.Y, 0, 720 - RectHeight)
        );
    }

    protected override void OnRender(float dt)
    {
        // Draw a white rectangle
        Renderer.DrawRect(new Rect(_rectPosition.X, _rectPosition.Y, RectWidth, RectHeight), Color.White);

        // Draw a smaller red rectangle inside as a "sprite" indicator
        Renderer.DrawRect(new Rect(_rectPosition.X + 10, _rectPosition.Y + 10, RectWidth - 20, RectHeight - 20),
            new Color(220, 50, 50));
    }
}

public static class Program
{
    public static void Main()
    {
        new HelloWindowDemo().Run();
    }
}
