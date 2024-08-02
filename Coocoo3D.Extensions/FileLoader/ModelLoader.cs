using Coocoo3D.Components;
using Coocoo3D.Core;
using Coocoo3D.Extensions.FileFormat;
using Coocoo3D.Extensions.Utility;
using Coocoo3D.Present;
using Coocoo3D.RenderPipeline;
using Coocoo3D.ResourceWrap;
using Coocoo3DGraphics;
using glTFLoader;
using glTFLoader.Schema;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Coocoo3D.Extensions.FileLoader;

[Export("ResourceLoader")]
[Export(typeof(IEditorAccess))]
public class ModelLoader : IResourceLoader<ModelPack>, IEditorAccess
{
    public MainCaches mainCaches;
    public GraphicsContext graphicsContext;
    public GameDriverContext gameDriverContext;

    public bool TryLoad(string path, out ModelPack value)
    {
        var modelPack = new ModelPack();
        modelPack.fullPath = path;

        if (".pmx".Equals(Path.GetExtension(path), StringComparison.CurrentCultureIgnoreCase))
        {
            LoadPMX(modelPack, path);
        }
        else
        {
            LoadModel(modelPack, path);
        }

        var paths = new HashSet<string>(modelPack.textures);
        foreach (var material in modelPack.Materials)
        {
            var keys = new List<string>(material.Parameters.Keys);
            foreach (var key in keys)
            {
                object o = material.Parameters[key];
                if (o as string == ModelPack.whiteTextureReplace)
                {
                    material.Parameters[key] = mainCaches.GetTexturePreloaded(Path.GetFullPath("Assets/Textures/white.png", mainCaches.workDir));
                }
                else if (o is string path1 && paths.Contains(path1))
                {
                    material.Parameters[key] = mainCaches.GetTexturePreloaded(path1);
                }
            }
        }

        graphicsContext.UploadMesh(modelPack.GetMesh());
        value = modelPack;
        return true;
    }
    public void LoadPMX(ModelPack modelPack, string fileName)
    {
        using var stream = new FileInfo(fileName).OpenRead();
        using BinaryReader reader = new BinaryReader(stream);
        string folder = Path.GetDirectoryName(fileName);

        var pmx = PMXFormat.Load(reader);
        reader.Dispose();
        modelPack.name = string.Format("{0} {1}", pmx.Name, pmx.NameEN);
        modelPack.description = string.Format("{0}\n{1}", pmx.Description, pmx.DescriptionEN);
        HashSet<string> _textures = new HashSet<string>();
        foreach (var tex in pmx.Textures)
        {
            string relativePath = tex.TexturePath.Replace("//", "\\").Replace('/', '\\');
            string texPath = Path.GetFullPath(relativePath, folder);
            _textures.Add(texPath);
        }

        int indexOffset1 = 0;
        int vertexCount = 0;
        HashSet<int> indexToVertex1 = new HashSet<int>();
        for (int i = 0; i < pmx.Materials.Count; i++)
        {
            var material = pmx.Materials[i];
            for (int j = indexOffset1; j < indexOffset1 + material.TriangeIndexNum; j++)
                indexToVertex1.Add(pmx.TriangleIndexs[j]);
            indexOffset1 += material.TriangeIndexNum;
            vertexCount += indexToVertex1.Count;
            indexToVertex1.Clear();
        }

        modelPack.skinning = true;

        var position = new Vector3[vertexCount];
        var normal = new Vector3[vertexCount];
        var uv = new Vector2[vertexCount];
        var tangent = new Vector4[vertexCount];
        var boneId = new ushort[vertexCount * 4];
        var boneWeights = new float[vertexCount * 4];
        var indices = new int[pmx.TriangleIndexs.Length];

        modelPack.vertexCount = vertexCount;
        modelPack.position = position;
        modelPack.normal = normal;
        modelPack.uv = uv;
        modelPack.tangent = tangent;
        modelPack.boneId = boneId;
        modelPack.boneWeights = boneWeights;
        modelPack.indices = indices;

        pmx.TriangleIndexs.CopyTo(new Span<int>(indices));


        int indexOffset = 0;
        int vertexOffset = 0;
        Dictionary<int, int> vertexIndicesLocal = new Dictionary<int, int>();
        Dictionary<int, int> vertexIndicesAll = new Dictionary<int, int>();
        for (int i = 0; i < pmx.Materials.Count; i++)
        {
            var mmdMat = pmx.Materials[i];

            Vector3 min;
            Vector3 max;
            min = pmx.Vertices[pmx.TriangleIndexs[indexOffset]].Coordinate;
            max = min;
            for (int k = 0; k < mmdMat.TriangeIndexNum; k++)
            {
                int oldIndex = pmx.TriangleIndexs[indexOffset + k];
                ref var vert = ref pmx.Vertices[oldIndex];
                min = Vector3.Min(min, vert.Coordinate);
                max = Vector3.Max(max, vert.Coordinate);

                if (vertexIndicesLocal.TryGetValue(oldIndex, out int newIndex))
                {
                }
                else
                {
                    newIndex = vertexIndicesLocal.Count + vertexOffset;
                    vertexIndicesLocal[oldIndex] = newIndex;
                    vertexIndicesAll[oldIndex] = newIndex;
                    position[newIndex] = vert.Coordinate * 0.1f;
                    normal[newIndex] = vert.Normal;
                    uv[newIndex] = vert.UvCoordinate;
                    boneId[newIndex * 4 + 0] = (ushort)vert.boneId0;
                    boneId[newIndex * 4 + 1] = (ushort)vert.boneId1;
                    boneId[newIndex * 4 + 2] = (ushort)vert.boneId2;
                    boneId[newIndex * 4 + 3] = (ushort)vert.boneId3;

                    boneWeights[newIndex * 4 + 0] = vert.Weights.X;
                    boneWeights[newIndex * 4 + 1] = vert.Weights.Y;
                    boneWeights[newIndex * 4 + 2] = vert.Weights.Z;
                    boneWeights[newIndex * 4 + 3] = vert.Weights.W;
                    float weightTotal = boneWeights[newIndex * 4 + 0] + boneWeights[newIndex * 4 + 1] + boneWeights[newIndex * 4 + 2] + boneWeights[newIndex * 4 + 3];
                    boneWeights[newIndex * 4 + 0] /= weightTotal;
                    boneWeights[newIndex * 4 + 1] /= weightTotal;
                    boneWeights[newIndex * 4 + 2] /= weightTotal;
                    boneWeights[newIndex * 4 + 3] /= weightTotal;
                }
                indices[k + indexOffset] = newIndex - vertexOffset;
            }

            var material = new RenderMaterial
            {
                Name = mmdMat.Name,
            };

            var submesh = new Submesh()
            {
                Name = mmdMat.Name,
                indexCount = mmdMat.TriangeIndexNum,
                indexOffset = indexOffset,
                vertexCount = vertexIndicesLocal.Count,
                vertexStart = vertexOffset,
                DrawDoubleFace = mmdMat.DrawFlags.HasFlag(PMX_DrawFlag.DrawDoubleFace),
                boundingBox = new Vortice.Mathematics.BoundingBox(min, max),
            };

            material.Parameters["DiffuseColor"] = mmdMat.DiffuseColor;
            material.Parameters["SpecularColor"] = mmdMat.SpecularColor;
            material.Parameters["EdgeSize"] = mmdMat.EdgeScale;
            material.Parameters["AmbientColor"] = mmdMat.AmbientColor;
            material.Parameters["EdgeColor"] = mmdMat.EdgeColor;
            material.Parameters["CastShadow"] = mmdMat.DrawFlags.HasFlag(PMX_DrawFlag.CastSelfShadow);
            material.Parameters["ReceiveShadow"] = mmdMat.DrawFlags.HasFlag(PMX_DrawFlag.DrawSelfShadow);
            indexOffset += mmdMat.TriangeIndexNum;
            string texPath = null;
            if (pmx.Textures.Count > mmdMat.TextureIndex && mmdMat.TextureIndex >= 0)
            {
                string relativePath = pmx.Textures[mmdMat.TextureIndex].TexturePath.Replace("//", "\\").Replace('/', '\\');
                texPath = Path.GetFullPath(relativePath, folder);

                material.Parameters["_Albedo"] = texPath;
            }
            else
            {
                material.Parameters["_Albedo"] = ModelPack.whiteTextureReplace;
            }
            if (mmdMat.SphereTextureIndex >= 0 && mmdMat.SphereTextureIndex < pmx.Textures.Count)
            {
                string relativePath = pmx.Textures[mmdMat.SphereTextureIndex].TexturePath.Replace("//", "\\").Replace('/', '\\');
                texPath = Path.GetFullPath(relativePath, folder);

                material.Parameters["_Spa"] = texPath;
                material.Parameters["UseSpa"] = true;
            }

            material.Parameters["_Metallic"] = ModelPack.whiteTextureReplace;
            material.Parameters["_Roughness"] = ModelPack.whiteTextureReplace;
            if (texPath != null && Path.GetFileName(texPath).Contains("diffuse", StringComparison.CurrentCultureIgnoreCase))
            {
                string texFileName = Path.GetFileName(texPath);
                string dirName = Path.GetDirectoryName(texPath);

                void _SetTexture(string _texName, string _texSlot, string _texParam)
                {
                    FileInfo _Tex = new FileInfo(Path.GetFullPath(texFileName.Replace("diffuse", _texName, StringComparison.CurrentCultureIgnoreCase), dirName));
                    if (_Tex.Exists)
                    {
                        material.Parameters[_texSlot] = _Tex.FullName;
                        if (_texParam != null)
                            material.Parameters[_texParam] = 1.0f;
                        _textures.Add(_Tex.FullName);
                    }
                }
                _SetTexture("emissive", "_Emissive", "Emissive");
                _SetTexture("metallic", "_Metallic", "Metallic");
                _SetTexture("metalness", "_Metallic", "Metallic");
                _SetTexture("normal", "_Normal", null);
                _SetTexture("roughness", "_Roughness", "Roughness");

            }

            modelPack.Materials.Add(material);
            modelPack.Submeshes.Add(submesh);
            vertexOffset += vertexIndicesLocal.Count;
            vertexIndicesLocal.Clear();
            ComputeTangent(modelPack, submesh.vertexStart, submesh.vertexCount, submesh.indexOffset, submesh.indexCount);
        }

        modelPack.morphs = new List<MorphDesc>();
        for (int i = 0; i < pmx.Morphs.Count; i++)
        {
            modelPack.morphs.Add(PMXFormatExtension.Translate(pmx.Morphs[i]));
        }
        foreach (var morph in modelPack.morphs)
        {
            if (morph.MorphVertexs != null)
            {
                for (int i = 0; i < morph.MorphVertexs.Length; i++)
                {
                    ref var vertex = ref morph.MorphVertexs[i];
                    vertex.VertexIndex = vertexIndicesAll[vertex.VertexIndex];
                }
            }
        }

        var rigidBodys = pmx.RigidBodies;
        modelPack.rigidBodyDescs = new List<RigidBodyDesc>();
        for (int i = 0; i < rigidBodys.Count; i++)
        {
            var rigidBodyData = rigidBodys[i];
            var rigidBodyDesc = PMXFormatExtension.Translate(rigidBodyData);

            modelPack.rigidBodyDescs.Add(rigidBodyDesc);
        }
        var joints = pmx.Joints;
        modelPack.jointDescs = new List<JointDesc>();
        for (int i = 0; i < joints.Count; i++)
            modelPack.jointDescs.Add(PMXFormatExtension.Translate(joints[i]));

        modelPack.bones = new List<BoneInstance>();
        modelPack.ikBones = new List<IKBone>();
        var _bones = pmx.Bones;
        for (int i = 0; i < _bones.Count; i++)
        {
            modelPack.bones.Add(PMXFormatExtension.Translate(_bones[i], i, _bones.Count));
            PMXFormatExtension.AddIKBone(modelPack.ikBones, _bones[i], i);
        }
        modelPack.textures = _textures.ToList();
    }

