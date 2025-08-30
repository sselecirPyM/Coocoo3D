using Coocoo3D.Core;
using System;

namespace Coocoo3D.RenderPipeline;

public class RenderPipelineContext : IDisposable
{
    public Scene scene;

    public bool recording = false;

    public double Time;
    public double DeltaTime;
    public double RealDeltaTime;
    public CameraData CameraData;

    public void Dispose()
    {
    }
}
