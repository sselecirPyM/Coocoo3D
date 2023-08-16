﻿using Caprice.Attributes;
using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using System;

namespace RenderPipelines;

public class EnvironmentReflectionAttribute : RuntimeBakeAttribute, ITexture2DBaker
{
    public bool Bake(Texture2D texture, RenderWrap renderWrap, ref object tag)
    {
        var tex = renderWrap.GetTex2D(Source);
        if (tex == null || tex.Status != GraphicsObjectStatus.loaded)
            return false;

        int currentQuality;
        if (tag is int val)
            currentQuality = val;
        else
            currentQuality = 0;

        int width = texture.width;
        int height = texture.height;
        var writer = renderWrap.Writer;

        int roughnessLevel = 5;

        {
            int t1 = roughnessLevel + 1;
            int face = 0;

            int mipLevel = currentQuality % t1;
            int quality = currentQuality / t1;
            int pow2a = 1 << mipLevel;
            writer.Write(width / pow2a);
            writer.Write(height / pow2a);
            writer.Write(quality);
            writer.Write(quality);
            writer.Write(Math.Max(mipLevel * mipLevel / (4.0f * 4.0f), 1e-3f));
            writer.Write(face);
            writer.SetCBV(0);

            renderWrap.SetSRV(tex, 0);
            renderWrap.SetUAV(texture, mipLevel, 0);

            if (mipLevel != roughnessLevel)
                renderWrap.Dispatch("PreFilterEnv.hlsl", null, width / 8 / pow2a, height / 8 / pow2a, 6);
            else
                renderWrap.Dispatch("IrradianceMap.hlsl", null, width / 8 / pow2a, height / 8 / pow2a, 6);

            currentQuality++;
        }
        tag = currentQuality;
        if (currentQuality < 256)
            return false;
        else
            return true;
    }

    public EnvironmentReflectionAttribute(string source)
    {
        Source = source;
    }
    public string Source { get; }
}
