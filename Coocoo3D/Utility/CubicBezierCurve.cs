using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;

namespace Coocoo3D.Utility
{
    public struct CubicBezierCurve//使用struct避免gc
    {
        public static CubicBezierCurve Load(float p1x, float p1y, float p2x, float p2y)
        {
            CubicBezierCurve curve = new CubicBezierCurve();
            curve.Reload(p1x, p1y, p2x, p2y);
            return curve;
        }

        public void Reload(float p1x, float p1y, float p2x, float p2y)
        {
            cx = 3.0f * p1x;
            bx = 3.0f * (p2x - p1x) - cx;
            ax = 1.0f - cx - bx;

            cy = 3.0f * p1y;
            by = 3.0f * (p2y - p1y) - cy;
            ay = 1.0f - cy - by;
        }

        public static CubicBezierCurve Load(Vector2 p1, Vector2 p2)
        {
            CubicBezierCurve curve = new CubicBezierCurve();
            curve.Reload(p1, p2);
            return curve;
        }

        public void Reload(Vector2 p1, Vector2 p2)
        {
            Vector2 v12 = p2 - p1;
            cx = 3.0f * p1.X;
            bx = 3.0f * v12.X - 3.0f * p1.X;
            ax = 1.0f - 3.0f * v12.X;

            cy = 3.0f * p1.Y;
            by = 3.0f * v12.Y - 3.0f * p1.Y;
            ay = 1.0f - 3.0f * v12.Y;
        }

        float SampleCurveX(float t)
        {
            return ((ax * t + bx) * t + cx) * t;
        }

        float SampleCurveY(float t)
        {
            return ((ay * t + by) * t + cy) * t;
        }

        float SampleCurveDerivativeX(float t)
        {
            return (3.0f * ax * t + 2.0f * bx) * t + cx;
        }

        float SolveCurveX(float x, float epsilon)
        {
            float t0;
            float t1;
            float t2;
            float x2;
            float d2;
            int i;

            for (t2 = x, i = 0; i < 8; i++)
            {
                x2 = SampleCurveX(t2) - x;
                if (MathF.Abs(x2) < epsilon)
                    return t2;
                d2 = SampleCurveDerivativeX(t2);
                if (MathF.Abs(d2) < 1e-6)
                    break;
                t2 = t2 - x2 / d2;
            }

            t0 = 0.0f;
            t1 = 1.0f;
            t2 = x;

            if (t2 < t0) return t0;
            if (t2 > t1) return t1;

            while (t0 < t1)
            {
                x2 = SampleCurveX(t2);
                if (MathF.Abs(x2 - x) < epsilon)
                    return t2;
                if (x > x2)
                    t0 = t2;
                else
                    t1 = t2;
                t2 = (t1 - t0) * 0.5f + t0;
            }

            return t2;
        }

        public float Sample(float x)
        {
            return SampleCurveY(SolveCurveX(x, 1e-6f));
        }

        float ax;
        float bx;
        float cx;
        float ay;
        float by;
        float cy;
    }
}
