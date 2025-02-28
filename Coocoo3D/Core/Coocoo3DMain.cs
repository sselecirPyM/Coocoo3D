using Coocoo3D.Common;
using Coocoo3D.RenderPipeline;
using Coocoo3D.UI;
using Coocoo3DGraphics;
using Coocoo3DGraphics.Management;
using DefaultEcs.Command;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading;

namespace Coocoo3D.Core;

public class Coocoo3DMain : IDisposable
{
    DefaultEcs.World world;
    public EntityCommandRecorder recorder;
    public Statistics statistics;

    GraphicsDevice graphicsDevice;
    SwapChain swapChain;
    public MainCaches mainCaches;
    public RenderPipelineContext RPContext;
    public GameDriverContext GameDriverContext;

    public Scene CurrentScene;
    public EditorContext EditorContext;
    public AnimationSystem animationSystem;

    public SceneExtensionsSystem sceneExtensions;

    public RenderSystem renderSystem;
    public UIRenderSystem uiRenderSystem;

    public GameDriver GameDriver;

    public PlatformIO platformIO;
    public UI.UIImGui UIImGui;

    GraphicsContext graphicsContext { get => RPContext.graphicsContext; }

    public EngineContext EngineContext = new EngineContext();

    public TimeManager timeManagerUpdate = new TimeManager();
    public TimeManager timeManager = new TimeManager();
    DX12Resource DX12Resource;

    Thread renderWorkThread;
    CancellationTokenSource cancelRenderThread;

    public Coocoo3DMain(LaunchOption launchOption)
    {
        Launch();
        if (launchOption.openFile != null)
        {
            sceneExtensions.OpenFile(launchOption.openFile);
        }
    }

    void Launch()
    {
        var e = EngineContext;
        world = e.AddSystem<DefaultEcs.World>();
        recorder = e.AddSystem<EntityCommandRecorder>();

        statistics = e.AddSystem<Statistics>();

        graphicsDevice = e.AddSystem<GraphicsDevice>();

        DX12Resource = JsonConvert.DeserializeObject<DX12Resource>(File.ReadAllText("Assets/DX12Launch.json"),
            new JsonSerializerSettings { PreserveReferencesHandling = PreserveReferencesHandling.All });

        swapChain = e.AddSystem<SwapChain>();

        mainCaches = e.AddSystem<MainCaches>();

        EditorContext = e.AddSystem<EditorContext>();

        RPContext = e.AddSystem<RenderPipelineContext>();
        e.AddSystem(RPContext.graphicsContext);

        GameDriverContext = e.AddSystem<GameDriverContext>();

        CurrentScene = e.AddSystem<Scene>();

        animationSystem = e.AddSystem<AnimationSystem>();

        sceneExtensions = e.AddSystem<SceneExtensionsSystem>();

        renderSystem = e.AddSystem<RenderSystem>();

        uiRenderSystem = e.AddSystem<UIRenderSystem>();

        GameDriver = e.AddSystem<GameDriver>();

        platformIO = e.AddSystem<PlatformIO>();
        UIImGui = e.AddSystem<UI.UIImGui>();

        e.InitializeSystems();
        mainCaches.Initialize1();

        statistics.DeviceDescription = graphicsDevice.GetDeviceDescription();

        GameDriverContext.timeManager = timeManager;
        GameDriverContext.FrameInterval = 1 / 240.0f;

        cancelRenderThread = new CancellationTokenSource();
        renderWorkThread = new Thread(RenderTask);
        renderWorkThread.IsBackground = true;
        renderWorkThread.Start();
    }

    void RenderTask()
    {
        var token = cancelRenderThread.Token;
        while (!token.IsCancellationRequested)
        {
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
            animationSystem.Update();

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

        if ((swapChain.width, swapChain.height) != platformIO.windowSize)
        {
            (int x, int y) = platformIO.windowSize;
            swapChain.Resize(x, y);
        }

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

    public void SetWindow(IntPtr hwnd, int width, int height)
    {
        InitializeSwapChain(swapChain, hwnd, width, height);
        platformIO.windowSize = (width, height);
    }
    public void InitializeSwapChain(SwapChain swapChain, IntPtr hwnd, int width, int height)
    {
        var desc = DX12Resource.SwapChainDescriptions[0].GetSwapChainDescription1();
        desc.Width = width;
        desc.Height = height;
        swapChain.Initialize(graphicsDevice, hwnd, desc);
    }
}
