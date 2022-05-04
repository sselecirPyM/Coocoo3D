using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.Utility
{
    public class BinaryWriterPlus : BinaryWriter
    {
        public BinaryWriterPlus(Stream stream) : base(stream)
        {

        }

        public BinaryWriterPlus(Stream input, Encoding encoding) : base(input, encoding)
        {

        }

        public BinaryWriterPlus(Stream input, Encoding encoding, bool leaveOpen) : base(input, encoding, leaveOpen)
        {

        }

        public void Write(Vector2 vec2)
        {
            Write(vec2.X);
            Write(vec2.Y);
        }

        public void Write(Vector3 vec3)
        {
            Write(vec3.X);
            Write(vec3.Y);
            Write(vec3.Z);
        }

        public void Write(Vector4 vec4)
        {
            Write(vec4.X);
            Write(vec4.Y);
            Write(vec4.Z);
            Write(vec4.W);
        }

        public void Write(Quaternion quat)
        {
            Write(quat.X);
            Write(quat.Y);
            Write(quat.Z);
            Write(quat.W);
        }

        public void Write(Matrix4x4 mat4)
        {
            Write(mat4.M11);
            Write(mat4.M12);
            Write(mat4.M13);
            Write(mat4.M14);
            Write(mat4.M21);
            Write(mat4.M22);
            Write(mat4.M23);
            Write(mat4.M24);
            Write(mat4.M31);
            Write(mat4.M32);
            Write(mat4.M33);
            Write(mat4.M34);
            Write(mat4.M41);
            Write(mat4.M42);
            Write(mat4.M43);
            Write(mat4.M44);
        }

        byte[] temp = new byte[512];

        public void Write<T>(T data) where T : unmanaged
        {
            MemoryMarshal.Write(temp, ref data);
            int length = Marshal.SizeOf(typeof(T));
            base.Write(new Span<byte>(temp, 0, length));
        }

        public void Write<T>(T[] data) where T : unmanaged
        {
            Span<byte> castData = MemoryMarshal.Cast<T, byte>(data);
            base.Write(castData);
        }

        public void Write<T>(T[] data, int start, int length) where T : unmanaged
        {
            Span<byte> castData = MemoryMarshal.Cast<T, byte>(new Span<T>(data, start, length));
            base.Write(castData);
        }
    }
}
