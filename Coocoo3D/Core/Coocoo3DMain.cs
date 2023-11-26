﻿using Coocoo3D.Common;
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

    public DX12ResourceManager manager;
    GraphicsDevice graphicsDevice;
    SwapChain swapChain;
    public MainCaches mainCaches;
    public RenderPipelineContext RPContext;
    public GameDriverContext GameDriverContext;

    public Scene CurrentScene;
    public EditorContext EditorContext;
    public AnimationSystem animationSystem;

    public SceneExtensionsSystem sceneExtensions;

    public WindowSystem windowSystem;
    public RenderSystem renderSystem;
    public RecordSystem recordSystem;
    public UIRenderSystem uiRenderSystem;

    public GameDriver GameDriver;

    public PlatformIO platformIO;
    public UI.UIImGui UIImGui;

    GraphicsContext graphicsContext { get => RPContext.graphicsContext; }

    public EngineContext EngineContext = new EngineContext();

    public TimeManager timeManagerUpdate = new TimeManager();
    public TimeManager timeManager = new TimeManager();

    public Config config;

    Thread renderWorkThread;
    CancellationTokenSource cancelRenderThread;

    public Coocoo3DMain(LaunchOption launchOption)
    {
        Launch();
        if (launchOption.openFile != null)
        {
            UI.UIImGui.openRequest = new FileInfo(launchOption.openFile);
        }
        if (launchOption.AddLight)
            CurrentScene.NewLighting();
    }

    void Launch()
    {
        var e = EngineContext;
        world = e.AddSystem<DefaultEcs.World>();
        recorder = e.AddSystem<EntityCommandRecorder>();

        statistics = e.AddSystem<Statistics>();

        config = e.AddSystem<Config>();

        graphicsDevice = e.AddSystem<GraphicsDevice>();

        manager = new DX12ResourceManager();
        manager.DX12Resource = JsonConvert.DeserializeObject<DX12Resource>(File.ReadAllText("Assets/DX12Launch.json"),
            new JsonSerializerSettings { PreserveReferencesHandling = PreserveReferencesHandling.All });
        e.AddSystem(manager);

        swapChain = e.AddSystem<SwapChain>();

        mainCaches = e.AddSystem<MainCaches>();

        EditorContext = e.AddSystem<EditorContext>();

        RPContext = e.AddSystem<RenderPipelineContext>();
        e.AddSystem(RPContext.graphicsContext);

        GameDriverContext = e.AddSystem<GameDriverContext>();

        CurrentScene = e.AddSystem<Scene>();

        animationSystem = e.AddSystem<AnimationSystem>();

        windowSystem = e.AddSystem<WindowSystem>();

        sceneExtensions = e.AddSystem<SceneExtensionsSystem>();

        renderSystem = e.AddSystem<RenderSystem>();

        recordSystem = e.AddSystem<RecordSystem>();

        uiRenderSystem = e.AddSystem<UIRenderSystem>();

        GameDriver = e.AddSystem<GameDriver>();

        platformIO = e.AddSystem<PlatformIO>();
        UIImGui = e.AddSystem<UI.UIImGui>();

        e.InitializeSystems();

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
            if (config.SaveCpuPower && !RPContext.recording && !(config.VSync && rendered))
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
            //animationSystem.playTime = (float)gdc.PlayTime;
            animationSystem.deltaTime = (float)gdc.DeltaTime;
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
        EngineContext.SyncCallStage();
        timeManager.RealCounter("fps", 1, out statistics.FramePerSecond);
        Simulation();

        if ((swapChain.width, swapChain.height) != platformIO.windowSize)
        {
            (int x, int y) = platformIO.windowSize;
            swapChain.Resize(x, y);
        }

        platformIO.Update();
        windowSystem.Update();
        UIImGui.GUI();
        RPContext.FrameBegin();

        graphicsContext.Begin();
        mainCaches.OnFrame(graphicsContext);

        renderSystem.Update();
        uiRenderSystem.Update();
        recordSystem.Update();

        graphicsContext.Execute();

        statistics.DrawTriangleCount = graphicsContext.TriangleCount;
        swapChain.Present(config.VSync);

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
        //swapChain.Initialize(graphicsDevice, hwnd, width, height);
        manager.InitializeSwapChain(swapChain, hwnd, width, height);
        platformIO.windowSize = (width, height);
    }
}
