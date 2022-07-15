using Caprice.Display;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RenderPipelines
{
    public enum BlendMode
    {
        [UIShow(name:"正常")]
        Alpha,
        [UIShow(name:"添加")]
        Add,
    }
}
