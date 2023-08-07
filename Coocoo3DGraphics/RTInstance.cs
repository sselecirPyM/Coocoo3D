using System.Collections.Generic;
using System.Numerics;

namespace Coocoo3DGraphics;

public class RTInstance
{
    public RTBottomLevelAccelerationStruct accelerationStruct;
    public string hitGroupName;
    public Matrix4x4 transform = Matrix4x4.Identity;
    public Dictionary<int, object> CBVs;
    public Dictionary<int, object> SRVs;
    public Dictionary<int, object> UAVs;
    public byte instanceMask = 0xff;
}
