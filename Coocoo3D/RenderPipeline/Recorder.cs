using Coocoo3D.ResourceWrap;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.RenderPipeline
{
    public class TextureRecordData
    {
        public ulong frame;
        public int offset;
        public int width;
        public int height;
        public string target;

        public Stream stream;
    }
    public class Recorder : IDisposable
    {
        public ReadBackBuffer ReadBackBuffer = new ReadBackBuffer();

        public GraphicsContext graphicsContext;

        public GraphicsDevice graphicsDevice;

        byte[] temp;

        public bool recording;

        public void OnFrame()
        {
            if (recordQueue.Count == 0) return;
            ulong completed = graphicsDevice.GetInternalCompletedFenceValue();
            while (recordQueue.Count > 0 && recordQueue.Peek().frame <= completed)
            {
                var tuple = recordQueue.Dequeue();
                int width = tuple.width;
                int height = tuple.height;
                var stream = tuple.stream;

                if (temp == null || temp.Length != width * height * 4)
                {
                    temp = new byte[width * height * 4];
                }
                var data = temp;
                ReadBackBuffer.GetData<byte>(tuple.offset, height, (width * 4 + 255) & ~255, width * 4, data);

                if (stream == null)
                    TextureHelper.SaveToFile(data, width, height, tuple.target);
                else
                {
                    TextureHelper.SaveToFile(data, width, height, tuple.target, stream);
                    stream.Flush();
                }
            }
        }

        public Queue<TextureRecordData> recordQueue = new();

        public void Record(Texture2D texture, Stream stream, string output)
        {
            int width = texture.width;
            int height = texture.height;
            if (ReadBackBuffer.size < ((width * 4 + 255) & ~255) * height)
            {
                graphicsContext.UpdateReadBackTexture(ReadBackBuffer, width, height, 4);
            }

            int offset = graphicsContext.ReadBack(ReadBackBuffer, texture);

            recordQueue.Enqueue(new TextureRecordData
            {
                frame = graphicsDevice.GetInternalFenceValue(),
                offset = offset,
                target = output,
                width = width,
                height = height,
                stream = stream
            });
        }

        public void Dispose()
        {
            ReadBackBuffer = null;
        }
    }
}
