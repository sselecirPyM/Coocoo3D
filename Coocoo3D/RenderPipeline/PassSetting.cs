using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Coocoo3DGraphics;
using Vortice.DXGI;
using System.Numerics;
using System.IO;

namespace Coocoo3D.RenderPipeline
{
    public class PassSetting
    {
        public string Name;
        public List<RenderSequence> RenderSequence;
        public Dictionary<string, RenderTarget> RenderTargets;
        public Dictionary<string, RenderTarget> RenderTargetCubes;
        public Dictionary<string, RenderTarget> DynamicBuffers;
        public Dictionary<string, UnionPass> Passes;
        public Dictionary<string, string> Texture2Ds;
        public Dictionary<string, string> UnionShaders;
        public Dictionary<string, string> RayTracingShaders;
        public Dictionary<string, string> ShowTextures;
        public Dictionary<string, PassParameter> ShowParameters;
        public Dictionary<string, string> ShowSettingTextures;
        public Dictionary<string, PassParameter> ShowSettingParameters;
        public string Dispatcher;

        [NonSerialized]
        public string path;

        [NonSerialized]
        public Dictionary<string, string> aliases = new Dictionary<string, string>();

        public string GetAliases(string input)
        {
            if (input == null)
                return null;
            if (aliases.TryGetValue(input, out string s))
                return s;
            return input;
        }

        [NonSerialized]
        public bool loaded;

        public bool Verify()
        {
            if (RenderTargets == null)
                return false;
            if (RenderSequence == null)
                return false;
            if (Passes == null)
                return false;
            foreach (var pass in Passes)
            {
                pass.Value.Name = pass.Key;
            }

            if (ShowParameters != null)
                foreach (var parameter in ShowParameters)
                {
                    parameter.Value.Name ??= parameter.Key;
                    parameter.Value.GenerateRuntimeValue();
                }
            if (ShowSettingParameters != null)
                foreach (var parameter in ShowSettingParameters)
                {
                    parameter.Value.Name ??= parameter.Key;
                    parameter.Value.GenerateRuntimeValue();
                }
            foreach (var passMatch in RenderSequence)
            {
                if (passMatch.Name != null && Passes.ContainsKey(passMatch.Name))
                {
                }
                else
                    return false;
            }

            return true;
        }

        public bool Initialize()
        {
            if (loaded) return true;
            if (!Verify()) return false;
            string path1 = Path.GetDirectoryName(path);

            if (RayTracingShaders != null)
                foreach (var shader in RayTracingShaders)
                    aliases[shader.Key] = Path.GetFullPath(shader.Value, path1);

            Dictionary<string, string> unionShaderAliases = new Dictionary<string, string>();
            if (UnionShaders != null)
                foreach (var shader in UnionShaders)
                    unionShaderAliases[shader.Key] = Path.GetFullPath(shader.Value, path1);

            foreach (var pass in Passes)
            {
                if (!unionShaderAliases.TryGetValue(pass.Value.UnionShader, out var val))
                    pass.Value.UnionShader = Path.GetFullPath(pass.Value.UnionShader, path1);
                else
                    pass.Value.UnionShader = val;
            }

            if (Texture2Ds != null)
                foreach (var texture in Texture2Ds)
                    aliases[texture.Key] = Path.GetFullPath(texture.Value, path1);

            if (Dispatcher != null)
                Dispatcher = Path.GetFullPath(Dispatcher, path1);
            else
                Console.WriteLine("Missing dispacher.");
            foreach (var sequence in RenderSequence)
            {
                var pass = Passes[sequence.Name];
                int SlotComparison(SlotRes x1, SlotRes y1)
                {
                    return x1.Index.CompareTo(y1.Index);
                }
                StringBuilder stringBuilder = new StringBuilder();
                pass.CBVs?.Sort(SlotComparison);
                pass.SRVs?.Sort(SlotComparison);
                pass.UAVs?.Sort(SlotComparison);

                if (pass.CBVs != null)
                {
                    int count = 0;
                    foreach (var cbv in pass.CBVs)
                    {
                        for (int i = count; i < cbv.Index + 1; i++)
                            stringBuilder.Append('C');
                        count = cbv.Index + 1;
                    }
                }
                if (pass.SRVs != null)
                {
                    int count = 0;
                    foreach (var srv in pass.SRVs)
                    {
                        for (int i = count; i < srv.Index + 1; i++)
                            stringBuilder.Append('s');
                        count = srv.Index + 1;
                    }
                }
                if (pass.UAVs != null)
                {
                    int count = 0;
                    foreach (var uav in pass.UAVs)
                    {
                        for (int i = count; i < uav.Index + 1; i++)
                            stringBuilder.Append('u');
                        count = uav.Index + 1;
                    }
                }
                sequence.rootSignatureKey = stringBuilder.ToString();
            }
            loaded = true;
            return true;
        }
    }
    public class RenderSequence
    {
        public string Name;
        public int DepthBias;
        public float SlopeScaledDepthBias;
        public string Type;
        public List<string> RenderTargets;

