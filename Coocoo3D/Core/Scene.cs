using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DefaultEcs;
using DefaultEcs.Command;

namespace Coocoo3D.Core
{
    public class Scene
    {
        public World world;
        public EntityCommandRecorder recorder;

        public List<int> SelectedGameObjects = new();

        public int idAllocated = 1;

        public void OnFrame()
        {
            recorder.Execute();
            recorder.Clear();
        }
    }
}
