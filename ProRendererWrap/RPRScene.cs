using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ProRendererWrap.RPRHelper;
using FireRender.AMD.RenderEngine.Core;


namespace ProRendererWrap
{
    public class RPRScene : IDisposable
    {
        public RPRScene(RPRContext context)
        {
            this.Context = context;
            Check(Rpr.ContextCreateScene(context._handle, out _handle));
        }

        public void SetCamera(RPRCamera camera)
        {
            Check(Rpr.SceneSetCamera(_handle, camera._handle));
        }

        public void SetCameraRight(RPRCamera camera)
        {
            Check(Rpr.SceneSetCameraRight(_handle, camera._handle));
        }

        public void AttachShape(RPRShape shape)
        {
            Check(Rpr.SceneAttachShape(_handle, shape._handle));
        }

        public void AttachLight(RPRLight light)
        {
            Check(Rpr.SceneAttachLight(_handle, light._handle));
        }

        public void DetachLight(RPRLight light)
        {
            Check(Rpr.SceneDetachLight(_handle, light._handle));
        }

        public void DetachShape(RPRShape shape)
        {
            Check(Rpr.SceneDetachShape(_handle, shape._handle));
        }

        public void Clear()
        {
            Check(Rpr.SceneClear(_handle));
        }

        public IntPtr _handle;
        public RPRContext Context { get; }
        public void Dispose()
        {
            Rpr.ObjectDelete(ref _handle);
        }
    }
}
