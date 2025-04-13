using System;
using System.Numerics;

namespace Coocoo3DGraphics;

public class RTInstance
{
    public RTBottomLevelAccelerationStruct blas;
    public Matrix4x4 transform = Matrix4x4.Identity;
    public byte instanceMask = 0xff;

    public Action<LocalResourceProxy> SetLocalResource;
}
