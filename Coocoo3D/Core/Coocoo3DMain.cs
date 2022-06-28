using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Coocoo3DGraphics;
using Coocoo3D.Common;
using Coocoo3D.RenderPipeline;

namespace Coocoo3D.Core
{
    public class Coocoo3DMain : IDisposable
    {
        GraphicsContext graphicsContext { get => RPContext.graphicsContext; }
        public SwapChain swapChain = new SwapChain();

        public string deviceDescription { get => graphicsDevice.GetDeviceDescription(); }

        public GameDriverContext GameDriverContext = new GameDriverContext()
        {
            FrameInterval = 1 / 240.0f,
        };

        GraphicsDevice graphicsDevice;
        public MainCaches mainCaches;
        public RenderPipelineContext RPContext;

        public Scene CurrentScene;
        public AnimationSystem animationSystem;
        public PhysicsSystem physicsSystem;
        public WindowSystem windowSystem;
        public RenderSystem renderSystem;
        public RecordSystem recordSystem;
        public UIRenderSystem uiRenderSystem;

        public GameDriver GameDriver;

        public UI.UIHelper UIHelper;
        public UI.PlatformIO platformIO;
        public UI.UIImGui UIImGui;

        public List<object> systems = new();
        public Dictionary<Type, object> systems1 = new();

        T AddSystem<T>() where T : class, new()
        {
            var system = new T();
            systems.Add(system);
            systems1[typeof(T)] = system;
            return system;
        }

        void InitializeSystems()
        {
            foreach (var system in systems)
            {
                var type = system.GetType();
                var fields = type.GetFields();
                foreach (var field in fields)
                {
                    if (field.FieldType == typeof(GraphicsContext))
                        field.SetValue(system, graphicsContext);
                    else if (field.FieldType == typeof(GameDriverContext))
                        field.SetValue(system, GameDriverContext);
                    else if (field.FieldType == typeof(SwapChain))
                        field.SetValue(system, swapChain);
                    if (systems1.TryGetValue(field.FieldType, out var system1))
                    {
                        field.SetValue(system, system1);
                    }
                }
                var menthods = type.GetMethods();
                foreach (var method in menthods)
                {
                    if (method.Name == "Initialize")
                    {
                        method.Invoke(system, null);
                        break;
                    }
                }
            }
        }

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

        public Coocoo3DMain()
        {
            graphicsDevice = AddSystem<GraphicsDevice>();

            mainCaches = AddSystem<MainCaches>();

            RPContext = AddSystem<RenderPipelineContext>();

            CurrentScene = AddSystem<Scene>();

            animationSystem = AddSystem<AnimationSystem>();

            physicsSystem = AddSystem<PhysicsSystem>();

            windowSystem = AddSystem<WindowSystem>();

            renderSystem = AddSystem<RenderSystem>();

            recordSystem = AddSystem<RecordSystem>();

            uiRenderSystem = AddSystem<UIRenderSystem>();

            GameDriver = AddSystem<GameDriver>();

            platformIO = AddSystem<UI.PlatformIO>();
            UIHelper = AddSystem<UI.UIHelper>();
            UIImGui = AddSystem<UI.UIImGui>();

            InitializeSystems();

            mainCaches._RequireRender = () => RequireRender(false);
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
            CurrentScene.Clear();
        }

        private bool RenderFrame()
        {
            double deltaTime = timeManager.GetDeltaTime();
            var gdc = GameDriverContext;
            gdc.FrameInterval = frameInterval;
            if (!GameDriver.Next())
            {
                return false;
            }
            timeManager.RealCounter("fps", 1, out framePerSecond);
            Simulation();

            if (RenderTask1 != null && RenderTask1.Status != TaskStatus.RanToCompletion) RenderTask1.Wait();
            RPContext.dynamicContext.RealDeltaTime = deltaTime;
            RPContext.dynamicContext.Time = gdc.PlayTime;
            RPContext.dynamicContext.DeltaTime = gdc.Playing ? gdc.DeltaTime : 0;
            RPContext.Submit(CurrentScene);
            if ((swapChain.width, swapChain.height) != platformIO.windowSize)
            {
                (int x, int y) = platformIO.windowSize;
                swapChain.Resize(x, y);
            }
            if (!RPContext.recording)
                mainCaches.OnFrame();

            windowSystem.Update2();
            platformIO.Update();
            UIImGui.GUI(this);
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

            GameDriver.AfterRender();
            recordSystem.Record();

            uiRenderSystem.Update();
            graphicsContext.Present(swapChain, performanceSettings.VSync);
            graphicsContext.EndCommand();
            graphicsContext.Execute();
            drawTriangleCount = graphicsContext.TriangleCount;
            graphicsDevice.RenderComplete();
        }

        #endregion
        public int drawTriangleCount = 0;

        public void Dispose()
        {
            cancelRenderThread.Cancel();
            renderWorkThread.Join();
            if (RenderTask1 != null && RenderTask1.Status == TaskStatus.Running)
                RenderTask1.Wait();
            graphicsDevice.WaitForGpu();

            for (int i = systems.Count - 1; i >= 0; i--)
            {
                object system = systems[i];
                if (system is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            swapChain?.Dispose();
        }
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
            swapChain.Initialize(graphicsDevice, hwnd, width, height);
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
