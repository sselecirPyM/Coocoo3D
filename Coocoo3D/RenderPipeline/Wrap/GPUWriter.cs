using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Coocoo3D.Utility;
using Coocoo3DGraphics;
using System.IO;
using System.Numerics;

namespace Coocoo3D.RenderPipeline.Wrap
{
    public class GPUWriter
    {
        MemoryStream memoryStream = new MemoryStream();
        public BinaryWriterPlus binaryWriter;
        CBuffer cBuffer;
        public GraphicsContext graphicsContext;

        public GPUWriter()
        {
            binaryWriter = new BinaryWriterPlus(memoryStream);
        }

        public int BufferBegin()
        {
            int allign = ((int)memoryStream.Position + 255) & ~255;
            binaryWriter.Seek(allign, SeekOrigin.Begin);
            return allign;
        }

        public byte[] GetData()
        {
            byte[] data = memoryStream.ToArray();
            binaryWriter.Seek(0, SeekOrigin.Begin);
            return data;
        }

        public void Clear()
        {
            binaryWriter.Seek(0, SeekOrigin.Begin);
        }

        public CBuffer GetBuffer(GraphicsContext context)
        {
            if (cBuffer == null)
                cBuffer = new CBuffer();
            cBuffer.Mutable = true;

            context.UpdateResource(cBuffer, new ReadOnlySpan<byte>(memoryStream.GetBuffer(), 0, (int)memoryStream.Position));
            binaryWriter.Seek(0, SeekOrigin.Begin);
            return cBuffer;
        }

        void GetSpacing(int sizeX)
        {
            int currentOffset = (int)memoryStream.Position;
            int c = (currentOffset & 15);
            if (c != 0 && c + sizeX > 16)
            {
                int d = 16 - c;
                for (int i = 0; i < d; i++)
                    binaryWriter.Write((byte)0);
            }
        }

        public void Write(int a) => binaryWriter.Write(a);
        public void Write(float a) => binaryWriter.Write(a);

        public void Write(Vector2 a)
        {
            GetSpacing(8);
            binaryWriter.Write(a);
        }

        public void Write(Vector3 a)
        {
            GetSpacing(12);
            binaryWriter.Write(a);
        }

        public void Write(Vector4 a)
        {
            GetSpacing(16);
            binaryWriter.Write(a);
        }

        public void Write(Matrix4x4 a)
        {
            GetSpacing(16);
            binaryWriter.Write(Matrix4x4.Transpose(a));
        }

        public void Write((int, int) a)
        {
            GetSpacing(8);
            binaryWriter.Write(a.Item1);
            binaryWriter.Write(a.Item2);
        }

        public void Write((int, int, int) a)
        {
            GetSpacing(12);
            binaryWriter.Write(a.Item1);
            binaryWriter.Write(a.Item2);
            binaryWriter.Write(a.Item3);
        }

        public void Write((int, int, int, int) a)
        {
            GetSpacing(16);
            binaryWriter.Write(a.Item1);
            binaryWriter.Write(a.Item2);
            binaryWriter.Write(a.Item3);
            binaryWriter.Write(a.Item4);
        }

        public void WriteObject(object obj)
        {
            switch (obj)
            {
                case Matrix4x4 value:
                    Write(value);
                    break;
                case int value:
                    Write(value);
                    break;
                case float value:
                    Write(value);
                    break;
                case Vector2 value:
                    Write(value);
                    break;
                case Vector3 value:
                    Write(value);
                    break;
                case Vector4 value:
                    Write(value);
                    break;
                case Matrix4x4[] values:
                    foreach (var v in values)
                        Write(v);
                    break;
                case ValueTuple<int, int> value:
                    Write(value);
                    break;
                case ValueTuple<int, int, int> value:
                    Write(value);
                    break;
                case ValueTuple<int, int, int, int> value:
                    Write(value);
                    break;
                case int[] values:
                    foreach (var v in values)
                        Write(v);
                    break;
                case float[] values:
                    foreach (var v in values)
                        Write(v);
                    break;
                case byte[] values:
                    GetSpacing(16);
                    binaryWriter.Write(values);
                    break;
                case ValueTuple<byte[], int> values:
                    GetSpacing(16);
                    binaryWriter.Write(values.Item1, 0, values.Item2);
                    break;
                case ValueTuple<byte[], int, int> values:
                    GetSpacing(16);
                    binaryWriter.Write(values.Item1, values.Item2, values.Item3);
                    break;
            }
        }

        public void SetCBV(int slot)
        {
            graphicsContext.SetCBVRSlot(new ReadOnlySpan<byte>(memoryStream.GetBuffer(), 0, (int)memoryStream.Position), slot);
            binaryWriter.Seek(0, SeekOrigin.Begin);
        }
    }
}
