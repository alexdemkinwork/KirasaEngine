using System.Runtime.InteropServices;
using System.Threading;
using Avalonia.Media.Imaging;
using KirasaEngine.MGL.Rendering;
using KirasaEngine.MGL.Rendering.Abstractions;
using KirasaEngine.MGL.Rendering.Abstractions.Enums;
using KirasaEngine.MGL.Rendering.Abstractions.Descriptions;
using KirasaEngine.MGL.SceneGraph;
using Silk.NET.Windowing;

namespace KirasaEngine.Editor.Infrastructure.Services;

[RegisterScoped]
public sealed class RendererService : IDisposable
{
    private IGraphicsDevice? _device;
    private SceneRenderer? _renderer;
    private IWindow? _window;
    private Scene? _currentScene;
    private CancellationTokenSource? _cts;
    private Thread? _renderThread;
    private readonly object _lock = new();
    private uint _width = 800;
    private uint _height = 600;
    private GraphicsBackend _backend = GraphicsBackend.Direct3D11;

    public event Action<byte[]>? FrameRendered;

    public void Initialize(Scene scene, uint width = 800, uint height = 600, GraphicsBackend backend = GraphicsBackend.Direct3D11)
    {
        lock (_lock)
        {
            DisposeInternal();

            _width = width;
            _height = height;
            _backend = backend;
            _currentScene = scene;

            _window = GraphicsDeviceFactory.CreateWindow(backend, width, height, "Editor Render", visible: false);
            _window.Initialize();

            _device = GraphicsDeviceFactory.Create(backend);
            _device.Initialize(new GraphicsDeviceDescription { Window = _window, Width = width, Height = height });

            _renderer = new SceneRenderer(_device);
        }
    }

    public void StartRendering(CancellationToken token)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        _renderThread = new Thread(RenderLoop) { IsBackground = true };
        _renderThread.Start();
    }

    private void RenderLoop()
    {
        try
        {
            while (!_cts!.Token.IsCancellationRequested)
            {
                if (_renderer != null && _currentScene != null)
                {
                    var pixels = _renderer.RenderToTexture(_currentScene, _width, _height);
                    FrameRendered?.Invoke(pixels);
                }
                Thread.Sleep(16); // ~60 FPS
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception) { }
    }

    public void UpdateScene(Scene scene)
    {
        lock (_lock)
        {
            _currentScene = scene;
        }
    }

    public void Resize(uint width, uint height)
    {
        lock (_lock)
        {
            _width = width;
            _height = height;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            DisposeInternal();
        }
    }

    private void DisposeInternal()
    {
        _cts?.Cancel();
        if (_renderThread is { IsAlive: true })
            _renderThread.Join(1000);

        _renderer?.Dispose();
        _device?.Dispose();
        _window?.Dispose();

        _renderer = null;
        _device = null;
        _window = null;
        _currentScene = null;
        _cts?.Dispose();
        _cts = null;
    }
}