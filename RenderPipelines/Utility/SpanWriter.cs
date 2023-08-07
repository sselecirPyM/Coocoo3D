using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace RenderPipelines.Utility;

public ref struct SpanWriter<T>
{
    public Span<T> dest;
    public int currentPosition;

    public SpanWriter(Span<T> dest)
    {
        this.dest = dest;
        currentPosition = 0;
    }

    public void Write(ReadOnlySpan<T> data)
    {
        data.CopyTo(dest.Slice(currentPosition));
        currentPosition += data.Length;
    }

    public void Write(IEnumerator<T> data)
    {
        while (data.MoveNext())
        {
            dest[currentPosition++] = data.Current;
        }
    }

    public void Write(T data)
    {
        dest[currentPosition++] = data;
    }
}
static class SpanWriter
{
    public static SpanWriter<T> New<T>(Span<byte> source) where T : unmanaged
    {
        return new SpanWriter<T>(MemoryMarshal.Cast<byte, T>(source));
    }
}
