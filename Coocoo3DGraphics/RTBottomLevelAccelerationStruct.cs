﻿using System;
using Vortice.Direct3D12;

namespace Coocoo3DGraphics;

public class RTBottomLevelAccelerationStruct : IDisposable
{
    public void Dispose()
    {
        resource?.Release();
        resource = null;
    }

    public bool initialized = false;
    public int vertexStart;
    public int vertexCount;
    public int indexStart;
    public int indexCount;
    public Mesh mesh;
    public ID3D12Resource resource;
    internal int size;
}
