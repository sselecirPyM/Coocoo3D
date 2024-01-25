using System.Numerics;

namespace RenderPipelines;

public class DirectionalLightData
{
    public Vector3 Direction;
    public Vector3 Color;
    public Quaternion Rotation;

    public Matrix4x4 GetLightingMatrix(Matrix4x4 cameraInvert, float start, float end)
    {
        Matrix4x4 rotateMatrix = Matrix4x4.CreateFromQuaternion(Rotation);
        Matrix4x4.Invert(rotateMatrix, out Matrix4x4 iRot);
        Vector4 v1x = Vector4.Transform(new Vector4(-1, -1, start, 1), cameraInvert);
        Vector3 v2x = new Vector3(v1x.X / v1x.W, v1x.Y / v1x.W, v1x.Z / v1x.W);
        Vector3 v3x = Vector3.Transform(v2x, iRot);
        Vector3 whMin = v3x;
        Vector3 whMax = v3x;

        for (int i = -1; i <= 1; i += 2)
            for (int j = -1; j <= 1; j += 2)
                for (int k = 0; k <= 1; k += 1)
                {
                    Vector4 v1 = Vector4.Transform(new Vector4(i * 0.95f, j * 0.8f, ((k == 0) ? start : end), 1), cameraInvert);
                    Vector3 v2 = new Vector3(v1.X / v1.W, v1.Y / v1.W, v1.Z / v1.W);
                    Vector3 v3 = Vector3.Transform(v2, iRot);
                    whMin = Vector3.Min(v3, whMin);
                    whMax = Vector3.Max(v3, whMax);
                }

        Vector3 size = whMax - whMin;

        var offset = Vector3.Transform(-Vector3.UnitZ * 64, rotateMatrix);
        var target = Vector3.Transform((whMax + whMin) * 0.5f, rotateMatrix);
        var up = Vector3.Normalize(Vector3.Transform(Vector3.UnitY, rotateMatrix));
        return Matrix4x4.CreateLookAt(target + offset, target, up) * Matrix4x4.CreateOrthographic(size.X, size.Y, 0.0f, 128);
    }
}
