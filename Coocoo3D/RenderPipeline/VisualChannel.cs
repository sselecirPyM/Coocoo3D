using Coocoo3D.Numerics;
using Coocoo3D.Present;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Vortice.DXGI;
using Caprice.Attributes;

namespace Coocoo3D.RenderPipeline
{
    public class VisualChannel : IDisposable
    {
        public string Name;
        public Camera camera = new Camera();
        public CameraData cameraData;
        public Int2 outputSize = new Int2(100, 100);
        public Int2 sceneViewSize = new Int2(100, 100);
        public GraphicsContext graphicsContext;
        public Texture2D OutputRTV = new Texture2D();

        internal Dictionary<string, Texture2D> RTs = new();
        internal Dictionary<string, TextureCube> RTCs = new();
        internal Dictionary<string, GPUBuffer> dynamicBuffers = new();

        public RenderPipeline renderPipeline;
        public RenderPipelineView renderPipelineView;

        public bool newRenderPipeline;
        public Type newRenderPipelineType;
        public string newRenderPipelinePath;
        public RenderPipelineContext rpc;

        public VisualChannel()
        {
        }

        Dictionary<int, Matrix4x4> lightMatrixCaches = new();

        static float[] lightMatrixLevel = { 0.0f, 0.977f, 0.993f, 0.997f, 0.998f };

        public Matrix4x4 GetLightMatrix(DirectionalLightData directionalLight, int level)
        {
            return GetLightMatrix1(directionalLight, level, lightMatrixLevel[level], lightMatrixLevel[level + 1]);
        }

        Matrix4x4 GetLightMatrix1(DirectionalLightData directionalLight, int level, float start, float end)
        {
            Matrix4x4 pvMatrix = cameraData.pvMatrix;
            if (lightMatrixCaches.TryGetValue(level, out var mat1))
                return mat1;
            Matrix4x4 lightCameraMatrix0 = directionalLight.GetLightingMatrix(pvMatrix, start, end);
            lightMatrixCaches[level] = lightCameraMatrix0;
            return lightCameraMatrix0;
        }

        public void Onframe(RenderPipelineContext RPContext)
        {
            if (newRenderPipelineType != null)
            {
                #region
                if (renderPipeline is IDisposable disposable0)
                    disposable0.Dispose();
                renderPipelineView?.Dispose();
                #endregion

                SetRenderPipeline((RenderPipeline)Activator.CreateInstance(newRenderPipelineType),
                    rpc, newRenderPipelinePath);
                newRenderPipelineType = null;
            }


            newRenderPipeline = RPContext.NewRenderPipeline;
            lightMatrixCaches.Clear();

            if (camera.CameraMotionOn) camera.SetCameraMotion((float)RPContext.dynamicContextRead.Time);
            cameraData = camera.GetCameraData();
        }

        public void DelaySetRenderPipeline(Type type, RenderPipelineContext rpc, string basePath)
        {
            newRenderPipelinePath = basePath;
            this.rpc = rpc;
            this.newRenderPipelineType = type;
        }

        void SetRenderPipeline(RenderPipeline renderPipeline, RenderPipelineContext rpc, string basePath)
        {
            this.renderPipeline = renderPipeline;
            var renderPipelineView = new RenderPipelineView(renderPipeline, basePath);
            this.renderPipelineView = renderPipelineView;
            var renderWrap = new RenderWrap()
            {
                RenderPipelineView = renderPipelineView,
                visualChannel = this,
                rpc = rpc,
            };
            renderPipeline.renderWrap = renderWrap;
            renderPipelineView.renderWrap = renderWrap;
        }

        public void PrepareRenderTarget(PassSetting passSetting, Format outputFormat)
        {
            var OutputTex = OutputRTV;
            if (outputSize.X != OutputTex.width || outputSize.Y != OutputTex.height)
            {
                OutputTex.ReloadAsRTVUAV(outputSize.X, outputSize.Y, outputFormat);
                graphicsContext.UpdateRenderTexture(OutputTex);
            }

            if (passSetting == null) return;

            if (passSetting.RenderTargets != null)
                foreach (var rt1 in passSetting.RenderTargets)
                {
                    var rt = rt1.Value;
                    if (rt.flag.HasFlag(RenderTargetFlag.Shared)) continue;
                    (int x, int y) = GetResolution(rt);
                    RPUtil.Texture2D(RTs, rt1.Key, rt1.Value, x, y, 1, graphicsContext);
                }
            if (passSetting.RenderTargetCubes != null)
                foreach (var rt1 in passSetting.RenderTargetCubes)
                {
                    var rt = rt1.Value;
                    if (rt.flag.HasFlag(RenderTargetFlag.Shared)) continue;
                    (int x, int y) = GetResolution(rt);
                    RPUtil.TextureCube(RTCs, rt1.Key, rt1.Value, x, y, 1, graphicsContext);
                }
            if (passSetting.DynamicBuffers != null)
                foreach (var rt1 in passSetting.DynamicBuffers)
                {
                    var rt = rt1.Value;
                    if (rt.flag.HasFlag(RenderTargetFlag.Shared)) continue;
                    RPUtil.DynamicBuffer(dynamicBuffers, rt1.Key, (int)rt.width, graphicsContext);
                }
        }

        ValueTuple<int, int> GetResolution(RenderTarget rt)
        {
            int x;
            int y;
            if (rt.Source == "OutputSize")
            {
                x = (int)(outputSize.X * rt.Multiplier + 0.5f);
                y = (int)(outputSize.Y * rt.Multiplier + 0.5f);
            }
            else
            {
                x = (int)rt.width;
                y = (int)rt.height;
            }
            return (x, y);
        }

        public bool SwapBuffer(string buf1, string buf2)
        {
            if (dynamicBuffers.TryGetValue(buf1, out var buffer1) && dynamicBuffers.TryGetValue(buf2, out var buffer2))
            {
                if (buffer1.size != buffer2.size)
                    return false;
                dynamicBuffers[buf2] = buffer1;
                dynamicBuffers[buf1] = buffer2;
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool SwapTexture(string tex1, string tex2)
        {
            if (RTs.TryGetValue(tex1, out var buffer1) && RTs.TryGetValue(tex2, out var buffer2))
            {
                if (buffer1.width != buffer2.width ||
                    buffer1.height != buffer2.height ||
                    buffer1.format != buffer2.format ||
                    buffer1.dsvFormat != buffer2.dsvFormat ||
                    buffer1.rtvFormat != buffer2.rtvFormat ||
                    buffer1.uavFormat != buffer2.uavFormat)
                    return false;
                RTs[tex2] = buffer1;
                RTs[tex1] = buffer2;
                return true;
            }
            else
            {
                return false;
            }
        }

        public Texture2D GetAOV(AOVType type)
        {
            if (!newRenderPipeline)
                return OutputRTV;
            var aov = renderPipelineView?.GetAOV(type);
            if (aov != null)
                return aov;
            else
                return null;
        }

        public void Dispose()
        {
            Dispose(RTs);
            Dispose(RTCs);
            Dispose(dynamicBuffers);
            OutputRTV.Dispose();
            renderPipelineView?.Dispose();
        }

        void Dispose<T1, T2>(Dictionary<T1, T2> dictionary) where T2 : IDisposable
        {
            foreach (var pair in dictionary)
            {
                pair.Value.Dispose();
            }
        }
    }
}