    void ComputeTangent(ModelPack modelPack, int vertexStart, int vertexCount, int indexStart, int indexCount)
    {
        int indexEnd = indexStart + indexCount;

        Vector3[] bitangent = new Vector3[vertexCount];

        var _tangent = new Span<Vector4>(modelPack.tangent, vertexStart, vertexCount);
        var _position = new Span<Vector3>(modelPack.position, vertexStart, vertexCount);
        var _uv = new Span<Vector2>(modelPack.uv, vertexStart, vertexCount);
        var _normal = new Span<Vector3>(modelPack.normal, vertexStart, vertexCount);
        for (int i = 0; i < vertexCount; i++)
        {
            _tangent[i] = new Vector4(0.0F, 0.0F, 0.0F, 0.0F);
        }
        for (int i = 0; i < vertexCount; i++)
        {
            bitangent[i] = new Vector3(0.0F, 0.0F, 0.0F);
        }

        // Calculate tangent and bitangent for each triangle and add to all three vertices.
        for (int k = indexStart; k < indexEnd; k += 3)
        {
            int i0 = modelPack.indices[k];
            int i1 = modelPack.indices[k + 1];
            int i2 = modelPack.indices[k + 2];
            Vector3 p0 = _position[i0];
            Vector3 p1 = _position[i1];
            Vector3 p2 = _position[i2];
            Vector2 w0 = _uv[i0];
            Vector2 w1 = _uv[i1];
            Vector2 w2 = _uv[i2];
            Vector3 e1 = p1 - p0;
            Vector3 e2 = p2 - p0;
            float x1 = w1.X - w0.X, x2 = w2.X - w0.X;
            float y1 = w1.Y - w0.Y, y2 = w2.Y - w0.Y;
            float r = 1.0F / (x1 * y2 - x2 * y1);
            Vector3 t = (e1 * y2 - e2 * y1) * r;
            Vector3 b = (e2 * x1 - e1 * x2) * r;
            _tangent[i0] += new Vector4(t, 0);
            _tangent[i1] += new Vector4(t, 0);
            _tangent[i2] += new Vector4(t, 0);
            bitangent[i0] += b;
            bitangent[i1] += b;
            bitangent[i2] += b;
        }
        //// Orthonormalize each tangent and calculate the handedness.
        //for (int i = 0; i < vertexCount; i++)
        //{
        //    Vector3 t = tangent[i];
        //    Vector3 b = bitangent[i];
        //    Vector3 n = normalArray[i];
        //    tangentArray[i].xyz() = Vector3.Normalize(Reject(t, n));
        //    tangentArray[i].w = (Vector3.Dot(Vector3.Cross(t, b), n) > 0.0F) ? 1.0F : -1.0F;
        //}
        for (int i = 0; i < vertexCount; i++)
        {
            float factor;
            Vector3 t1 = Vector3.Cross(bitangent[i], _normal[i]);
            if (Vector3.Dot(t1, new Vector3(_tangent[i].X, _tangent[i].Y, _tangent[i].Z)) > 0)
                factor = 1;
            else
                factor = -1;
            _tangent[i] = new Vector4(Vector3.Normalize(t1) * factor, 1);
        }
    }

