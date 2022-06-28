using Coocoo3D.Present;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Coocoo3D.Components;

namespace Coocoo3D.Core
{
    public class Scene
    {
        public List<GameObject> SelectedGameObjects = new List<GameObject>();

        public List<GameObject> gameObjects = new();
        public List<GameObject> gameObjectLoadList = new();
        public List<GameObject> gameObjectRemoveList = new();

        public Dictionary<GameObject, Transform> setTransform = new();

        public int idAllocated = 1;

        public void AddGameObject(GameObject gameObject)
        {
            gameObjectLoadList.Add(gameObject);
        }

        public void RemoveGameObject(GameObject gameObject)
        {
            gameObjectRemoveList.Add(gameObject); ;
        }

        public void SetTransform(GameObject gameObject, Transform transform)
        {
            setTransform[gameObject] = transform;
        }

        public void DealProcessList()
        {
            for (int i = 0; i < gameObjectLoadList.Count; i++)
            {
                var gameObject = gameObjectLoadList[i];
                gameObject.id = idAllocated;
                idAllocated++;
                var renderComponent = gameObject.GetComponent<MMDRendererComponent>();
                if (renderComponent != null)
                    renderComponent.SetTransform(gameObject.Transform);
                gameObjects.Add(gameObject);
            }
            for (int i = 0; i < gameObjectRemoveList.Count; i++)
            {
                var gameObject = gameObjectRemoveList[i];
                gameObjects.Remove(gameObject);
            }
            //gameObjectLoadList.Clear();
            //gameObjectRemoveList.Clear();
        }

        public void Simulation()
        {
            for (int i = 0; i < gameObjects.Count; i++)
            {
                var gameObject = gameObjects[i];

                var render = gameObject.GetComponent<MMDRendererComponent>();
                var meshRender = gameObject.GetComponent<MeshRendererComponent>();
                if (setTransform.TryGetValue(gameObject, out var transform))
                {
                    gameObject.Transform = transform;
                    if (render != null)
                    {
                        render.SetTransform(transform);
                    }
                    if (meshRender != null)
                    {
                        meshRender.transform = transform;
                    }
                }
            }
            //setTransform.Clear();
        }

        public void Clear()
        {
            setTransform.Clear();
            gameObjectLoadList.Clear();
            gameObjectRemoveList.Clear();
        }
    }
}
