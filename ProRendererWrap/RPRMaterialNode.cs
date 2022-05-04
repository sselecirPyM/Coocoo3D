using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ProRendererWrap.RPRHelper;
using FireRender.AMD.RenderEngine.Core;
using System.Numerics;

namespace ProRendererWrap
{
    public class RPRMaterialNode : IDisposable
    {
        public RPRMaterialNode(RPRMaterialSystem materialSystem, Rpr.MaterialNodeType materialNodeType)
        {
            this.Context = materialSystem.Context;
            Check(Rpr.MaterialSystemCreateNode(materialSystem._handle, materialNodeType, out _handle));
        }

        public void SetInputFByKey(Rpr.MaterialInput input, float x, float y, float z, float w)
        {
            Check(Rpr.MaterialNodeSetInputFByKey(_handle, input, x, y, z, w));
        }

        public void SetInputFByKey(Rpr.MaterialInput input, Vector4 value)
        {
            Check(Rpr.MaterialNodeSetInputFByKey(_handle, input, value.X, value.Y, value.Z, value.W));
        }

        public void SetInputImageDataByKey(Rpr.MaterialInput materialInput, RPRImage image)
        {
            Check(Rpr.MaterialNodeSetInputImageDataByKey(_handle, materialInput, image._handle));
        }

        public void SetInputNByKey(Rpr.MaterialInput materialInput, RPRMaterialNode material)
        {
            Check(Rpr.MaterialNodeSetInputNByKey(_handle, materialInput, material._handle));
        }

        public void SetInputUByKey(Rpr.MaterialInput materialInput, uint value)
        {
            Check(Rpr.MaterialNodeSetInputUByKey(_handle, materialInput, value));
        }

        public IntPtr _handle;
        public RPRContext Context { get; }
        public void Dispose()
        {
            Rpr.ObjectDelete(ref _handle);
        }
    }
}