    public void LoadModel(ModelPack modelPack, string fileName)
    {
        var dir = Path.GetDirectoryName(fileName);
        var deserializedFile = Interface.LoadModel(fileName);
        byte[][] buffers = new byte[(int)deserializedFile.Buffers?.Length][];
        for (int i = 0; i < deserializedFile.Buffers?.Length; i++)
        {
            buffers[i] = deserializedFile.LoadBinaryBuffer(i, fileName);
        }
        modelPack.textures = new List<string>();
        for (int i = 0; i < deserializedFile.Images?.Length; i++)
        {
            var image = deserializedFile.Images[i];
            //string name = Path.GetFullPath(image.Uri, dir);
            string name = $"gltf://texture:{i}|{fileName}";
            modelPack.textures.Add(name);
        }
        List<RenderMaterial> _materials = new();
        for (int i = 0; i < deserializedFile.Materials?.Length; i++)
        {
            var material = deserializedFile.Materials[i];
            var renderMaterial = new RenderMaterial()
            {
                Name = material.Name,
            };
            if (material.PbrMetallicRoughness.BaseColorTexture != null)
            {
                int index = material.PbrMetallicRoughness.BaseColorTexture.Index;
                renderMaterial.Parameters["_Albedo"] = modelPack.textures[GLTFGetTexture(deserializedFile, index)];
            }
            else
            {
                renderMaterial.Parameters["_Albedo"] = ModelPack.whiteTextureReplace;
            }
            if (material.PbrMetallicRoughness.MetallicRoughnessTexture != null)
            {
                int index = material.PbrMetallicRoughness.MetallicRoughnessTexture.Index;
                renderMaterial.Parameters["_Metallic"] = modelPack.textures[GLTFGetTexture(deserializedFile, index)];
                renderMaterial.Parameters["_Roughness"] = modelPack.textures[GLTFGetTexture(deserializedFile, index)];
            }
            else
            {
                renderMaterial.Parameters["_Metallic"] = ModelPack.whiteTextureReplace;
                renderMaterial.Parameters["_Roughness"] = ModelPack.whiteTextureReplace;
            }
            if (material.EmissiveTexture != null)
            {
                int index = material.EmissiveTexture.Index;
                renderMaterial.Parameters["_Emissive"] = modelPack.textures[GLTFGetTexture(deserializedFile, index)];
                renderMaterial.Parameters["Emissive"] = 1.0f;
            }
            if (material.NormalTexture != null)
            {
                int index = material.NormalTexture.Index;
                renderMaterial.Parameters["_Normal"] = modelPack.textures[GLTFGetTexture(deserializedFile, index)];
                renderMaterial.Parameters["UseNormalMap"] = true;
            }

            renderMaterial.Parameters["Metallic"] = material.PbrMetallicRoughness.MetallicFactor;
            renderMaterial.Parameters["Roughness"] = material.PbrMetallicRoughness.RoughnessFactor;

            _materials.Add(renderMaterial);
        }
        Span<T> GetBuffer<T>(int accessorIndex) where T : struct
        {
            var accessor = deserializedFile.Accessors[accessorIndex];
            var bufferView = deserializedFile.BufferViews[(int)accessor.BufferView];
            var buffer = buffers[bufferView.Buffer];
            int typeSize = Marshal.SizeOf(typeof(T));
            return MemoryMarshal.Cast<byte, T>(new Span<byte>(buffer, bufferView.ByteOffset + accessor.ByteOffset, accessor.Count * typeSize));
        }

        Accessor.ComponentTypeEnum GetAccessorComponentType(int accessorIndex)
        {
            var accessor = deserializedFile.Accessors[accessorIndex];
            return accessor.ComponentType;
        }

        Vector3 scale = Vector3.One;
        if (deserializedFile.Nodes.Length == 1)
        {
            var scale1 = deserializedFile.Nodes[0].Scale;
            scale = new Vector3(scale1);
        }

        int vertexCount = 0;
        int indexCount = 0;
        for (int i = 0; i < deserializedFile.Meshes?.Length; i++)
        {
            var mesh = deserializedFile.Meshes[i];
            for (int j = 0; j < mesh.Primitives.Length; j++)
            {
                var primitive = mesh.Primitives[j];
                primitive.Attributes.TryGetValue("POSITION", out int pos1);

                var position1 = GetBuffer<Vector3>(pos1);
                vertexCount += position1.Length;
                var format = GetAccessorComponentType(primitive.Indices.Value);
                if (format == Accessor.ComponentTypeEnum.UNSIGNED_SHORT)
                {
                    var indices1 = GetBuffer<ushort>(primitive.Indices.Value);
                    indexCount += indices1.Length;
                }
                else if (format == Accessor.ComponentTypeEnum.UNSIGNED_INT)
                {
                    var indices1 = GetBuffer<uint>(primitive.Indices.Value);
                    indexCount += indices1.Length;
                }
            }
        }

        var indices = new int[indexCount];
        var position = new Vector3[vertexCount];
        var normal = new Vector3[vertexCount];
        var uv = new Vector2[vertexCount];
        var tangent = new Vector4[vertexCount];

        modelPack.vertexCount = vertexCount;
        modelPack.position = position;
        modelPack.normal = normal;
        modelPack.uv = uv;
        modelPack.tangent = tangent;
        modelPack.indices = indices;

        var positionWriter = new SpanWriter<Vector3>(position);
        var normalWriter = new SpanWriter<Vector3>(normal);
        var uvWriter = new SpanWriter<Vector2>(uv);
        var indicesWriter = new SpanWriter<int>(indices);
        var tangentWriter = new SpanWriter<Vector4>(tangent);
        for (int i = 0; i < deserializedFile.Meshes?.Length; i++)
        {
            var mesh = deserializedFile.Meshes[i];
            for (int j = 0; j < mesh.Primitives.Length; j++)
            {
                var primitive = mesh.Primitives[j];

                var material = _materials[primitive.Material.Value].GetClone();
                material.Name = modelPack.Materials.Count.ToString();
                int vertexStart = positionWriter.currentPosition;
                var submesh = new Submesh()
                {
                    Name = modelPack.Materials.Count.ToString(),
                    vertexStart = positionWriter.currentPosition,
                    indexOffset = indicesWriter.currentPosition,
                };

                primitive.Attributes.TryGetValue("POSITION", out int pos1);
                var bufferPos1 = GetBuffer<Vector3>(pos1);
                positionWriter.Write(bufferPos1);
                primitive.Attributes.TryGetValue("NORMAL", out int norm1);
                normalWriter.Write(GetBuffer<Vector3>(norm1));

                GetMinMax(bufferPos1, out Vector3 min, out Vector3 max);
                submesh.boundingBox = new Vortice.Mathematics.BoundingBox(min, max);

                primitive.Attributes.TryGetValue("TEXCOORD_0", out int uv1);
                uvWriter.Write(GetBuffer<Vector2>(uv1));

                submesh.vertexCount = bufferPos1.Length;
                var format = GetAccessorComponentType(primitive.Indices.Value);
                if (format == Accessor.ComponentTypeEnum.UNSIGNED_SHORT)
                {
                    var indices1 = GetBuffer<ushort>(primitive.Indices.Value);
                    for (int k = 0; k < indices1.Length; k++)
                        indicesWriter.Write(indices1[k]);
                    submesh.indexCount = indices1.Length;
                }
                else if (format == Accessor.ComponentTypeEnum.UNSIGNED_INT)
                {
                    var indices1 = GetBuffer<uint>(primitive.Indices.Value);
                    for (int k = 0; k < indices1.Length; k++)
                        indicesWriter.Write((int)indices1[k]);
                    submesh.indexCount = indices1.Length;
                }

                for (int k = submesh.indexOffset; k < submesh.indexOffset + submesh.indexCount; k += 3)
                {
                    (indices[k], indices[k + 1], indices[k + 2]) = (indices[k + 2], indices[k + 1], indices[k]);
                }

                for (int k = vertexStart; k < vertexStart + bufferPos1.Length; k++)
                {
                    position[k] *= scale;
                }

                if (primitive.Attributes.TryGetValue("TANGENT", out int tan1))
                {
                    tangentWriter.Write(GetBuffer<Vector4>(tan1));
                }
                else
                {
                    ComputeTangent(modelPack, vertexStart, bufferPos1.Length, submesh.indexOffset, submesh.indexCount);
                }
                modelPack.Materials.Add(material);
                modelPack.Submeshes.Add(submesh);
            }
        }
    }

    static void GetMinMax(ReadOnlySpan<Vector3> vectors, out Vector3 min, out Vector3 max)
    {
        min = vectors[0];
        max = min;
        for (int k = 0; k < vectors.Length; k++)
        {
            min = Vector3.Min(min, vectors[k]);
            max = Vector3.Max(max, vectors[k]);
        }
    }

    static int GLTFGetTexture(Gltf gltf, int index)
    {
        return (int)gltf.Textures[index].Source;
    }
}
