using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Coocoo3DGraphics;
using Coocoo3D.Common;
using Coocoo3D.RenderPipeline;
using DefaultEcs.System;
using DefaultEcs.Command;

namespace Coocoo3D.Core
{
    public class Coocoo3DMain : IDisposable
    {
        DefaultEcs.World world;
        public EntityCommandRecorder recorder;
        public Statistics statistics;

        GraphicsDevice graphicsDevice;
        public SwapChain swapChain;
        public MainCaches mainCaches;
        public RenderPipelineContext RPContext;
        public GameDriverContext GameDriverContext;

        public Scene CurrentScene;
        public AnimationSystem animationSystem;
        public ParticleSystem particleSystem;

        public SceneExtensionsSystem sceneExtensions;

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
        SequentialSystem<State> system2;

        GraphicsContext graphicsContext { get => RPContext.graphicsContext; }

        T AddSystem<T>() where T : class, new()
        {
            var system = new T();
            systems.Add(system);
            systems1[typeof(T)] = system;
            return system;
        }

        T AddSystem<T>(T system) where T : class
        {
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
            world = AddSystem<DefaultEcs.World>();
            recorder = AddSystem<EntityCommandRecorder>();

            statistics = AddSystem<Statistics>();

            config = AddSystem<Config>();

            graphicsDevice = AddSystem<GraphicsDevice>();

            swapChain = AddSystem<SwapChain>();

            mainCaches = AddSystem<MainCaches>();

            RPContext = AddSystem<RenderPipelineContext>();
            AddSystem(RPContext.graphicsContext);

            GameDriverContext = AddSystem<GameDriverContext>();

            CurrentScene = AddSystem<Scene>();

            animationSystem = AddSystem<AnimationSystem>();

            windowSystem = AddSystem<WindowSystem>();

            particleSystem = AddSystem<ParticleSystem>();

            sceneExtensions = AddSystem<SceneExtensionsSystem>();

            renderSystem = AddSystem<RenderSystem>();

            recordSystem = AddSystem<RecordSystem>();

            uiRenderSystem = AddSystem<UIRenderSystem>();

            GameDriver = AddSystem<GameDriver>();

            platformIO = AddSystem<PlatformIO>();
            UIHelper = AddSystem<UI.UIHelper>();
            UIImGui = AddSystem<UI.UIImGui>();

            system2 = new SequentialSystem<State>(
                renderSystem,
                recordSystem,
                uiRenderSystem);
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

            if (gdc.Playing || gdc.RequireResetPhysics)
            {
                animationSystem.playTime = (float)gdc.PlayTime;
                animationSystem.Update();

                particleSystem.Update();
                sceneExtensions.Update();

                gdc.RequireResetPhysics = false;
            }
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
            RPContext.FrameBegin();

            graphicsDevice.RenderBegin();
            graphicsContext.Begin();

            system2.Update(null);

            graphicsContext.EndCommand();
            graphicsContext.Execute();
            graphicsDevice.RenderComplete();

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
