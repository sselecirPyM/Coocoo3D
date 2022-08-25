using System;
using System.Collections.Generic;
using System.Linq;
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
        public Statistics statistics;

        GraphicsDevice graphicsDevice;
        public SwapChain swapChain;
        public MainCaches mainCaches;
        public RenderPipelineContext RPContext;
        public GameDriverContext GameDriverContext;

        public Scene CurrentScene;
        public AnimationSystem animationSystem;
        public PhysicsSystem physicsSystem;
        public ParticleSystem particleSystem;
        public WindowSystem windowSystem;
        public RenderSystem renderSystem;
        public RecordSystem recordSystem;
        public UIRenderSystem uiRenderSystem;

        public GameDriver GameDriver;

        public UI.UIHelper UIHelper;
        public PlatformIO platformIO;
        public UI.UIImGui UIImGui;

        public List<object> systems = new();
        public Dictionary<Type, object> systems1 = new();

        GraphicsContext graphicsContext { get => RPContext.graphicsContext; }

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
                    if (systems1.TryGetValue(field.FieldType, out var system1))
                    {
                        field.SetValue(system, system1);
                    }
                }
                var menthods = type.GetMethods();
                foreach (var method in menthods)
                {
                    if (method.Name == "Initialize" && method.GetParameters().Length == 0)
                    {
                        method.Invoke(system, null);
                        break;
                    }
                }
            }
        }

        public TimeManager timeManagerUpdate = new TimeManager();
        public TimeManager timeManager = new TimeManager();

        public Config config;

        Thread renderWorkThread;
        CancellationTokenSource cancelRenderThread;

        public Coocoo3DMain()
        {
            statistics = AddSystem<Statistics>();

            config = AddSystem<Config>();

            graphicsDevice = AddSystem<GraphicsDevice>();

            swapChain = AddSystem<SwapChain>();

            mainCaches = AddSystem<MainCaches>();

            RPContext = AddSystem<RenderPipelineContext>();

            GameDriverContext = AddSystem<GameDriverContext>();

            CurrentScene = AddSystem<Scene>();

            animationSystem = AddSystem<AnimationSystem>();

            physicsSystem = AddSystem<PhysicsSystem>();

            windowSystem = AddSystem<WindowSystem>();

            particleSystem = AddSystem<ParticleSystem>();

            renderSystem = AddSystem<RenderSystem>();

            recordSystem = AddSystem<RecordSystem>();

            uiRenderSystem = AddSystem<UIRenderSystem>();

            GameDriver = AddSystem<GameDriver>();

            platformIO = AddSystem<PlatformIO>();
            UIHelper = AddSystem<UI.UIHelper>();
            UIImGui = AddSystem<UI.UIImGui>();

            InitializeSystems();
            statistics.DeviceDescription = graphicsDevice.GetDeviceDescription();

            GameDriverContext.timeManager = timeManager;
            GameDriverContext.FrameInterval = 1 / 240.0f;

            cancelRenderThread = new CancellationTokenSource();
            renderWorkThread = new Thread(() =>
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

        private void Simulation()
        {
            var gdc = GameDriverContext;

            CurrentScene.OnFrame();
            physicsSystem.OnFrame();
            particleSystem.Onframe();
            if (CurrentScene.setTransform.Count != 0)
                gdc.RequireResetPhysics = true;
            if (gdc.Playing || gdc.RequireResetPhysics)
            {
                CurrentScene.Simulation();

                animationSystem.playTime = (float)gdc.PlayTime;
                animationSystem.Update();

                physicsSystem.resetPhysics = gdc.RequireResetPhysics;
                physicsSystem.deltaTime = gdc.DeltaTime;
                physicsSystem.Update();

                particleSystem.Update();

                gdc.RequireResetPhysics = false;
            }
            CurrentScene.ClearChanges();
        }

        private bool RenderFrame()
        {
            if (!GameDriver.Next())
            {
                return false;
            }
            timeManager.RealCounter("fps", 1, out statistics.FramePerSecond);
            Simulation();
            mainCaches.OnFrame();

            if ((swapChain.width, swapChain.height) != platformIO.windowSize)
            {
                (int x, int y) = platformIO.windowSize;
                swapChain.Resize(x, y);
            }

            platformIO.Update();
            windowSystem.Update();
            UIImGui.GUI();
            graphicsDevice.RenderBegin();
            graphicsContext.Begin();
            RPContext.FrameBegin();

            renderSystem.Update();

            GameDriver.AfterRender();
            recordSystem.Record();

            uiRenderSystem.Update();
            graphicsContext.Present(swapChain, config.VSync);
            graphicsContext.EndCommand();
            graphicsContext.Execute();
            graphicsDevice.RenderComplete();
            statistics.DrawTriangleCount = graphicsContext.TriangleCount;

            return true;
        }

        #endregion

        public void Dispose()
        {
            cancelRenderThread.Cancel();
            renderWorkThread.Join();

            graphicsDevice.WaitForGpu();

            for (int i = systems.Count - 1; i >= 0; i--)
            {
                object system = systems[i];
                if (system is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }

        public void SetWindow(IntPtr hwnd, int width, int height)
        {
            swapChain.Initialize(graphicsDevice, hwnd, width, height);
            platformIO.windowSize = (width, height);
        }
    }
}
