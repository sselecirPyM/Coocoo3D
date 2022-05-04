using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ProRendererWrap.RPRHelper;
using FireRender.AMD.RenderEngine.Core;
using System.Numerics;
using System.Buffers;
using System.Runtime.InteropServices;

namespace ProRendererWrap
{
    public class RPRLight : IDisposable
    {
        public static RPRLight PointLight(RPRContext context)
        {
            var light = new RPRLight(context);
            Check(Rpr.ContextCreatePointLight(context._handle, out light._handle));
            return light;
        }
        public static RPRLight EnvLight(RPRContext context)
        {
            var light = new RPRLight(context);
            Check(Rpr.ContextCreateEnvironmentLight(context._handle, out light._handle));
            return light;
        }
        public static RPRLight SkyLight(RPRContext context)
        {
            var light = new RPRLight(context);
            Check(Rpr.ContextCreateSkyLight(context._handle, out light._handle));
            return light;
        }
        public static RPRLight SphereLight(RPRContext context)
        {
            var light = new RPRLight(context);
            Check(Rpr.ContextCreateSphereLight(context._handle, out light._handle));
            return light;
        }
        public static RPRLight DirectionalLight(RPRContext context)
        {
            var light = new RPRLight(context);
            Check(Rpr.ContextCreateDirectionalLight(context._handle, out light._handle));
            return light;
        }
        public static RPRLight DiskLight(RPRContext context)
        {
            var light = new RPRLight(context);
            Check(Rpr.ContextCreateDiskLight(context._handle, out light._handle));
            return light;
        }
        public static RPRLight SpotLight(RPRContext context)
        {
            var light = new RPRLight(context);
            Check(Rpr.ContextCreateSpotLight(context._handle, out light._handle));
            return light;
        }

        RPRLight(RPRContext context)
        {
            this.Context = context;
        }

        public void DirectionalLightSetRadiantPower3f(float r, float g, float b)
        {
            Check(Rpr.DirectionalLightSetRadiantPower3f(_handle, r, g, b));
        }

        public void DirectionalLightSetShadowSoftnessAngle(float softnessAngle)
        {
            Check(Rpr.DirectionalLightSetShadowSoftnessAngle(_handle, softnessAngle));
        }

        public void DiskLightSetAngle(float angle)
        {
            Check(Rpr.DiskLightSetAngle(_handle, angle));
        }

        public void DiskLightSetInnerAngle(float angle)
        {
            Check(Rpr.DiskLightSetInnerAngle(_handle, angle));
        }

        public void DiskLightSetRadiantPower3f(float r, float g, float b)
        {
            Check(Rpr.DiskLightSetRadiantPower3f(_handle, r, g, b));
        }

        public void DiskLightSetRadius(float radius)
        {
            Check(Rpr.DiskLightSetRadius(_handle, radius));
        }

        public void EnvironmentLightSetImage(RPRImage image)
        {
            Check(Rpr.EnvironmentLightSetImage(_handle, image._handle));
        }

        public void EnvironmentLightSetIntensityScale(float scale)
        {
            Check(Rpr.EnvironmentLightSetIntensityScale(_handle, scale));
        }

        public void PointLightSetRadiantPower3f(float r, float g, float b)
        {
            Check(Rpr.PointLightSetRadiantPower3f(_handle, r, g, b));
        }

        public void SpotLightSetImage(RPRImage imge)
        {
            Check(Rpr.SpotLightSetImage(_handle, imge._handle));
        }

        public void SphereLightSetRadiantPower3f(float r, float g, float b)
        {
            Check(Rpr.SphereLightSetRadiantPower3f(_handle, r, g, b));
        }

        public void SphereLightSetRadius(float radius)
        {
            Check(Rpr.SphereLightSetRadius(_handle, radius));
        }

        public void SpotLightSetRadiantPower3f(float r, float g, float b)
        {
            Check(Rpr.SpotLightSetRadiantPower3f(_handle, r, g, b));
        }

        public void SpotLightSetConeShape(float iangle, float oangle)
        {
            Check(Rpr.SpotLightSetConeShape(_handle, iangle, oangle));
        }

        public unsafe void SetTransform(Matrix4x4 matrix)
        {
            float* data1 = stackalloc float[16];
            MatrixToFloatArray(matrix, new Span<float>(data1, 16));
            rprLightSetTransform(_handle, false, data1);
        }

        public IntPtr _handle;
        public RPRContext Context { get; }
        public void Dispose()
        {
            Rpr.ObjectDelete(ref _handle);
        }

        [DllImport(dllName)] unsafe static extern Rpr.Status rprLightSetTransform(IntPtr light, bool transpose, float* transform);
    }
}
