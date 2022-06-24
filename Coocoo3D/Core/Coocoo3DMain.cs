using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Coocoo3DGraphics;
using Coocoo3D.Common;
using Coocoo3D.Present;
using Coocoo3D.RenderPipeline;

namespace Coocoo3D.Core
{
    public class Coocoo3DMain : IDisposable
    {
        GraphicsDevice graphicsDevice { get => RPContext.graphicsDevice; }
        GraphicsContext graphicsContext { get => RPContext.graphicsContext; }
        public string deviceDescription { get => graphicsDevice.GetDeviceDescription(); }
        public MainCaches mainCaches { get => RPContext.mainCaches; }

        public RenderPipelineContext RPContext = new RenderPipelineContext();

        public Scene CurrentScene;
        public AnimationSystem animationSystem;
        public PhysicsSystem physicsSystem;
        public WindowSystem windowSystem;
        public RenderSystem renderSystem;
        public RecordSystem recordSystem;
        public UIRenderSystem uiRenderSystem;

        public void AddGameObject(GameObject gameObject)
        {
            CurrentScene.gameObjectLoadList.Add(gameObject);
            physicsSystem.gameObjectLoadList.Add(gameObject);
        }

        public void RemoveGameObject(GameObject gameObject)
        {
            CurrentScene.gameObjectRemoveList.Add(gameObject);
            physicsSystem.gameObjectRemoveList.Add(gameObject);
        }

        public void SetTransform(GameObject gameObject, Transform transform)
        {
            CurrentScene.setTransform[gameObject] = transform;
            physicsSystem.setTransform[gameObject] = transform;
        }

        public List<GameObject> SelectedGameObjects = new List<GameObject>();

        public GameDriver GameDriver = new GameDriver();

        public TimeManager timeManager = new TimeManager();
        public double framePerSecond;
        public float frameInterval = 1 / 240.0f;

        public PerformanceSettings performanceSettings = new PerformanceSettings()
        {
            MultiThreadRendering = true,
            SaveCpuPower = true,
            VSync = false,
        };

        Thread renderWorkThread;
        CancellationTokenSource cancelRenderThread;
        public GameDriverContext GameDriverContext { get => GameDriver.gameDriverContext; }
        public Coocoo3DMain()
        {
            RPContext.Load();
            mainCaches._RequireRender = () => RequireRender(false);

            CurrentScene = new Scene();
            animationSystem = new AnimationSystem();
            animationSystem.scene = CurrentScene;
            animationSystem.caches = mainCaches;
            physicsSystem = new PhysicsSystem();
            physicsSystem.scene = CurrentScene;
            physicsSystem.Initialize();
            windowSystem = new WindowSystem();
            windowSystem.RenderPipelineContext = RPContext;
            windowSystem.Initialize();
            renderSystem = new RenderSystem();
            renderSystem.windowSystem = windowSystem;
            renderSystem.graphicsContext = graphicsContext;
            recordSystem = new RecordSystem();
            recordSystem.windowSystem = windowSystem;
            recordSystem.gameDriverContext = GameDriverContext;
            recordSystem.graphicsDevice = graphicsDevice;
            recordSystem.graphicsContext = graphicsContext;
            recordSystem.Initialize();
            uiRenderSystem = new UIRenderSystem();
            uiRenderSystem.swapChain = RPContext.swapChain;
            uiRenderSystem.graphicsContext = graphicsContext;
            uiRenderSystem.caches = mainCaches;

            GameDriverContext.timeManager = timeManager;

            cancelRenderThread = new CancellationTokenSource();
            renderWorkThread = new Thread(() =>
            {
                var token = cancelRenderThread.Token;
                while (!token.IsCancellationRequested)
                {
                    timeManager.AbsoluteTimeInput(stopwatch1.ElapsedTicks);
                    if (timeManager.RealTimerCorrect("render", frameInterval, out _)) continue;

                    bool rendered = RenderFrame();
                    if (performanceSettings.SaveCpuPower && !RPContext.recording && !(performanceSettings.VSync && rendered))
                        Thread.Sleep(1);
                }
            });
            renderWorkThread.IsBackground = true;
            renderWorkThread.Start();

            RequireRender();
        }
        #region Rendering

