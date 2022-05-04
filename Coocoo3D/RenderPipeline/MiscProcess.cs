using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Coocoo3D.RenderPipeline.Wrap;
using Coocoo3DGraphics;

namespace Coocoo3D.RenderPipeline
{
    public static class MiscProcess
    {
        public static void Process(RenderPipelineContext rp, GPUWriter gpuWriter)
        {
            int currentQuality = rp.GetPersistentValue("CurrentSkyBoxQuality", 0);

            if (rp.SkyBoxChanged || currentQuality < rp.dynamicContextRead.settings.SkyBoxMaxQuality)
            {
                var mainCaches = rp.mainCaches;
                GraphicsContext graphicsContext = rp.graphicsContext;

                Texture2D texOri = mainCaches.GetTextureLoaded(rp.skyBoxTex, rp.graphicsContext);
                mainCaches.GetSkyBox(rp.skyBoxName, rp.graphicsContext, out var texSkyBox, out var texReflect);
                int roughnessLevel = 5;

                var rootSignature = mainCaches.GetRootSignature("Csu");

                graphicsContext.SetRootSignature(rootSignature);

                if (rp.SkyBoxChanged)
                {
                    rp.SkyBoxChanged = false;
                    currentQuality = 0;

                    graphicsContext.SetPSO(mainCaches.GetComputeShader("Shaders/G_ConvertToCube.hlsl"));
                    gpuWriter.Write(texSkyBox.width);
                    gpuWriter.Write(texSkyBox.height);
                    gpuWriter.SetBufferComputeImmediately(0);
                    graphicsContext.SetSRVTSlot(texOri, 0);
                    graphicsContext.SetUAVTSlot(texSkyBox, 0, 0);
                    graphicsContext.Dispatch((int)(texSkyBox.width + 7) / 8, (int)(texSkyBox.height + 7) / 8, 6);
                }
                if (currentQuality < texSkyBox.mipLevels - 1)
                {
                    int pow2a;
                    for (int j = currentQuality * 2 + 1; j < currentQuality * 2 + 3 && j < texSkyBox.mipLevels; j++)
                    {
                        graphicsContext.SetPSO(mainCaches.GetComputeShader("Shaders/G_GenerateCubeMipMap.hlsl"));
                        pow2a = 1 << j;
                        graphicsContext.SetSRVTLim(texSkyBox, j, 0);
                        graphicsContext.SetUAVTSlot(texSkyBox, j, 0);
                        gpuWriter.Write(texSkyBox.width / pow2a);
                        gpuWriter.Write(texSkyBox.height / pow2a);
                        gpuWriter.Write(j - 1);
                        gpuWriter.SetBufferComputeImmediately(0);
                        graphicsContext.Dispatch((int)(texSkyBox.width + 7) / 8 / pow2a, (int)(texSkyBox.height + 7) / 8 / pow2a, 6);
                    }
                }
                {
                    int t1 = roughnessLevel + 1;
                    int face = currentQuality % (t1 * 6) / t1;//0-5

                    int mipLevel = currentQuality % t1;
                    int quality = currentQuality / (t1 * 6);

                    if (mipLevel != roughnessLevel)
                        graphicsContext.SetPSO(mainCaches.GetComputeShader("Shaders/G_PreFilterEnv.hlsl"));
                    else
                        graphicsContext.SetPSO(mainCaches.GetComputeShader("Shaders/G_IrradianceMap0.hlsl"));
                    int pow2a = 1 << mipLevel;
                    gpuWriter.Write(texReflect.width / pow2a);
                    gpuWriter.Write(texReflect.height / pow2a);
                    gpuWriter.Write(quality);
                    gpuWriter.Write(quality);
                    gpuWriter.Write(Math.Max(mipLevel * mipLevel / (4.0f * 4.0f), 1e-3f));
                    gpuWriter.Write(face);
                    gpuWriter.SetBufferComputeImmediately(0);

                    graphicsContext.SetSRVTSlot(texSkyBox, 0);
                    graphicsContext.SetUAVTSlot(texReflect, mipLevel, 0);
                    graphicsContext.Dispatch((int)(texReflect.width + 3) / 4 / pow2a, (int)(texReflect.height + 3) / 4 / pow2a, 1);
                }
                rp.SetPersistentValue("CurrentSkyBoxQuality", currentQuality + 1);
            }
        }
    }
}
