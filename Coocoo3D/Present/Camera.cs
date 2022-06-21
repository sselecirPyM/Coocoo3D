using Coocoo3D.RenderPipeline;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.Present
{
    public class Camera
    {
        public Vector3 LookAtPoint = new Vector3(0, 1, 0);
        public float Distance = -4.5f;
        public Vector3 Angle;
        public float Fov = 0.6632251157578452f;//38 degree
        public float AspectRatio = 1;
        public float farClip = 300.0f;
        public float nearClip = 0.2f;
        public CameraMotion cameraMotion = new CameraMotion();
        public bool CameraMotionOn = false;

        public void SetCameraMotion(float time)
        {
            var keyFrame = cameraMotion.GetCameraMotion(time);
            Distance = keyFrame.distance;
            Angle = keyFrame.rotation;
            Fov = Math.Clamp(keyFrame.FOV, 0.1f, 179.9f) / 180 * MathF.PI;
            LookAtPoint = keyFrame.position;
        }

        public void RotateDelta(Vector3 delta)
        {
            Angle += delta;
        }

        public void MoveDelta(Vector3 delta)
        {
            Matrix4x4 rotateMatrix = Matrix4x4.CreateFromYawPitchRoll(-Angle.Y, -Angle.X, -Angle.Z);
            LookAtPoint += Vector3.Transform(delta, rotateMatrix);
        }
        public CameraData GetCameraData()
        {
            Matrix4x4 rotateMatrix = Matrix4x4.CreateFromYawPitchRoll(-Angle.Y, -Angle.X, -Angle.Z);
            var position = Vector3.Transform(Vector3.UnitZ * Distance, rotateMatrix * Matrix4x4.CreateTranslation(LookAtPoint));
            var up = Vector3.Normalize(Vector3.Transform(Vector3.UnitY, rotateMatrix));
            Matrix4x4 vMatrix = Matrix4x4.CreateLookAt(position, LookAtPoint, up);
            float nearClip1 = MathF.Max(nearClip, 0.001f);
            float farClip1 = MathF.Max(farClip, nearClip1 + 1e-1f);
            float fov1 = Math.Clamp(Fov, 1e-3f, MathF.PI - 1e-3f);
            Matrix4x4 pMatrix = Matrix4x4.CreatePerspectiveFieldOfView(fov1, AspectRatio, nearClip1, farClip1);
            Matrix4x4 vpMatrix = Matrix4x4.Multiply(vMatrix, pMatrix);
            Matrix4x4.Invert(vpMatrix, out Matrix4x4 pvMatrix);
            return new CameraData()
            {
                Angle = Angle,
                AspectRatio = AspectRatio,
                Distance = Distance,
                Fov = Fov,
                LookAtPoint = LookAtPoint,
                Position = position,
                vMatrix = vMatrix,
                pMatrix = pMatrix,
                vpMatrix = vpMatrix,
                pvMatrix = pvMatrix,
                far = farClip1,
                near = nearClip1,
            };
        }
    }
}
