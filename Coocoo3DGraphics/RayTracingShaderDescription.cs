using System.Collections.Generic;

namespace Coocoo3DGraphics;

public struct SlotRes
{
    public int Index;
    public SlotResourceFlag Flags;
}
public enum SlotResourceFlag
{
    None = 0,
    Linear = 1,
}

public class RayTracingShaderDescription
{
    public string name;
    public List<SlotRes> CBVs;
    public List<SlotRes> SRVs;
    public List<SlotRes> UAVs;

    public string anyHit;
    public string closestHit;
    public string intersection;
}
