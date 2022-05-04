using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ProRendererWrap.RPRHelper;
using FireRender.AMD.RenderEngine.Core;
using System.Numerics;
using System.Buffers;
using System.Runtime.InteropServices;

namespace ProRendererWrap
{
    public class RPRShape : IDisposable
    {
        public RPRShape(RPRContext context, float[] vertices, float[] normals, float[] texcoords, int[] indices)
        {
            this.Context = context;
            int vertexCount = vertices.Length / 3;
            int[] num_face_vertices = new int[indices.Length / 3];
            Array.Fill<int>(num_face_vertices, 3);
            Check(Rpr.ContextCreateMesh(context._handle, vertices, vertexCount, 12, normals, vertexCount, 12, texcoords, vertexCount, 8,
                indices, 4, indices, 4, indices, 4, num_face_vertices, num_face_vertices.LongLength, out _handle));
        }
        public RPRShape(RPRContext context, Span<byte> vertices, Span<byte> normals, Span<byte> texcoords, Span<byte> indices, int numIndices)
        {
            this.Context = context;
            int vertexCount = vertices.Length / 4 / 3;
            int[] num_face_vertices = new int[numIndices / 3];
            Array.Fill<int>(num_face_vertices, 3);

            Check(ContextCreateMesh(context._handle, vertices, vertexCount, 12, normals, vertexCount, 12, texcoords, vertexCount, 8,
                indices, 4, indices, 4, indices, 4, num_face_vertices, num_face_vertices.LongLength, out _handle));
        }
        public RPRShape(RPRContext context, Span<byte> vertices, Span<byte> normals, Span<byte> texcoords, Span<byte> indices, int numIndices, bool dxCoord)
        {
            this.Context = context;
            int vertexCount = vertices.Length / 4 / 3;
            int[] num_face_vertices = new int[numIndices / 3];
            Array.Fill<int>(num_face_vertices, 3);
            float[] texCoords1 = ArrayPool<float>.Shared.Rent(vertexCount * 2);
            MemoryMarshal.Cast<byte, float>(texcoords).CopyTo(texCoords1);
            for (int i = 0; i < vertexCount; i++)
            {
                texCoords1[i * 2 + 1] = 1 - texCoords1[i * 2 + 1];
            }
            //byte[] indices2 = ArrayPool<byte>.Shared.Rent(indices.Length);
            //indices.CopyTo(indices2);
            //Span<int> indiceX = MemoryMarshal.Cast<byte, int>(new Span<byte>(indices2, 0, indices.Length));
            //for (int i = 0; i < indiceX.Length; i += 3)
            //{
            //    (indiceX[i], indiceX[i + 1], indiceX[i + 2]) = (indiceX[i + 2], indiceX[i + 1], indiceX[i]);
            //}

            Check(ContextCreateMesh(context._handle, vertices, vertexCount, 12, normals, vertexCount, 12, MemoryMarshal.AsBytes<float>(texCoords1), vertexCount, 8,
                indices, 4, indices, 4, indices, 4, num_face_vertices, num_face_vertices.LongLength, out _handle));
            ArrayPool<float>.Shared.Return(texCoords1);
            //ArrayPool<byte>.Shared.Return(indices2);
        }
        public RPRShape(RPRShape shape)
        {
            this.Context = shape.Context;
        }

        public unsafe void SetTransform(Matrix4x4 matrix)
        {
            float* data1 = stackalloc float[16];
            MatrixToFloatArray(matrix, new Span<float>(data1, 16));
            rprShapeSetTransform(_handle, false, data1);
        }

        public void SetMaterial(RPRMaterialNode materialNode)
        {
            Check(Rpr.ShapeSetMaterial(_handle, materialNode._handle));
        }

        public void SetObjectID(uint id)
        {
            Check(Rpr.ShapeSetObjectID(_handle, id));
        }

        public void SetObjectGroupId(uint id)
        {
            Check(Rpr.ShapeSetObjectGroupID(_handle, id));
        }

        public IntPtr _handle;
        public RPRContext Context { get; }
        public void Dispose()
        {
            Rpr.ObjectDelete(ref _handle);
        }

        [DllImport(dllName)] unsafe static extern Rpr.Status rprShapeSetTransform(IntPtr shape, bool transpose, float* transform);

        [DllImport(dllName)] unsafe static extern Rpr.Status rprContextCreateMesh(IntPtr context, byte* vertices, long num_vertices, int vertex_stride, byte* normals, long num_normals, int normal_stride, byte* texcoords, long num_texcoords, int texcoord_stride, byte* vertex_indices, int vidx_stride, byte* normal_indices, int nidx_stride, byte* texcoord_indices, int tidx_stride, int[] num_face_vertices, long num_faces, out IntPtr out_mesh);
        unsafe static Rpr.Status ContextCreateMesh(IntPtr context, Span<byte> vertices, long num_vertices, int vertex_stride, Span<byte> normals, long num_normals, int normal_stride, Span<byte> texcoords, long num_texcoords, int texcoord_stride, Span<byte> vertex_indices, int vidx_stride, Span<byte> normal_indices, int nidx_stride, Span<byte> texcoord_indices, int tidx_stride, int[] num_face_vertices, long num_faces, out IntPtr out_mesh)
        {
            fixed (byte* pvert = &MemoryMarshal.GetReference(vertices))
            fixed (byte* pnorm = &MemoryMarshal.GetReference(normals))
            fixed (byte* ptex = &MemoryMarshal.GetReference(texcoords))
            fixed (byte* pvertIndice = &MemoryMarshal.GetReference(vertex_indices))
            fixed (byte* pnormIndice = &MemoryMarshal.GetReference(normal_indices))
            fixed (byte* ptexIndice = &MemoryMarshal.GetReference(texcoord_indices))
            {
                return rprContextCreateMesh(context, pvert, num_vertices, vertex_stride, pnorm, num_normals, normal_stride, ptex, num_texcoords, texcoord_stride, pvertIndice, vidx_stride, pnormIndice, nidx_stride, ptexIndice, tidx_stride, num_face_vertices, num_faces, out out_mesh);
            }
        }
    }
}
