namespace Coocoo3DGraphics;

public class CBuffer
{
    public int size;
    public ulong gpuRefAddress;

    public ulong GetCurrentVirtualAddress()
    {
        return gpuRefAddress;
    }
}
