using Obsydian.Audio;
using Obsydian.Content;
using Obsydian.Core;
using Obsydian.Core.Math;
using Obsydian.Core.Scenes;
using Obsydian.DevTools;
using Obsydian.Graphics;
using Obsydian.Input;
using Obsydian.Platform.Desktop;
using Obsydian.Platform.Desktop.Audio;
using Obsydian.Platform.Desktop.Rendering;
using Silk.NET.Input;
using Silk.NET.OpenGL;

namespace Showcase.Common;

/// <summary>
/// Base class for showcase demos. Wires up Engine + DesktopWindow + GlRenderer + InputManager.
/// Subclasses override OnLoad/OnUpdate/OnRender to implement their demo logic.
/// Includes DevToolsOverlay (toggled with F3) automatically.
/// </summary>
public abstract class ShowcaseApp
{
    public Engine Engine { get; }
    public DesktopWindow Window { get; }
    public GlRenderer Renderer { get; }
    public InputManager Input { get; }
    public ContentManager Content { get; private set; } = null!;
    public IAudioEngine Audio { get; private set; } = null!;

    /// <summary>The raw Silk.NET GL context, available after OnLoad.</summary>
    protected GL Gl { get; private set; } = null!;

    /// <summary>The DevTools overlay. Available after OnLoad.</summary>
    public DevToolsOverlay? DevTools { get; private set; }

    private SilkInputBridge? _inputBridge;
    private IInputContext? _silkInput;

    protected ShowcaseApp(EngineConfig? config = null)
    {
        Engine = new Engine(config ?? new EngineConfig());
        Window = new DesktopWindow();
        Renderer = new GlRenderer();
        Input = new InputManager();
    }

    /// <summary>
    /// Override to provide a SceneManager for the DevTools scene hierarchy panel.
    /// </summary>
    protected virtual SceneManager? GetSceneManager() => null;

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
            _silkInput = Window.NativeWindow.CreateInput();
            _inputBridge = new SilkInputBridge(Input);
            _inputBridge.Connect(_silkInput);

            // Wire audio
            Audio = new OpenAlAudioEngine(contentRoot);
            Audio.Initialize();

            Engine.Initialize();
            OnLoad();

            // Create DevTools after OnLoad so subclasses can set up SceneManager first
            DevTools = new DevToolsOverlay(
                Gl, Window.NativeWindow, _silkInput,
                Engine, Renderer, GetSceneManager());
        };

        Window.OnUpdate += dt =>
        {
            Engine.Update((float)dt);
            Audio?.Update();
            OnUpdate((float)dt);
            DevTools?.Update((float)dt, Input);
            Input.BeginFrame();
        };

        Window.OnRenderFrame += dt =>
        {
            Renderer.BeginFrame();
            Renderer.Clear(ClearColor);
            OnRender((float)dt);
            Renderer.EndFrame();
            DevTools?.Render();
        };

        Window.OnResize += (w, h) =>
        {
            Renderer.OnResize(w, h, Window.Width, Window.Height);
        };

        Window.OnClose += () =>
        {
            DevTools?.Dispose();
            Audio?.Dispose();
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
