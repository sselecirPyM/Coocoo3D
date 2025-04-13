using System;

namespace Coocoo3DGraphics;

public class RayTracingCall
{
    public string rayGenShader;
    public string[] missShaders;

    public RTTopLevelAcclerationStruct tpas;

    public Action<ComputeResourceProxy> SetResources;
}
