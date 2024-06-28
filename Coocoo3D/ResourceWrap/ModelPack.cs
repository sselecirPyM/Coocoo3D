using Coocoo3D.Components;
using Coocoo3D.Present;
using System;
using System.Collections.Generic;
using System.Numerics;
using Mesh = Coocoo3DGraphics.Mesh;

namespace Coocoo3D.ResourceWrap;

public class ModelPack : IDisposable
{
    public const string POSITION = "POSITION0";
    public const string NORMAL = "NORMAL0";
    public const string TEXCOORD = "TEXCOORD0";
    public const string TANGENT = "TANGENT0";
    public const string WEIGHTS = "WEIGHTS0";
    public const string BONES = "BONES0";

    public string fullPath;

    public string name;
    public string description;

    public Vector3[] position;
    public Vector3[] normal;
    public Vector4[] tangent;
    public Vector2[] uv;
    public ushort[] boneId;
    public float[] boneWeights;
    public int[] indices;
    Mesh meshInstance;
    public int vertexCount;
    public bool skinning;
    public List<string> textures;

    public List<Submesh> Submeshes = new();
    public List<RenderMaterial> Materials = new();

    public List<RigidBodyDesc> rigidBodyDescs;
    public List<JointDesc> jointDescs;

    public List<BoneInstance> bones;
    public List<MorphDesc> morphs;

    public const string whiteTextureReplace = ":whiteTexture";

    public Mesh GetMesh()
    {
        if (meshInstance == null)
        {
            meshInstance = new Mesh();
            meshInstance.LoadIndex<int>(vertexCount, indices);
            meshInstance.AddBuffer<Vector3>(position, POSITION);
            meshInstance.AddBuffer<Vector3>(normal, NORMAL);
            meshInstance.AddBuffer<Vector2>(uv, TEXCOORD);
            meshInstance.AddBuffer<Vector4>(tangent, TANGENT);
            if (boneId != null)
                meshInstance.AddBuffer<ushort>(boneId, BONES);
            if (boneWeights != null)
                meshInstance.AddBuffer<float>(boneWeights, WEIGHTS);
        }
        return meshInstance;
    }

    public void Dispose()
    {
        meshInstance?.Dispose();
        meshInstance = null;
    }
}
