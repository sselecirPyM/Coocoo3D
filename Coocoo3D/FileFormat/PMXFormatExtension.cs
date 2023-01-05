using Coocoo3D.Components;
using Coocoo3D.Present;
using Coocoo3D.ResourceWrap;
using DefaultEcs.Command;
using System;
using System.Numerics;

namespace Coocoo3D.FileFormat
{
    public static class PMXFormatExtension
    {
        public static (MMDRendererComponent, AnimationStateComponent) LoadPmx(this EntityRecord gameObject, ModelPack modelPack)
        {
            return LoadPmx(gameObject, modelPack, new Transform(Vector3.Zero, Quaternion.Identity));
        }
        public static (MMDRendererComponent, AnimationStateComponent) LoadPmx(this EntityRecord gameObject, ModelPack modelPack, Transform transform)
        {
            gameObject.Set(new ObjectDescription
            {
                Name = modelPack.name,
                Description = modelPack.description,
            });
            gameObject.Set(transform);

            var renderer = new MMDRendererComponent();
            gameObject.Set(renderer);
            renderer.skinning = true;
            renderer.Morphs.Clear();
            renderer.Morphs.AddRange(modelPack.morphs);
            var animationState = new AnimationStateComponent();
            gameObject.Set(animationState);
            animationState.LoadAnimationStates(modelPack);

            renderer.Initialize(modelPack);
            renderer.LoadMesh(modelPack);
            return (renderer, animationState);
        }

        static void Initialize(this MMDRendererComponent renderer, ModelPack modelPack)
        {
            renderer.bones.Clear();
            renderer.BoneMatricesData = new Matrix4x4[modelPack.bones.Count];

            foreach (var bone in modelPack.bones)
                renderer.bones.Add(bone.GetClone());


            renderer.rigidBodyDescs.Clear();
            renderer.rigidBodyDescs.AddRange(modelPack.rigidBodyDescs);
            for (int i = 0; i < modelPack.rigidBodyDescs.Count; i++)
            {
                var rigidBodyDesc = renderer.rigidBodyDescs[i];

                if (rigidBodyDesc.Type != RigidBodyType.Kinematic && rigidBodyDesc.AssociatedBoneIndex != -1)
                    renderer.bones[rigidBodyDesc.AssociatedBoneIndex].IsPhysicsFreeBone = true;
            }
            renderer.jointDescs.Clear();
            renderer.jointDescs.AddRange(modelPack.jointDescs);
            renderer.Bake();
        }

        static void LoadMesh(this MMDRendererComponent renderer, ModelPack modelPack)
        {
            renderer.Materials.Clear();
            for (int i = 0; i < modelPack.Materials.Count; i++)
            {
                var mat = modelPack.Materials[i].GetClone();
                renderer.Materials.Add(mat);
            }
            renderer.Weights = new float[modelPack.morphs.Count];

            var mesh = modelPack.GetMesh();
            renderer.meshPath = modelPack.fullPath;
            renderer.MeshPosition = new Vector3[mesh.GetVertexCount()];
            new Span<Vector3>(modelPack.position).CopyTo(renderer.MeshPosition);
        }

        public static MorphSubMorphDesc Translate(PMX_MorphSubMorphDesc desc)
        {
            return new MorphSubMorphDesc()
            {
                GroupIndex = desc.GroupIndex,
                Rate = desc.Rate,
            };
        }
        public static MorphMaterialDesc Translate(PMX_MorphMaterialDesc desc)
        {
            return new MorphMaterialDesc()
            {
                Ambient = desc.Ambient,
                Diffuse = desc.Diffuse,
                EdgeColor = desc.EdgeColor,
                EdgeSize = desc.EdgeSize,
                MaterialIndex = desc.MaterialIndex,
                MorphMethon = (MorphMaterialMethon)desc.MorphMethon,
                Specular = desc.Specular,
                SubTexture = desc.SubTexture,
                Texture = desc.Texture,
                ToonTexture = desc.ToonTexture,
            };
        }
        public static MorphVertexDesc Translate(PMX_MorphVertexDesc desc)
        {
            return new MorphVertexDesc()
            {
                Offset = desc.Offset * 0.1f,
                VertexIndex = desc.VertexIndex,
            };
        }
        public static MorphUVDesc Translate(PMX_MorphUVDesc desc)
        {
            return new MorphUVDesc()
            {
                Offset = desc.Offset,
                VertexIndex = desc.VertexIndex,
            };
        }
        public static MorphBoneDesc Translate(PMX_MorphBoneDesc desc)
        {
            return new MorphBoneDesc()
            {
                BoneIndex = desc.BoneIndex,
                Rotation = desc.Rotation,
                Translation = desc.Translation * 0.1f,
            };
        }

