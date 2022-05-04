using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ProRendererWrap.RPRHelper;
using FireRender.AMD.RenderEngine.Core;
using System.Runtime.InteropServices;

namespace ProRendererWrap
{
    public class RPRFrameBuffer : IDisposable
    {
        public unsafe RPRFrameBuffer(RPRContext context, Rpr.FramebufferFormat format, Rpr.FrameBufferDesc desc)
        {
            this.Context = context;
            Check(Rpr.ContextCreateFrameBuffer(context._handle, format, new IntPtr(&desc), out _handle));
        }

        public void SaveToFile(string filePath)
        {
            Check(Rpr.FrameBufferSaveToFile(_handle, filePath));
        }

        public void GetInfo(Rpr.FrameBuffer info, byte[] data, out int size)
        {
            var ptr = Marshal.UnsafeAddrOfPinnedArrayElement(data, 0);
            Check(Rpr.FrameBufferGetInfo(_handle, info, data.LongLength, ptr, out long size1));
            size = (int)size1;
        }

        public void Clear()
        {
            Check(Rpr.FrameBufferClear(_handle));
        }

        public IntPtr _handle;
        public RPRContext Context { get; }
        public void Dispose()
        {
            Rpr.ObjectDelete(ref _handle);
        }
    }
}
