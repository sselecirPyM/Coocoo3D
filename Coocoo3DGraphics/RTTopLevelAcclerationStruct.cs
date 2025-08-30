using System.Collections.Generic;

namespace Coocoo3DGraphics;

public class RTTopLevelAcclerationStruct
{
    public List<RTBottomLevelAccelerationStruct> bottomLevels;
    public List<RTInstance> instances;
    internal ulong GPUVirtualAddress;
}
