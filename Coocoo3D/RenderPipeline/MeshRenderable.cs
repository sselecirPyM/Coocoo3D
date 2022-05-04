using Coocoo3D.Present;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.RenderPipeline
{
    public struct MeshRenderable
    {
        public Mesh mesh;
        public Mesh meshOverride;
        public int indexStart;
        public int indexCount;
        public int vertexStart;
        public int vertexCount;
        public RenderMaterial material;
        public Matrix4x4 transform;
        public bool gpuSkinning;
        public RenderableType type;
    }
    public enum RenderableType
    {
        Object,
        Particle,

    }
}
