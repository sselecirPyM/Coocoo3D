using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ProRendererWrap.RPRHelper;
using FireRender.AMD.RenderEngine.Core;
using System.Numerics;
using System.Runtime.InteropServices;

namespace ProRendererWrap
{
    public class RPRCamera : IDisposable
    {

        public RPRCamera(RPRContext context)
        {
            this.Context = context;
            Check(Rpr.ContextCreateCamera(context._handle, out _handle));
        }

        public void LookAt(float posx, float posy, float posz, float atx, float aty, float atz, float upx, float upy, float upz)
        {
            Check(Rpr.CameraLookAt(_handle, posx, posy, posz, atx, aty, atz, upx, upy, upz));
        }

        public void LookAt(Vector3 pos, Vector3 at, Vector3 up)
        {
            Check(Rpr.CameraLookAt(_handle, pos.X, pos.Y, pos.Z, at.X, at.Y, at.Z, up.X, up.Y, up.Z));
        }

        public void SetExposure(float exposure)
        {
            Check(Rpr.CameraSetExposure(_handle, exposure));
        }

        public void SetFocalDistance(float fdist)
        {
            Check(Rpr.CameraSetFocusDistance(_handle, fdist));
        }

        public void SetFocalLength(float flength)
        {
            Check(Rpr.CameraSetFocalLength(_handle, flength));
        }

        public void SetFStop(float fstop)
        {
            Check(Rpr.CameraSetFStop(_handle, fstop));
        }

        public void SetFarPlane(float far)
        {
            Check(Rpr.CameraSetFarPlane(_handle, far));
        }

        public void SetNearPlane(float near)
        {
            Check(Rpr.CameraSetNearPlane(_handle, near));
        }

        public void SetMode(Rpr.CameraMode mode)
        {
            Check(Rpr.CameraSetMode(_handle, mode));
        }

        public void SetSensorSize(float width, float height)
        {
            Check(Rpr.CameraSetSensorSize(_handle, width, height));
        }

        public unsafe void SetTransform(Matrix4x4 matrix)
        {
            float* data1 = stackalloc float[16];
            MatrixToFloatArray(matrix, new Span<float>(data1, 16));

            Check(rprCameraSetTransform(_handle, false, data1));
        }

        [DllImport(dllName)] unsafe static extern Rpr.Status rprCameraSetTransform(IntPtr camera, bool transpose, float* transform);

        public IntPtr _handle;
        public RPRContext Context { get; }
        public void Dispose()
        {
            Rpr.ObjectDelete(ref _handle);
        }
    }
}
