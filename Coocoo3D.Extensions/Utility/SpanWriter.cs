using System;
using System.Collections.Generic;

namespace Coocoo3D.Extensions.Utility
{
    ref struct SpanWriter<T>
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
}
