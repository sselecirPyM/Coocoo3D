using Coocoo3D.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.Present
{
    public class ParticleHolder
    {
        public List<Vector3> positions = new();
        public List<Vector3> velocities = new();
        public List<float> lifes = new();
        public List<float> scales = new();

        public Transform transform;

        public VisualComponent visualComponent;

        public ParticleHolder(VisualComponent visualComponent)
        {
            this.visualComponent = visualComponent;
        }

        public void Add(Vector3 position, float life)
        {
            positions.Add(position);
            velocities.Add(new Vector3());
            lifes.Add(life);
            scales.Add(1);
        }

        public void Remove(int index)
        {
            int last = positions.Count - 1;
            positions[index] = positions[last];
            positions.RemoveAt(last);
            velocities[index] = velocities[last];
            velocities.RemoveAt(last);
            lifes[index] = lifes[last];
            lifes.RemoveAt(last);
            scales[index] = scales[last];
            scales.RemoveAt(last);
        }

        public int count;
        public int id;
    }
}
