using System;
using System.Numerics;

namespace Coocoo3D.Present;

public struct BoneKeyFrame : IComparable<BoneKeyFrame>
{
    public int Frame { get; set; }
    public Vector3 Translation { get => translation; set => translation = value; }
    public Vector3 translation;
    public Quaternion Rotation { get => rotation; set => rotation = value; }
    public Quaternion rotation;
    public Interpolator xInterpolator;
    public Interpolator yInterpolator;
    public Interpolator zInterpolator;
    public Interpolator rInterpolator;

    public int CompareTo(BoneKeyFrame other)
    {
        return Frame.CompareTo(other.Frame);
    }
}

public struct MorphKeyFrame : IComparable<MorphKeyFrame>
{
    public int Frame;
    public float Weight;

    public int CompareTo(MorphKeyFrame other)
    {
        return Frame.CompareTo(other.Frame);
    }
}

public struct CameraKeyFrame : IComparable<CameraKeyFrame>
{
    public int Frame;
    public float distance;
    public Vector3 position;
    public Vector3 rotation;
    public Interpolator mxInterpolator;
    public Interpolator myInterpolator;
    public Interpolator mzInterpolator;
    public Interpolator rInterpolator;
    public Interpolator dInterpolator;
    public Interpolator fInterpolator;
    public float FOV;
    public bool orthographic;

    public int CompareTo(CameraKeyFrame other)
    {
        return Frame.CompareTo(other.Frame);
    }
}

public struct LightKeyFrame : IComparable<LightKeyFrame>
{
    public int Frame;
    public Vector3 Color;
    public Vector3 Position;

    public int CompareTo(LightKeyFrame other)
    {
        return Frame.CompareTo(other.Frame);
    }
}

public struct IKKeyFrame : IComparable<IKKeyFrame>
{
    public int Frame;
    public bool enable;

    public int CompareTo(IKKeyFrame other)
    {
        return Frame.CompareTo(other);
    }
}

public struct BoneKeyFrame1
{
    public Vector3 Position;
    public bool EnableIK;
    public Quaternion Rotation;

    public BoneKeyFrame1(Vector3 position, Quaternion rotation, bool enableIK)
    {
        this.Position = position;
        this.Rotation = rotation;
        this.EnableIK = enableIK;
    }
}

public struct Interpolator
{
    public float ax;
    public float bx;
    public float ay;
    public float by;
}
