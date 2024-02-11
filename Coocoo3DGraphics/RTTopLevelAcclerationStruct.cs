using System.Collections.Generic;

namespace Coocoo3DGraphics;

public class RTTopLevelAcclerationStruct
{
    public List<RTInstance> instances;
    public bool initialized;
    internal ulong GPUVirtualAddress;
}