        public UI.PlatformIO platformIO = new UI.PlatformIO();

        public void RequireRender(bool updateEntities = false)
        {
            GameDriverContext.RequireRender(updateEntities);
        }

        public System.Diagnostics.Stopwatch stopwatch1 = System.Diagnostics.Stopwatch.StartNew();
        Task RenderTask1;

        private void Simulation()
        {
            var gdc = GameDriverContext;

            CurrentScene.DealProcessList();
            physicsSystem.DealProcessList();
            if (CurrentScene.setTransform.Count != 0) gdc.RequireResetPhysics = true;
            if (gdc.Playing || gdc.RequireResetPhysics)
            {
                CurrentScene.Simulation();

                animationSystem.playTime = (float)gdc.PlayTime;
                animationSystem.Update();

                physicsSystem.resetPhysics = gdc.RequireResetPhysics;
                physicsSystem.deltaTime = gdc.DeltaTime;
                physicsSystem.Update();
                gdc.RequireResetPhysics = false;
            }
        }

        private bool RenderFrame()
        {
            double deltaTime = timeManager.GetDeltaTime();
            var gdc = GameDriverContext;
            gdc.FrameInterval = frameInterval;
            if (!GameDriver.Next(windowSystem, recordSystem))
            {
                return false;
            }
            timeManager.RealCounter("fps", 1, out framePerSecond);
            Simulation();

            var dynamicContext = RPContext.GetDynamicContext(CurrentScene);
            dynamicContext.RealDeltaTime = deltaTime;
            if (RenderTask1 != null && RenderTask1.Status != TaskStatus.RanToCompletion) RenderTask1.Wait();
            dynamicContext.Time = gdc.PlayTime;
            dynamicContext.DeltaTime = gdc.Playing ? gdc.DeltaTime : 0;
            RPContext.Submit(dynamicContext);
            if ((RPContext.swapChain.width, RPContext.swapChain.height) != platformIO.windowSize)
            {
                (int x, int y) = platformIO.windowSize;
                RPContext.swapChain.Resize(x, y);
            }
            if (!RPContext.recording)
                mainCaches.OnFrame();

            windowSystem.Update2();
            platformIO.Update();
            UI.UIImGui.GUI(this);
            graphicsDevice.RenderBegin();
            graphicsContext.Begin();
            RPContext.UpdateGPUResource();

            if (performanceSettings.MultiThreadRendering)
                RenderTask1 = Task.Run(RenderFunction);
            else
                RenderFunction();

            return true;
        }
        void RenderFunction()
        {
            windowSystem.Update();
            renderSystem.Update();

            GameDriver.AfterRender(windowSystem);
            recordSystem.Record();

            uiRenderSystem.Update();
            graphicsContext.Present(RPContext.swapChain, performanceSettings.VSync);
            graphicsContext.EndCommand();
            graphicsContext.Execute();
            drawTriangleCount = graphicsContext.TriangleCount;
            graphicsDevice.RenderComplete();
        }

        public void Dispose()
        {
            cancelRenderThread.Cancel();
            graphicsDevice.WaitForGpu();
            RPContext.Dispose();
        }
        #endregion
        public int drawTriangleCount = 0;

        public void ToPlayMode()
        {
            GameDriver.ToPlayMode();
        }

        public void ToRecordMode(string saveDir)
        {
            GameDriver.ToRecordMode(saveDir);
        }

        public void SetWindow(IntPtr hwnd, int width, int height)
        {
            RPContext.swapChain.Initialize(graphicsDevice, hwnd, width, height);
            platformIO.windowSize = (width, height);
        }
    }

    public struct PerformanceSettings
    {
        public bool MultiThreadRendering;
        public bool SaveCpuPower;
        public bool VSync;
    }
}
