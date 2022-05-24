using Coocoo3D.Present;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Coocoo3D.Components;
using Coocoo3D.Base;
using System.Numerics;
using Coocoo3D.RenderPipeline;
using Coocoo3D.Utility;

namespace Coocoo3D.Core
{
    public class _physicsObjects
    {
        public List<Physics3DRigidBody> rigidbodies = new();
        public List<Physics3DJoint> joints = new();
    }
    public class Scene
    {
        public Settings settings = new Settings()
        {
        };

        public List<GameObject> gameObjects = new();
        public List<GameObject> gameObjectLoadList = new();
        public List<GameObject> gameObjectRemoveList = new();
        public Physics3DScene physics3DScene = new();

        public Dictionary<GameObject, Transform> setTransform = new();

        public Dictionary<MMDRendererComponent, _physicsObjects> physicsObjects = new();

        public MainCaches mainCaches;

        public void AddGameObject(GameObject gameObject)
        {
            gameObjectLoadList.Add(gameObject);
        }

        public void RemoveGameObject(GameObject gameObject)
        {
            gameObjectRemoveList.Add(gameObject);
        }

        public void DealProcessList()
        {
            for (int i = 0; i < gameObjectLoadList.Count; i++)
            {
                var gameObject = gameObjectLoadList[i];
                var renderComponent = gameObject.GetComponent<MMDRendererComponent>();

                gameObjects.Add(gameObject);
            }
            for (int i = 0; i < gameObjectRemoveList.Count; i++)
            {
                var gameObject = gameObjectRemoveList[i];
                gameObjects.Remove(gameObject);
                var renderComponent = gameObject.GetComponent<MMDRendererComponent>();
                if (renderComponent != null && physicsObjects.TryGetValue(renderComponent, out var phyObj))
                {
                    RemovePhysics(renderComponent, phyObj.rigidbodies, phyObj.joints);
                    physicsObjects.Remove(renderComponent);
                }
            }
            gameObjectLoadList.Clear();
            gameObjectRemoveList.Clear();
        }

        public void _ResetPhysics(IList<MMDRendererComponent> rendererComponents)
        {
            for (int i = 0; i < rendererComponents.Count; i++)
            {
                var r = rendererComponents[i];
                var phyO = GetOrCreatePhysics(r);

                r.UpdateAllMatrix();
                for (int j = 0; j < r.rigidBodyDescs.Count; j++)
                {
                    var desc = r.rigidBodyDescs[j];
                    if (desc.Type == 0) continue;
                    int index = desc.AssociatedBoneIndex;
                    if (index == -1) continue;
                    var mat1 = r.bones[index].GeneratedTransform * r.LocalToWorld;
                    Matrix4x4.Decompose(mat1, out _, out var rot, out _);
                    physics3DScene.ResetRigidBody(phyO.rigidbodies[j], Vector3.Transform(desc.Position, mat1), rot * desc.Rotation);
                }
            }
            physics3DScene.Simulation(1 / 60.0);
        }

        _physicsObjects GetOrCreatePhysics(MMDRendererComponent r)
        {
            if (!physicsObjects.TryGetValue(r, out var _PhysicsObjects))
            {
                _PhysicsObjects = new _physicsObjects();
                AddPhysics(r, _PhysicsObjects);
                physicsObjects[r] = _PhysicsObjects;
            }
            return _PhysicsObjects;
        }

        void AddPhysics(MMDRendererComponent r, _physicsObjects _physicsObjects)
        {
            var rigidbodies = _physicsObjects.rigidbodies;
            var joints = _physicsObjects.joints;
            for (int j = 0; j < r.rigidBodyDescs.Count; j++)
            {
                rigidbodies.Add(new Physics3DRigidBody());
                var desc = r.rigidBodyDescs[j];
                physics3DScene.AddRigidBody(rigidbodies[j], desc);
            }
            for (int j = 0; j < r.jointDescs.Count; j++)
            {
                joints.Add(new Physics3DJoint());
                var desc = r.jointDescs[j];
                physics3DScene.AddJoint(joints[j], rigidbodies[desc.AssociatedRigidBodyIndex1], rigidbodies[desc.AssociatedRigidBodyIndex2], desc);
            }
        }
        void RemovePhysics(MMDRendererComponent r, List<Physics3DRigidBody> rigidbodies, List<Physics3DJoint> joints)
        {
            for (int j = 0; j < rigidbodies.Count; j++)
            {
                physics3DScene.RemoveRigidBody(rigidbodies[j]);
            }
            for (int j = 0; j < joints.Count; j++)
            {
                physics3DScene.RemoveJoint(joints[j]);
            }
            rigidbodies.Clear();
            joints.Clear();
        }
        void PrePhysicsSync(MMDRendererComponent r, List<Physics3DRigidBody> rigidbodies)
        {
            for (int i = 0; i < r.rigidBodyDescs.Count; i++)
            {
                var desc = r.rigidBodyDescs[i];
                if (desc.Type != 0) continue;
                int index = desc.AssociatedBoneIndex;

                Matrix4x4 mat2 = MatrixExt.Transform(desc.Position, desc.Rotation) * r.bones[index].GeneratedTransform * r.LocalToWorld;
                physics3DScene.MoveRigidBody(rigidbodies[i], mat2);
            }
        }