        public static MorphDesc Translate(PMX_Morph desc)
        {
            MorphSubMorphDesc[] subMorphDescs = null;
            if (desc.SubMorphs != null)
            {
                subMorphDescs = new MorphSubMorphDesc[desc.SubMorphs.Length];
                for (int i = 0; i < desc.SubMorphs.Length; i++)
                    subMorphDescs[i] = Translate(desc.SubMorphs[i]);
            }
            MorphMaterialDesc[] morphMaterialDescs = null;
            if (desc.MorphMaterials != null)
            {
                morphMaterialDescs = new MorphMaterialDesc[desc.MorphMaterials.Length];
                for (int i = 0; i < desc.MorphMaterials.Length; i++)
                    morphMaterialDescs[i] = Translate(desc.MorphMaterials[i]);
            }
            MorphVertexDesc[] morphVertexDescs = null;
            if (desc.MorphVertexs != null)
            {
                morphVertexDescs = new MorphVertexDesc[desc.MorphVertexs.Length];
                for (int i = 0; i < desc.MorphVertexs.Length; i++)
                    morphVertexDescs[i] = Translate(desc.MorphVertexs[i]);
            }
            MorphUVDesc[] morphUVDescs = null;
            if (desc.MorphUVs != null)
            {
                morphUVDescs = new MorphUVDesc[desc.MorphUVs.Length];
                for (int i = 0; i < desc.MorphUVs.Length; i++)
                    morphUVDescs[i] = Translate(desc.MorphUVs[i]);
            }
            MorphBoneDesc[] morphBoneDescs = null;
            if (desc.MorphBones != null)
            {
                morphBoneDescs = new MorphBoneDesc[desc.MorphBones.Length];
                for (int i = 0; i < desc.MorphBones.Length; i++)
                    morphBoneDescs[i] = Translate(desc.MorphBones[i]);
            }

            return new MorphDesc()
            {
                Name = desc.Name,
                NameEN = desc.NameEN,
                Category = (MorphCategory)desc.Category,
                Type = (MorphType)desc.Type,
                MorphBones = morphBoneDescs,
                MorphMaterials = morphMaterialDescs,
                MorphUVs = morphUVDescs,
                MorphVertexs = morphVertexDescs,
                SubMorphs = subMorphDescs,
            };
        }

        public static BoneEntity Translate(PMX_Bone _bone, int index, int boneCount)
        {
            BoneEntity boneEntity = new();
            boneEntity.ParentIndex = (_bone.ParentIndex >= 0 && _bone.ParentIndex < boneCount) ? _bone.ParentIndex : -1;
            boneEntity.staticPosition = _bone.Position * 0.1f;
            boneEntity.rotation = Quaternion.Identity;
            boneEntity.index = index;
            boneEntity.inverseBindMatrix = Matrix4x4.CreateTranslation(-boneEntity.staticPosition);

            boneEntity.Name = _bone.Name;
            boneEntity.NameEN = _bone.NameEN;
            boneEntity.Flags = (BoneFlags)_bone.Flags;

            if (boneEntity.Flags.HasFlag(BoneFlags.HasIK))
            {
                boneEntity.IKTargetIndex = _bone.boneIK.IKTargetIndex;
                boneEntity.CCDIterateLimit = _bone.boneIK.CCDIterateLimit;
                boneEntity.CCDAngleLimit = _bone.boneIK.CCDAngleLimit;
                boneEntity.boneIKLinks = new BoneEntity.IKLink[_bone.boneIK.IKLinks.Length];
                for (int j = 0; j < boneEntity.boneIKLinks.Length; j++)
                {
                    boneEntity.boneIKLinks[j] = IKLink(_bone.boneIK.IKLinks[j]);
                }
            }
            if (_bone.AppendBoneIndex >= 0 && _bone.AppendBoneIndex < boneCount)
            {
                boneEntity.AppendParentIndex = _bone.AppendBoneIndex;
                boneEntity.AppendRatio = _bone.AppendBoneRatio;
                boneEntity.IsAppendRotation = boneEntity.Flags.HasFlag(BoneFlags.AcquireRotate);
                boneEntity.IsAppendTranslation = boneEntity.Flags.HasFlag(BoneFlags.AcquireTranslate);
            }
            else
            {
                boneEntity.AppendParentIndex = -1;
                boneEntity.AppendRatio = 0;
                boneEntity.IsAppendRotation = false;
                boneEntity.IsAppendTranslation = false;
            }
            return boneEntity;
        }
        static void LoadAnimationStates(this AnimationStateComponent component, ModelPack modelPack)
        {
            component.cachedBoneKeyFrames.Clear();
            for (int i = 0; i < modelPack.bones.Count; i++)
                component.cachedBoneKeyFrames.Add((Vector3.Zero, Quaternion.Identity));
            int morphCount = modelPack.morphs.Count;

            component.Weights.Load(morphCount);
            component.stringToMorphIndex.Clear();
            for (int i = 0; i < morphCount; i++)
                component.stringToMorphIndex[modelPack.morphs[i].Name] = i;
        }

