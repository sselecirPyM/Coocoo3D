using Coocoo3D.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.UI
{
    public static class PlayControl
    {
        public static void Play(Coocoo3DMain appBody)
        {
            appBody.GameDriverContext.Playing = true;
            appBody.GameDriverContext.PlaySpeed = 1.0f;
            appBody.RequireRender();
        }
        public static void Pause(Coocoo3DMain appBody)
        {
            appBody.GameDriverContext.Playing = false;
        }
        public static void Stop(Coocoo3DMain appBody)
        {
            appBody.ToPlayMode();
            appBody.GameDriverContext.Playing = false;
            appBody.GameDriverContext.PlayTime = 0;
            appBody.RequireRender(true);
        }
        public static void Rewind(Coocoo3DMain appBody)
        {
            appBody.ToPlayMode();
            appBody.GameDriverContext.Playing = true;
            appBody.GameDriverContext.PlaySpeed = -2.0f;
        }
        public static void FastForward(Coocoo3DMain appBody)
        {
            appBody.ToPlayMode();
            appBody.GameDriverContext.Playing = true;
            appBody.GameDriverContext.PlaySpeed = 2.0f;
        }
        public static void Front(Coocoo3DMain appBody)
        {
            appBody.ToPlayMode();
            appBody.GameDriverContext.PlayTime = 0;
            appBody.RequireRender(true);
        }
        public static void Rear(Coocoo3DMain appBody)
        {
            appBody.ToPlayMode();
            appBody.GameDriverContext.PlayTime = 9999;
            appBody.RequireRender(true);
        }
    }
}