        void PhysicsSyncBack(MMDRendererComponent r, List<Physics3DRigidBody> rigidbodies, List<Physics3DJoint> joints)
        {
            for (int i = 0; i < r.rigidBodyDescs.Count; i++)
            {
                var desc = r.rigidBodyDescs[i];
                if (desc.Type == 0) continue;
                int index = desc.AssociatedBoneIndex;
                if (index == -1) continue;
                r.bones[index]._generatedTransform = MatrixExt.InverseTransform(desc.Position, desc.Rotation) *
                    rigidbodies[i].GetTransform() * r.WorldToLocal;
            }
            r.UpdateMatrices(r.PhysicsNeedUpdateMatrixIndexs);

            r.UpdateAppendBones();
        }

        public void TransformToNew(MMDRendererComponent r, List<Physics3DRigidBody> rigidbodies)
        {
            for (int i = 0; i < r.rigidBodyDescs.Count; i++)
            {
                var desc = r.rigidBodyDescs[i];
                if (desc.Type != RigidBodyType.Kinematic) continue;
                int index = desc.AssociatedBoneIndex;
                var bone = r.bones[index];
                Matrix4x4 mat2 = MatrixExt.Transform(desc.Position, desc.Rotation) * bone.GeneratedTransform * r.LocalToWorld;
                physics3DScene.MoveRigidBody(rigidbodies[i], mat2);
            }
        }

        void BoneUpdate(double playTime, float deltaTime, IList<MMDRendererComponent> rendererComponents)
        {
            UpdateGameObjects((float)playTime, rendererComponents);

            float t1 = Math.Clamp(deltaTime, -0.17f, 0.17f);
            for (int i = 0; i < rendererComponents.Count; i++)
            {
                var r = rendererComponents[i];
                var _PhysicsObjects = GetOrCreatePhysics(r);
                PrePhysicsSync(r, _PhysicsObjects.rigidbodies);
            }
            physics3DScene.Simulation(t1 >= 0 ? t1 : -t1);
            //physics3DScene.FetchResults();
            for (int i = 0; i < rendererComponents.Count; i++)
            {
                var r = rendererComponents[i];
                physicsObjects.TryGetValue(r, out var _PhysicsObjects);
                PhysicsSyncBack(r, _PhysicsObjects.rigidbodies, _PhysicsObjects.joints);
            }
        }
        void UpdateGameObjects(float playTime, IList<MMDRendererComponent> rendererComponents)
        {
            void UpdateGameObject1(MMDRendererComponent rendererComponent)
            {
                rendererComponent?.ComputeMotion(playTime, mainCaches.GetMotion(rendererComponent.motionPath));
                //rendererComponent?.ComputeVertexMorph();
            }
            Parallel.ForEach(rendererComponents, UpdateGameObject1);
        }

        public void Simulation(double playTime, double deltaTime, bool resetPhysics)
        {
            var rendererComponents = new List<MMDRendererComponent>();
            for (int i = 0; i < gameObjects.Count; i++)
            {
                var gameObject = gameObjects[i];

                var render = gameObject.GetComponent<MMDRendererComponent>();
                var meshRender = gameObject.GetComponent<MeshRendererComponent>();
                if (render != null)
                {
                    rendererComponents.Add(render);
                }
                if (setTransform.TryGetValue(gameObject, out var transform))
                {
                    gameObject.Transform = transform;
                    if (render != null)
                    {
                        render.SetTransform(transform);
                        var phyObj = GetOrCreatePhysics(render);
                        TransformToNew(render, phyObj.rigidbodies);
                        resetPhysics = true;
                    }
                    if (meshRender != null)
                    {
                        meshRender.transform = transform;
                    }
                }
            }
            setTransform.Clear();
            if (resetPhysics)
            {
                _ResetPhysics(rendererComponents);
                BoneUpdate(playTime, (float)deltaTime, rendererComponents);
                _ResetPhysics(rendererComponents);
            }
            BoneUpdate(playTime, (float)deltaTime, rendererComponents);
        }
    }

    public class Settings
    {

        [NonSerialized]
        public Dictionary<string, object> Parameters = new();

        public Dictionary<string, bool> bValue;
        public Dictionary<string, int> iValue;
        public Dictionary<string, float> fValue;
        public Dictionary<string, Vector2> f2Value;
        public Dictionary<string, Vector3> f3Value;
        public Dictionary<string, Vector4> f4Value;

        public Settings GetClone()
        {
            var clone = (Settings)this.MemberwiseClone();
            clone.Parameters = new Dictionary<string, object>(Parameters);
            return clone;
        }
    }
}
