using System.Numerics;

namespace Coocoo3D.RenderPipeline;

public struct CameraData
{
    public Matrix4x4 vMatrix;
    public Matrix4x4 pMatrix;
    public Matrix4x4 vpMatrix;
    public Matrix4x4 pvMatrix;
    public Vector3 LookAtPoint;
    public float Distance;
    public Vector3 Angle;
    public float Fov;
    public float AspectRatio;
    public Vector3 Position;
    public float far;
    public float near;

    public CameraData GetJitter(Vector2 offset)
    {
        CameraData cameraData = (CameraData)MemberwiseClone();

        cameraData.pMatrix.M31 += offset.X;
        cameraData.pMatrix.M32 += offset.Y;
        cameraData.vpMatrix = Matrix4x4.Multiply(cameraData.vMatrix, cameraData.pMatrix);
        Matrix4x4.Invert(cameraData.vpMatrix, out cameraData.pvMatrix);

        return cameraData;
    }
}
