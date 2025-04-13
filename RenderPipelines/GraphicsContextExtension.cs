using Coocoo3DGraphics;

namespace RenderPipelines
{
    public static class GraphicsContextExtension
    {
        public static void SetSRVs(this ComputeResourceProxy proxy, params Texture2D[] textures)
        {
            for (int i = 0; i < textures.Length; i++)
            {
                proxy.SetSRV(i, textures[i]);
            }
        }
    }
}