        public string DepthStencil;
        public bool ClearDepth;
        public bool ClearRenderTarget;
        public CullMode CullMode;

        [NonSerialized]
        public string rootSignatureKey;
    }
    public class UnionPass
    {
        public string Name;
        public string VertexShader;
        public string GeometryShader;
        public string PixelShader;
        public string ComputeShader;
        public string UnionShader;
        public string RayTracingShader;
        public BlendState BlendMode;
        public Dictionary<string, string> Properties;
        public List<SlotRes> CBVs;
        public List<SlotRes> SRVs;
        public List<SlotRes> UAVs;
    }
    [Flags]
    public enum RenderTargetFlag
    {
        None = 0,
        Shared = 1,
    }

    public class RenderTarget
    {
        [DefaultValue(1.0f)]
        public float width = 1;
        [DefaultValue(1.0f)]
        public float height = 1;
        [DefaultValue(1.0f)]
        public float depth = 1;

        [DefaultValue(1.0f)]
        public float Multiplier = 1.0f;

        public string Source;

        public Format Format;
        public RenderTargetFlag flag;
    }
    public class PassParameter
    {
        public string Name;
        public string Type;
        public string Default;
        public string Min;
        public string Max;
        public string Step;
        public string Format;
        public bool IsHidden;
        public string Tooltip;
        [NonSerialized]
        public object defaultValue;
        [NonSerialized]
        public object minValue;
        [NonSerialized]
        public object maxValue;
        [NonSerialized]
        public object step;

        public void GenerateRuntimeValue()
        {
            switch (Type)
            {
                case "float":
                case "sliderFloat":
                    float f1;
                    if (float.TryParse(Default, out f1))
                        defaultValue = f1;
                    else
                        defaultValue = default(float);

                    FloatDefaultSettings();
                    break;
                case "float2":
                    defaultValue = Utility.StringConvert.GetFloat2(Default);

                    FloatDefaultSettings();
                    break;
                case "float3":
                case "color3":
                    defaultValue = Utility.StringConvert.GetFloat3(Default);

                    FloatDefaultSettings();
                    break;
                case "float4":
                case "color4":
                    defaultValue = Utility.StringConvert.GetFloat4(Default);

                    FloatDefaultSettings();
                    break;
                case "int":
                case "sliderInt":
                    int i1;
                    if (int.TryParse(Default, out i1))
                        defaultValue = i1;
                    else
                        defaultValue = default(int);

                    IntDefaultSettings();
                    break;
                case "bool":
                    if (bool.TryParse(Default, out bool b1))
                        defaultValue = b1;
                    else defaultValue = default(bool);
                    break;
            }
        }

        void FloatDefaultSettings()
        {
            float f1;
            if (float.TryParse(Min, out f1))
                minValue = f1;
            else
                minValue = float.MinValue;

            if (float.TryParse(Max, out f1))
                maxValue = f1;
            else
                maxValue = float.MaxValue;
            if (float.TryParse(Step, out f1))
                step = f1;
            else
                step = 1.0f;

            Format ??= "%.3f";
        }

        void IntDefaultSettings()
        {
            int i1;
            if (int.TryParse(Min, out i1))
                minValue = i1;
            else
                minValue = int.MinValue;

            if (int.TryParse(Max, out i1))
                maxValue = i1;
            else
                maxValue = int.MaxValue;

            if (int.TryParse(Step, out i1))
                step = i1;
            else
                step = 1;
        }
    }
}
