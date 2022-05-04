using FireRender.AMD.RenderEngine.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ProRendererWrap.RPRHelper;

namespace ProRendererWrap
{
    public class RPRContext : IDisposable
    {

        public RPRContext()
        {
            string[] plugins = new string[] { "Northstar64.dll" };
            Init(Rpr.CreationFlags.ENABLE_GPU0, plugins);
        }

        public RPRContext(Rpr.CreationFlags creationFlags, IEnumerable<string> plugins)
        {
            Init(creationFlags, plugins);
        }

        public void Init(Rpr.CreationFlags creationFlags, IEnumerable<string> plugins)
        {
            int[] _plugins = plugins.Select(s => Rpr.RegisterPlugin(s)).ToArray();
            Check(Rpr.CreateContext(Rpr.RPR_API_VERSION, _plugins, _plugins.LongLength, creationFlags, new IntPtr(0), null, out _handle));
            foreach (var plugin in _plugins)
                Check(Rpr.ContextSetActivePlugin(_handle, plugin));
        }

        public RPRScene CreateScene()
        {
            return new RPRScene(this);
        }

        public void SetScene(RPRScene scene)
        {
            Check(Rpr.ContextSetScene(_handle, scene._handle));
        }

        public unsafe string GetInfo(Rpr.ContextInfo contextInfo)
        {
            const int c_size = 100;

            byte* dat = stackalloc byte[c_size];
            Check(Rpr.ContextGetInfo(_handle, contextInfo, c_size, new IntPtr(dat), out long size));
            return Encoding.UTF8.GetString(dat, (int)size);
        }

        public void SetAOV(Rpr.Aov aov, RPRFrameBuffer frameBuffer)
        {
            Check(Rpr.ContextSetAOV(_handle, aov, frameBuffer._handle));
        }

        public void SetParameterByKey1u(Rpr.ContextInfo contextInfo, uint x)
        {
            Check(Rpr.ContextSetParameterByKey1u(_handle, contextInfo, x));
        }

        public void SetParameterByKey1f(Rpr.ContextInfo contextInfo, float x)
        {
            Check(Rpr.ContextSetParameterByKey1f(_handle, contextInfo, x));
        }

        public void SetParameterByKey3f(Rpr.ContextInfo contextInfo, float x, float y, float z)
        {
            Check(Rpr.ContextSetParameterByKey3f(_handle, contextInfo, x, y, z));
        }

        public void SetParameterByKey4f(Rpr.ContextInfo contextInfo, float x, float y, float z, float w)
        {
            Check(Rpr.ContextSetParameterByKey4f(_handle, contextInfo, x, y, z, w));
        }

        public void SetParameterByKeyPtr(Rpr.ContextInfo contextInfo, IntPtr x)
        {
            Check(Rpr.ContextSetParameterByKeyPtr(_handle, contextInfo, x));
        }

        public void SetParameterByKeyString(Rpr.ContextInfo contextInfo, string x)
        {
            Check(Rpr.ContextSetParameterByKeyString(_handle, contextInfo, x));
        }

        public void Render()
        {
            Check(Rpr.ContextRender(_handle));
        }

        public void RenderTile(int xmin, int xmax, int ymin, int ymax)
        {
            Check(Rpr.ContextRenderTile(_handle, (uint)xmin, (uint)xmax, (uint)ymin, (uint)ymax));
        }

        public void ResolveFrameBuffer(RPRFrameBuffer src, RPRFrameBuffer dst, bool noDisplayGamma = false)
        {
            Check(Rpr.ContextResolveFrameBuffer(_handle, src._handle, dst._handle, noDisplayGamma));
        }

        public IntPtr _handle;
        public void Dispose()
        {
            Rpr.ObjectDelete(ref _handle);
        }
    }
}