        static BoneEntity.IKLink IKLink(in PMX_BoneIKLink ikLink1)
        {
            var ikLink = new BoneEntity.IKLink();

            ikLink.HasLimit = ikLink1.HasLimit;
            ikLink.LimitMax = ikLink1.LimitMax;
            ikLink.LimitMin = ikLink1.LimitMin;
            ikLink.LinkedIndex = ikLink1.LinkedIndex;

            Vector3 tempMin = ikLink.LimitMin;
            Vector3 tempMax = ikLink.LimitMax;
            ikLink.LimitMin = Vector3.Min(tempMin, tempMax);
            ikLink.LimitMax = Vector3.Max(tempMin, tempMax);

            if (ikLink.LimitMin.X > -Math.PI * 0.5 && ikLink.LimitMax.X < Math.PI * 0.5)
                ikLink.TransformOrder = IKTransformOrder.Zxy;
            else if (ikLink.LimitMin.Y > -Math.PI * 0.5 && ikLink.LimitMax.Y < Math.PI * 0.5)
                ikLink.TransformOrder = IKTransformOrder.Xyz;
            else
                ikLink.TransformOrder = IKTransformOrder.Yzx;

            const float epsilon = 1e-6f;
            if (ikLink.HasLimit)
            {
                if (Math.Abs(ikLink.LimitMin.X) < epsilon &&
                    Math.Abs(ikLink.LimitMax.X) < epsilon &&
                    Math.Abs(ikLink.LimitMin.Y) < epsilon &&
                    Math.Abs(ikLink.LimitMax.Y) < epsilon &&
                    Math.Abs(ikLink.LimitMin.Z) < epsilon &&
                    Math.Abs(ikLink.LimitMax.Z) < epsilon)
                {
                    ikLink.FixTypes = AxisFixType.FixAll;
                }
                else if (Math.Abs(ikLink.LimitMin.Y) < epsilon &&
                         Math.Abs(ikLink.LimitMax.Y) < epsilon &&
                         Math.Abs(ikLink.LimitMin.Z) < epsilon &&
                         Math.Abs(ikLink.LimitMax.Z) < epsilon)
                {
                    ikLink.FixTypes = AxisFixType.FixX;
                }
                else if (Math.Abs(ikLink.LimitMin.X) < epsilon &&
                         Math.Abs(ikLink.LimitMax.X) < epsilon &&
                         Math.Abs(ikLink.LimitMin.Z) < epsilon &&
                         Math.Abs(ikLink.LimitMax.Z) < epsilon)
                {
                    ikLink.FixTypes = AxisFixType.FixY;
                }
                else if (Math.Abs(ikLink.LimitMin.X) < epsilon &&
                         Math.Abs(ikLink.LimitMax.X) < epsilon &&
                         Math.Abs(ikLink.LimitMin.Y) < epsilon &&
                         Math.Abs(ikLink.LimitMax.Y) < epsilon)
                {
                    ikLink.FixTypes = AxisFixType.FixZ;
                }
            }
            return ikLink;
        }

        public static RigidBodyDesc Translate(PMX_RigidBody rigidBody)
        {
            RigidBodyDesc desc = new RigidBodyDesc();
            desc.AssociatedBoneIndex = rigidBody.AssociatedBoneIndex;
            desc.CollisionGroup = rigidBody.CollisionGroup;
            desc.CollisionMask = rigidBody.CollisionMask;
            desc.Shape = (RigidBodyShape)rigidBody.Shape;
            desc.Dimemsions = rigidBody.Dimemsions * 0.1f;
            desc.Position = rigidBody.Position * 0.1f;
            desc.Rotation = MMDRendererComponent.ToQuaternion(rigidBody.Rotation);
            desc.Mass = rigidBody.Mass;
            desc.LinearDamping = rigidBody.TranslateDamp;
            desc.AngularDamping = rigidBody.RotateDamp;
            desc.Restitution = rigidBody.Restitution;
            desc.Friction = rigidBody.Friction;
            desc.Type = (RigidBodyType)rigidBody.Type;
            return desc;
        }

        public static JointDesc Translate(PMX_Joint joint)
        {
            JointDesc desc = new JointDesc();
            desc.Type = joint.Type;
            desc.AssociatedRigidBodyIndex1 = joint.AssociatedRigidBodyIndex1;
            desc.AssociatedRigidBodyIndex2 = joint.AssociatedRigidBodyIndex2;
            desc.Position = joint.Position * 0.1f;
            desc.Rotation = joint.Rotation;
            desc.PositionMinimum = joint.PositionMinimum * 0.1f;
            desc.PositionMaximum = joint.PositionMaximum * 0.1f;
            desc.RotationMinimum = joint.RotationMinimum;
            desc.RotationMaximum = joint.RotationMaximum;
            desc.PositionSpring = joint.PositionSpring;
            desc.RotationSpring = joint.RotationSpring;
            return desc;
        }
    }
}
