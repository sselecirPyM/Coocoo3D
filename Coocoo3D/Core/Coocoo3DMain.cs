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
using Coocoo3D.Utility;
using Coocoo3D.RenderPipeline;

namespace Coocoo3D.Core
{
    public class Coocoo3DMain : IDisposable
    {
        GraphicsDevice graphicsDevice { get => RPContext.graphicsDevice; }
        public string deviceDescription { get => graphicsDevice.GetDeviceDescription(); }
        public MainCaches mainCaches { get => RPContext.mainCaches; }

        public Scene CurrentScene;

        public List<GameObject> SelectedGameObjects = new List<GameObject>();

        public GameDriver GameDriver = new GameDriver();

        public bool RequireResize;
        public Vector2 NewSize;

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
        public GameDriverContext GameDriverContext { get => RPContext.gameDriverContext; }
        public Coocoo3DMain()
        {
            RPContext.Load();
            mainCaches._RequireRender = () => RequireRender(false);

            CurrentScene = new Scene();
            CurrentScene.physics3DScene.Initialize();
            CurrentScene.physics3DScene.SetGravitation(new Vector3(0, -9.801f, 0));
            CurrentScene.mainCaches = mainCaches;
            GameDriverContext.timeManager = timeManager;

            cancelRenderThread = new CancellationTokenSource();
            renderWorkThread = new Thread(() =>
            {
                var token = cancelRenderThread.Token;
                while (!token.IsCancellationRequested)
                {
                    timeManager.AbsoluteTimeInput(stopwatch1.ElapsedTicks);
                    if (timeManager.RealTimerCorrect("render", frameInterval, out _)) continue;

                    RenderFrame();
                    if (performanceSettings.SaveCpuPower && !RPContext.recording && !performanceSettings.VSync)
                        System.Threading.Thread.Sleep(1);
                }
            });
            renderWorkThread.IsBackground = true;
            renderWorkThread.Start();

            RequireRender();
        }
        #region Rendering

        public WidgetRenderer widgetRenderer = new WidgetRenderer();
        public UI.ImguiInput imguiInput = new UI.ImguiInput();

        public void RequireRender(bool updateEntities = false)
        {
            GameDriverContext.RequireRender(updateEntities);
        }

        public RenderPipelineContext RPContext = new RenderPipelineContext();

        public System.Diagnostics.Stopwatch stopwatch1 = System.Diagnostics.Stopwatch.StartNew();
        GraphicsContext graphicsContext { get => RPContext.graphicsContext; }
        Task RenderTask1;

        private bool RenderFrame()
        {
            double deltaTime = timeManager.GetDeltaTime();
            var gdc = GameDriverContext;
            gdc.FrameInterval = frameInterval;
            if (!GameDriver.Next(RPContext))
            {
                return false;
            }
            timeManager.RealCounter("fps", 1, out framePerSecond);

            #region Scene Simulation

            CurrentScene.DealProcessList();

            RPContext.BeginDynamicContext(CurrentScene);
            RPContext.dynamicContextWrite.Time = gdc.PlayTime;
            RPContext.dynamicContextWrite.DeltaTime = gdc.Playing ? gdc.DeltaTime : 0;
            RPContext.dynamicContextWrite.RealDeltaTime = deltaTime;

            if (CurrentScene.setTransform.Count != 0) gdc.RequireResetPhysics = true;
            if (gdc.Playing || gdc.RequireResetPhysics)
            {
                CurrentScene.Simulation(gdc.PlayTime, gdc.DeltaTime, gdc.RequireResetPhysics);
                gdc.RequireResetPhysics = false;
            }

            RPContext.dynamicContextWrite.Preprocess(CurrentScene.gameObjects);

            #endregion
            if (RenderTask1 != null && RenderTask1.Status != TaskStatus.RanToCompletion) RenderTask1.Wait();
            (RPContext.dynamicContextRead, RPContext.dynamicContextWrite) = (RPContext.dynamicContextWrite, RPContext.dynamicContextRead);
            if (RequireResize.SetFalse())
            {
                RPContext.swapChain.Resize(NewSize.X, NewSize.Y);
                graphicsDevice.WaitForGpu();
            }
            if (!RPContext.recording)
                mainCaches.OnFrame();
            RPContext.PreConfig();

            imguiInput.Update();
            UI.UIImGui.GUI(this);
            graphicsDevice.RenderBegin();
            graphicsContext.Begin();
            RPContext.UpdateGPUResource();

            HybirdRenderPipeline.BeginFrame(RPContext);
            if (performanceSettings.MultiThreadRendering)
                RenderTask1 = Task.Run(RenderFunction);
            else
                RenderFunction();

            return true;
        }
        void RenderFunction()
        {
            HybirdRenderPipeline.RenderCamera(RPContext);
            HybirdRenderPipeline.EndFrame(RPContext);

            GameDriver.AfterRender(RPContext);
            widgetRenderer.Render(RPContext, graphicsContext);
            RPContext.AfterRender();
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

        public void Resize(int width, int height)
        {
            RequireResize = true;
            NewSize = new Vector2(width, height);
        }

        public void SetWindow(IntPtr hwnd, int width, int height)
        {
            RPContext.swapChain.Initialize(graphicsDevice, hwnd, width, height);
        }
    }

    public struct PerformanceSettings
    {
        public bool MultiThreadRendering;
        public bool SaveCpuPower;
        public bool VSync;
    }
}
