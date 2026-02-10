using Obsydian.Core;
using Obsydian.Core.Math;
using Obsydian.Graphics;
using Obsydian.Input;
using Obsydian.Platform.Desktop;
using Obsydian.Platform.Desktop.Rendering;
using Silk.NET.Input;
using Silk.NET.OpenGL;

namespace Showcase.Common;

/// <summary>
/// Base class for showcase demos. Wires up Engine + DesktopWindow + GlRenderer + InputManager.
/// Subclasses override OnLoad/OnUpdate/OnRender to implement their demo logic.
/// </summary>
public abstract class ShowcaseApp
{
    public Engine Engine { get; }
    public DesktopWindow Window { get; }
    public GlRenderer Renderer { get; }
    public InputManager Input { get; }

    private SilkInputBridge? _inputBridge;

    protected ShowcaseApp(EngineConfig? config = null)
    {
        Engine = new Engine(config ?? new EngineConfig());
        Window = new DesktopWindow();
        Renderer = new GlRenderer();
        Input = new InputManager();
    }

    public void Run()
    {
        Window.Create(Engine.Config.Title, Engine.Config.WindowWidth, Engine.Config.WindowHeight);

        Window.OnLoad += () =>
        {
            var gl = GL.GetApi(Window.NativeWindow);
            Renderer.InitializeWithGl(gl, Window.Width, Window.Height);

            // Wire input
            var silkInput = Window.NativeWindow.CreateInput();
            _inputBridge = new SilkInputBridge(Input);
            _inputBridge.Connect(silkInput);

            Engine.Initialize();
            OnLoad();
        };

        Window.OnUpdate += dt =>
        {
            Input.BeginFrame();
            Engine.Update((float)dt);
            OnUpdate((float)dt);
        };

        Window.OnRenderFrame += dt =>
        {
            Renderer.BeginFrame();
            Renderer.Clear(ClearColor);
            OnRender((float)dt);
            Renderer.EndFrame();
        };

        Window.OnResize += (w, h) =>
        {
            Renderer.OnResize(w, h);
        };

        Window.OnClose += () =>
        {
            Engine.Shutdown();
            Renderer.Shutdown();
        };

        Window.Run();
        Window.Dispose();
    }

    protected virtual Color ClearColor => Color.CornflowerBlue;
    protected virtual void OnLoad() { }
    protected virtual void OnUpdate(float dt) { }
    protected virtual void OnRender(float dt) { }
}
