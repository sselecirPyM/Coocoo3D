﻿using Vortice.Direct3D12;

namespace Coocoo3DGraphics;

public class RTBottomLevelAccelerationStruct
{
    internal ulong GPUVirtualAddress;

    public int vertexStart;
    public int vertexCount;
    public int indexStart;
    public int indexCount;
    public Mesh mesh;
    internal RaytracingAccelerationStructurePrebuildInfo prebuildInfo;
}
