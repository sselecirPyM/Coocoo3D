using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.Present
{
    public class CameraMotion
    {
        const float c_framePerSecond = 30;
        public List<CameraKeyFrame> cameraKeyFrames;

        public CameraKeyFrame GetCameraMotion(float time)
        {
            if (cameraKeyFrames == null || cameraKeyFrames.Count == 0)
            {
                return new CameraKeyFrame() { FOV = 30, distance = 45 };
            }
            float frame = Math.Max(time * c_framePerSecond, 0);

            CameraKeyFrame ComputeKeyFrame(CameraKeyFrame _Left, CameraKeyFrame _Right)
            {
                float t = (frame - _Left.Frame) / (_Right.Frame - _Left.Frame);
                float amountX = GetAmount(_Right.mxInterpolator, t);
                float amountY = GetAmount(_Right.myInterpolator, t);
                float amountZ = GetAmount(_Right.mzInterpolator, t);
                float amountR = GetAmount(_Right.rInterpolator, t);
                float amountD = GetAmount(_Right.dInterpolator, t);
                float amountF = GetAmount(_Right.fInterpolator, t);


                CameraKeyFrame newKeyFrame = new CameraKeyFrame();
                newKeyFrame.Frame = (int)MathF.Round(frame);
                newKeyFrame.position = Lerp(_Left.position, _Right.position, new Vector3(amountX, amountY, amountZ));
                newKeyFrame.rotation = Lerp(_Left.rotation, _Right.rotation, amountR);
                newKeyFrame.distance = Lerp(_Left.distance, _Right.distance, amountD);
                newKeyFrame.FOV = Lerp(_Left.FOV, _Right.FOV, amountF);
                if (newKeyFrame.FOV < 0)
                {

                }

                return newKeyFrame;
            }

            int left = 0;
            int right = cameraKeyFrames.Count - 1;
            if (left == right) return cameraKeyFrames[left];
            if (cameraKeyFrames[right].Frame < frame) return cameraKeyFrames[right];

            while (right - left > 1)
            {
                int mid = (right + left) / 2;
                if (cameraKeyFrames[mid].Frame > frame)
                    right = mid;
                else
                    left = mid;
            }

            return ComputeKeyFrame(cameraKeyFrames[left], cameraKeyFrames[right]);
        }
        static float Lerp(float x, float y, float s)
        {
            return x * (1 - s) + y * s;
        }
        static Vector3 Lerp(Vector3 x, Vector3 y, float s)
        {
            return x * (1 - s) + y * s;
        }
        static Vector3 Lerp(Vector3 x, Vector3 y, Vector3 s)
        {
            return x * (Vector3.One - s) + y * s;
        }

        static float GetAmount(Interpolator interpolator, float _a)
        {
            if (interpolator.ax == interpolator.ay && interpolator.bx == interpolator.by)
                return _a;
            var _curve = Utility.CubicBezierCurve.Load(interpolator.ax, interpolator.ay, interpolator.bx, interpolator.by);
            return _curve.Sample(_a);
        }
    }
}
