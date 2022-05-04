using Coocoo3D.Present;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.Components
{
    public class MMDMotion
    {
        public Dictionary<string, List<BoneKeyFrame>> BoneKeyFrameSet { get; set; } = new();
        public Dictionary<string, List<MorphKeyFrame>> MorphKeyFrameSet { get; set; } = new();

        const float c_framePerSecond = 30;
        public (Vector3, Quaternion) GetBoneMotion(string key, float time)
        {
            if (!BoneKeyFrameSet.TryGetValue(key, out var keyframeSet) || keyframeSet.Count == 0)
            {
                return (Vector3.Zero, Quaternion.Identity);
            }
            if (keyframeSet.Count == 1)
                return (keyframeSet[0].translation, keyframeSet[0].rotation);
            float frame = Math.Max(time * c_framePerSecond, 0);

            int left = 0;
            int right = keyframeSet.Count - 1;
            if (keyframeSet[right].Frame < frame) return (keyframeSet[right].translation, keyframeSet[right].rotation);

            while (right - left > 1)
            {
                int mid = (right + left) / 2;
                if (keyframeSet[mid].Frame > frame)
                    right = mid;
                else
                    left = mid;
            }
            (Vector3, Quaternion) ComputeKeyFrame(in BoneKeyFrame _Left, in BoneKeyFrame _Right)
            {
                float t = (frame - _Left.Frame) / (_Right.Frame - _Left.Frame);
                float amountR = GetAmount(_Right.rInterpolator, t);
                float amountX = GetAmount(_Right.xInterpolator, t);
                float amountY = GetAmount(_Right.yInterpolator, t);
                float amountZ = GetAmount(_Right.zInterpolator, t);

                return (Lerp(_Left.translation, _Right.translation, new Vector3(amountX, amountY, amountZ)), Quaternion.Slerp(_Left.rotation, _Right.rotation, amountR));
            }
            return ComputeKeyFrame(keyframeSet[left], keyframeSet[right]);
        }

        public float GetMorphWeight(string key, float time)
        {
            if (!MorphKeyFrameSet.TryGetValue(key, out var keyframeSet))
            {
                return 0.0f;
            }
            int left = 0;
            int right = keyframeSet.Count - 1;
            float indexFrame = Math.Max(time * c_framePerSecond, 0);


            if (keyframeSet.Count == 1)
            {
                return keyframeSet[0].Weight;
            }

            if (keyframeSet[right].Frame < indexFrame)
            {
                return keyframeSet[right].Weight;
            }

            while (right - left > 1)
            {
                int mid = (right + left) / 2;
                if (keyframeSet[mid].Frame > indexFrame)
                    right = mid;
                else
                    left = mid;
            }
            MorphKeyFrame keyFrameLeft = keyframeSet[left];
            MorphKeyFrame keyFrameRight = keyframeSet[right];

            return ComputeKeyFrame(keyFrameLeft, keyFrameRight, indexFrame);
        }
        static float ComputeKeyFrame(MorphKeyFrame _left, MorphKeyFrame _right, float frame)
        {
            float amount = (float)(frame - _left.Frame) / (_right.Frame - _left.Frame);
            return Lerp(_left.Weight, _right.Weight, amount);
        }
        static float Lerp(float x, float y, float s)
        {
            return x * (1 - s) + y * s;
        }
        static Vector3 Lerp(Vector3 x, Vector3 y, float s)
        {
            return Vector3.Lerp(x, y, s);
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

