using System.Collections.Generic;

namespace Coocoo3DGraphics;

public class RayTracingCall
{
    public string rayGenShader;
    public string[] missShaders;

    public Dictionary<int, object> CBVs;
    public Dictionary<int, object> SRVs;
    public Dictionary<int, object> UAVs;

    public RTTopLevelAcclerationStruct tpas;

}
