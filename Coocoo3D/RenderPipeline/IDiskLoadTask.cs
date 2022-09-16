using Coocoo3D.ResourceWrap;

namespace Coocoo3D.RenderPipeline
{
    public interface IDiskLoadTask : INavigableTask
    {
        public byte[] Datas { get; set; }

        public KnownFile KnownFile { get; set; }
    }
}
