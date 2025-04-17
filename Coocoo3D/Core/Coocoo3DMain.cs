using Coocoo3D.Common;
using Coocoo3D.RenderPipeline;
using Coocoo3D.UI;
using Coocoo3DGraphics;
using Arch.Core;
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Coocoo3D.Core;

public class Coocoo3DMain : IDisposable
{
    World world;
    public Statistics statistics;

    GraphicsDevice graphicsDevice;
    SwapChain swapChain;
    public MainCaches mainCaches;
    public RenderPipelineContext RPContext;
    public GameDriverContext GameDriverContext;

    public Scene CurrentScene;
    public EditorContext EditorContext;

    public SceneExtensionsSystem sceneExtensions;

    public RenderSystem renderSystem;
    public UIRenderSystem uiRenderSystem;

    public GameDriver GameDriver;

    public PlatformIO platformIO;
    public UI.UIImGui UIImGui;

    GraphicsContext graphicsContext;

    public EngineContext EngineContext = new EngineContext();

    public TimeManager timeManagerUpdate = new TimeManager();
    public TimeManager timeManager = new TimeManager();

    Thread renderWorkThread;
    CancellationTokenSource cancelRenderThread;


    Action OnRender;
    public ConcurrentQueue<Action> OnRenderOnce = new ConcurrentQueue<Action>();
    public Action<EngineContext> launchCallback;

    public Coocoo3DMain()
    {
    }

    public void Launch()
    {
        var e = EngineContext;
        world = World.Create();
        e.AddSystem<Arch.Core.World>(world);

        statistics = e.AddSystem<Statistics>();

        graphicsDevice = e.AddSystem<GraphicsDevice>();

        swapChain = e.AddSystem<SwapChain>();

        mainCaches = e.AddSystem<MainCaches>();

        EditorContext = e.AddSystem<EditorContext>();

        RPContext = e.AddSystem<RenderPipelineContext>();


        graphicsContext = new();
        graphicsContext.Initialize(graphicsDevice);
        e.AddSystem(graphicsContext);

        GameDriverContext = e.AddSystem<GameDriverContext>();

        CurrentScene = e.AddSystem<Scene>();

        sceneExtensions = e.AddSystem<SceneExtensionsSystem>();

        renderSystem = e.AddSystem<RenderSystem>();

        uiRenderSystem = e.AddSystem<UIRenderSystem>();

        GameDriver = e.AddSystem<GameDriver>();

        platformIO = e.AddSystem<PlatformIO>();
        UIImGui = e.AddSystem<UI.UIImGui>();
        e.AddAutoFill(this);
        e.InitializeSystems();

        statistics.DeviceDescription = graphicsDevice.GetDeviceDescription();

        GameDriverContext.timeManager = timeManager;
        GameDriverContext.FrameInterval = 1 / 240.0f;

        cancelRenderThread = new CancellationTokenSource();
        renderWorkThread = new Thread(Loop);
        renderWorkThread.IsBackground = true;
        renderWorkThread.Start();

        launchCallback?.Invoke(EngineContext);
    }

    void Loop()
    {
        var token = cancelRenderThread.Token;
        while (!token.IsCancellationRequested)
        {
            OnRender?.Invoke();
            while (OnRenderOnce.TryDequeue(out var action))
            {
                action.Invoke();
            }

            long time = stopwatch1.ElapsedTicks;
            timeManagerUpdate.AbsoluteTimeInput(time);
            if (timeManagerUpdate.RealTimerCorrect("render", GameDriverContext.FrameInterval, out _))
                continue;
            timeManager.AbsoluteTimeInput(time);

            bool rendered = RenderFrame();
            if (GameDriverContext.SaveCpuPower && !RPContext.recording && !(GameDriverContext.VSync && rendered))
                Thread.Sleep(1);
        }
    }

    #region Rendering

    public void RequireRender(bool updateEntities = false)
    {
        GameDriverContext.RequireRender(updateEntities);
    }

    public System.Diagnostics.Stopwatch stopwatch1 = System.Diagnostics.Stopwatch.StartNew();

    private void Simulation()
    {
        var gdc = GameDriverContext;

        CurrentScene.OnFrame();

        if (gdc.Playing || gdc.RefreshScene)
        {
            sceneExtensions.Update();

            gdc.RefreshScene = false;
        }
    }

    private bool RenderFrame()
    {
        if (!GameDriver.Next())
        {
            return false;
        }
        graphicsContext.Begin();
        EngineContext._OnFrameBegin();
        sceneExtensions.ProcessFileLoad();
        timeManager.RealCounter("fps", 1, out statistics.FramePerSecond);
        Simulation();

        platformIO.Update();
        UIImGui.GUI();
        RPContext.FrameBegin();

        mainCaches.OnFrame();

        uiRenderSystem.Update();

        EngineContext._OnFrameEnd();
        graphicsContext.Execute();

        statistics.DrawTriangleCount = graphicsContext.TriangleCount;
        swapChain.Present(GameDriverContext.VSync);

        return true;
    }

    #endregion

    public void Dispose()
    {
        cancelRenderThread.Cancel();
        renderWorkThread.Join();

        graphicsDevice.WaitForGpu();

        EngineContext.Dispose();
    }
}
