using Coocoo3D.ResourceWrap;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.RenderPipeline
{
    public class Recorder : IDisposable
    {
        public ReadBackTexture2D ReadBackTexture2D = new ReadBackTexture2D();

        public GraphicsContext graphicsContext;

        public GraphicsDevice graphicsDevice;

        public void OnFrame()
        {
            if (recordQueue.Count == 0) return;
            ulong completed = graphicsDevice.GetInternalCompletedFenceValue();
            int width = ReadBackTexture2D.GetWidth();
            int height = ReadBackTexture2D.GetHeight();
            while (recordQueue.Count > 0 && recordQueue.Peek().Item1 <= completed)
            {
                var triple = recordQueue.Dequeue();

                var data = ReadBackTexture2D.StartRead<byte>(triple.Item2);
                TextureHelper.SaveToFile(data, width, height, triple.Item3);
                ReadBackTexture2D.StopRead(triple.Item2);
            }
        }

        int index1;

        Queue<ValueTuple<ulong, int, string>> recordQueue = new();

        public void Record(Texture2D texture, string output)
        {
            int width = texture.width;
            int height = texture.height;
            if (ReadBackTexture2D.GetWidth() != width || ReadBackTexture2D.GetHeight() != height)
            {
                ReadBackTexture2D.Reload(width, height, 4);
                graphicsContext.UpdateReadBackTexture(ReadBackTexture2D);
            }

            graphicsContext.CopyTexture(ReadBackTexture2D, texture, index1);

            recordQueue.Enqueue(new ValueTuple<ulong, int, string>(graphicsDevice.GetInternalFenceValue(), index1, output));
            index1 = (index1 + 1) % 3;
        }

        public void Dispose()
        {
            ReadBackTexture2D = null;
        }
    }
}
