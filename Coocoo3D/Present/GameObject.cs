using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Coocoo3D.Components;

namespace Coocoo3D.Present
{
    public class GameObject
    {
        public string Name = "GameObject";
        public string Description = string.Empty;
        public int layoutMask;

        public Transform Transform;

        public override string ToString()
        {
            return this.Name;
        }

        public Dictionary<Type, Component> components = new Dictionary<Type, Component>();
        public T GetComponent<T>() where T : Component
        {
            if (components.TryGetValue(typeof(T), out Component component))
            {
                return (T)component;
            }
            else return null;
        }

        public bool TryGetComponent<T>(out T component) where T : Component
        {
            bool result = components.TryGetValue(typeof(T), out var component1);
            if (result)
                component = (T)component1;
            else
                component = default(T);
            return result;
        }

        public bool AddComponent(Component component)
        {
            if (components.ContainsKey(component.GetType()))
            {
                return false;
            }
            else
            {
                components.Add(component.GetType(), component);
                return true;
            }
        }
    }
}
