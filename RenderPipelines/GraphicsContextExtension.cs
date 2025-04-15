using Coocoo3DGraphics;
using Coocoo3DGraphics.Commanding;

namespace RenderPipelines
{
    public static class GraphicsContextExtension
    {
        public static void SetSRVs(this ComputeCommandProxy proxy, params Texture2D[] textures)
        {
            for (int i = 0; i < textures.Length; i++)
            {
                proxy.SetSRV(i, textures[i]);
            }
        }
    }
}