using Coocoo3DGraphics.Commanding;
using System;

namespace Coocoo3DGraphics;

public class RayTracingCall
{
    public string rayGenShader;
    public string[] missShaders;

    public RTTopLevelAcclerationStruct tlas;

    public Action<ComputeCommandProxy> SetResources;
}
