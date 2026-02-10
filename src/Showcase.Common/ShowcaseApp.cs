using Obsydian.Content;
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
    public ContentManager Content { get; private set; } = null!;

    /// <summary>The raw Silk.NET GL context, available after OnLoad.</summary>
    protected GL Gl { get; private set; } = null!;

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
            Gl = GL.GetApi(Window.NativeWindow);
            Renderer.InitializeWithGl(Gl, Window.FramebufferWidth, Window.FramebufferHeight, Window.Width, Window.Height);

            // Set up content manager with exe directory as root
            var contentRoot = AppDomain.CurrentDomain.BaseDirectory;
            Content = new ContentManager(contentRoot);
            Content.RegisterLoader(new GlTextureLoader(Gl));

            // Wire input
            var silkInput = Window.NativeWindow.CreateInput();
            _inputBridge = new SilkInputBridge(Input);
            _inputBridge.Connect(silkInput);

            Engine.Initialize();
            OnLoad();
        };

        Window.OnUpdate += dt =>
        {
            // Order matters: Silk.NET fires input events during PollEvents() at
            // the start of the frame, BEFORE this callback. So the pressed/released
            // buffers are populated by the time we get here. Process game logic first,
            // then clear the buffers at the end so they're ready for next frame's events.
            Engine.Update((float)dt);
            OnUpdate((float)dt);
            Input.BeginFrame();
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
            Renderer.OnResize(w, h, Window.Width, Window.Height);
        };

        Window.OnClose += () =>
        {
            Content?.Dispose();
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
