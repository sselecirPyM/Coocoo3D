using Arch.Core;
using Arch.Core.Extensions;
using Coocoo3D.Components;
using Coocoo3D.Core;
using Coocoo3D.Present;
using Coocoo3D.ResourceWrap;
using ImageMagick;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Coocoo3D.Extensions.FileFormat;

public static class PMXFormatExtension
{
    public static MMDRendererComponent LoadPmx(this Entity entity, Scene scene, ModelPack modelPack, Transform transform)
    {
        entity.Add(new ObjectDescription
        {
            Name = modelPack.name,
            Description = modelPack.description,
        });
        entity.Add(transform);

        var renderer = new MMDRendererComponent();
        renderer.skinning = true;
        renderer.Morphs.Clear();
        renderer.Morphs.AddRange(modelPack.morphs);

        renderer.BoneMatricesData = new Matrix4x4[modelPack.bones.Count];

        foreach (var bone in modelPack.bones)
            renderer.bones.Add(bone.GetClone());
        foreach (var ikBone in modelPack.ikBones)
            renderer.ikBones.Add(ikBone.GetClone());
        foreach (var appendBone in modelPack.appendBones)
            renderer.appendBones.Add(appendBone.GetClone());

        renderer.rigidBodyDescs.AddRange(modelPack.rigidBodyDescs);
        for (int i = 0; i < modelPack.rigidBodyDescs.Count; i++)
        {
            var rigidBodyDesc = renderer.rigidBodyDescs[i];

            if (rigidBodyDesc.Type != RigidBodyType.Kinematic && rigidBodyDesc.AssociatedBoneIndex != -1)
                renderer.bones[rigidBodyDesc.AssociatedBoneIndex].IsPhysicsFreeBone = true;
        }
        renderer.jointDescs.AddRange(modelPack.jointDescs);
        renderer.Precompute();


        renderer.LoadMesh(modelPack);
        scene.AddComponent(entity, renderer);
        //entity.Add(renderer);
        return renderer;
    }

    public static AnimationStateComponent LoadAnimationState(this Entity entity)
    {
        entity.TryGet<MMDRendererComponent>(out var renderer);

        var animationState = new AnimationStateComponent();
        entity.Add(animationState);
        animationState.LoadAnimationStates(renderer.model.bones, renderer.model.morphs);
        return animationState;
    }

    static void LoadMesh(this MMDRendererComponent renderer, ModelPack modelPack)
    {
        renderer.Materials.Clear();
        foreach (RenderMaterial v in modelPack.Materials)
        {
            renderer.Materials.Add(v.GetClone());
        }
        renderer.MorphWeights = new float[modelPack.morphs.Count];

        var mesh = modelPack.GetMesh();
        renderer.meshPath = modelPack.fullPath;
        renderer.model = modelPack;
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
            Offset = desc.Offset,
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
            Translation = desc.Translation,
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
        if (desc.MorphVertice != null)
        {
            morphVertexDescs = new MorphVertexDesc[desc.MorphVertice.Length];
            for (int i = 0; i < desc.MorphVertice.Length; i++)
                morphVertexDescs[i] = Translate(desc.MorphVertice[i]);
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

    public static BoneInstance Translate(PMX_Bone _bone, int index, int boneCount)
    {
        BoneInstance boneInstance = new();
        boneInstance.ParentIndex = (_bone.ParentIndex >= 0 && _bone.ParentIndex < boneCount) ? _bone.ParentIndex : -1;
        boneInstance.restPosition = _bone.Position;
        boneInstance.rotation = Quaternion.Identity;
        boneInstance.index = index;
        boneInstance.inverseBindMatrix = Matrix4x4.CreateTranslation(-boneInstance.restPosition);

        boneInstance.Name = _bone.Name;
        boneInstance.NameEN = _bone.NameEN;
        boneInstance.Flags = (BoneFlags)_bone.Flags;
        boneInstance.children = new List<int>();
        return boneInstance;
    }

    public static void AddIKBone(List<IKBone> ikBones, PMX_Bone _bone, int index)
    {
        if (!_bone.Flags.HasFlag(PMX_BoneFlag.HasIK))
            return;
        var ikBone = new IKBone();

        ikBone.index = index;
        ikBone.Name = _bone.Name;
        ikBone.target = _bone.boneIK.IKTargetIndex;
        ikBone.CCDIterateLimit = _bone.boneIK.CCDIterateLimit;
        ikBone.CCDAngleLimit = _bone.boneIK.CCDAngleLimit;
        ikBone.ikLinks = new IKBone.IKLink[_bone.boneIK.IKLinks.Length];
        for (int j = 0; j < ikBone.ikLinks.Length; j++)
            ikBone.ikLinks[j] = IKLink(_bone.boneIK.IKLinks[j]);
        ikBone.EnableIK = true;
        ikBones.Add(ikBone);
    }

    public static void AddAppendBone(List<AppendBone> appendBones, PMX_Bone _bone, int index)
    {
        if (_bone.AppendBoneIndex == -1)
            return;
        var appendBone = new AppendBone();
        appendBone.index = index;
        appendBone.AppendParentIndex = _bone.AppendBoneIndex;
        appendBone.AppendRatio = _bone.AppendBoneRatio;
        appendBone.IsAppendRotation = _bone.Flags.HasFlag(PMX_BoneFlag.AcquireRotate);
        appendBone.IsAppendTranslation = _bone.Flags.HasFlag(PMX_BoneFlag.AcquireTranslate);
        appendBones.Add(appendBone);
    }

    static void LoadAnimationStates(this AnimationStateComponent component, IList<BoneInstance> bones, IList<MorphDesc> morphs)
    {
        component.cachedBoneKeyFrames.Clear();
        for (int i = 0; i < bones.Count; i++)
            component.cachedBoneKeyFrames.Add(new(Vector3.Zero, Quaternion.Identity));
        int morphCount = morphs.Count;

        component.Weights.Load(morphCount);
        component.stringToMorphIndex.Clear();
        for (int i = 0; i < morphCount; i++)
            component.stringToMorphIndex[morphs[i].Name] = i;
    }

    static IKBone.IKLink IKLink(in PMX_BoneIKLink ikLink1)
    {
        var ikLink = new IKBone.IKLink();

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
            uint a = 0;
            if (Math.Abs(ikLink.LimitMin.X) < epsilon &&
                    Math.Abs(ikLink.LimitMax.X) < epsilon)
            {
                a |= 1;
            }
            if (Math.Abs(ikLink.LimitMin.Y) < epsilon &&
                    Math.Abs(ikLink.LimitMax.Y) < epsilon)
            {
                a |= 2;
            }
            if (Math.Abs(ikLink.LimitMin.Z) < epsilon &&
                    Math.Abs(ikLink.LimitMax.Z) < epsilon)
            {
                a |= 4;
            }
            ikLink.FixTypes = a switch
            {
                7 => AxisFixType.FixAll,
                6 => AxisFixType.FixX,
                5 => AxisFixType.FixY,
                3 => AxisFixType.FixZ,
                _ => AxisFixType.FixNone
            };
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
        desc.Dimensions = rigidBody.Dimensions;
        desc.Mass = rigidBody.Mass;
        desc.LinearDamping = rigidBody.TranslateDamp;
        desc.AngularDamping = rigidBody.RotateDamp;
        desc.Restitution = rigidBody.Restitution;
        desc.Friction = rigidBody.Friction;
        desc.Type = (RigidBodyType)rigidBody.Type;

        desc.transform = GetTransform(rigidBody.Position, rigidBody.Rotation);
        Matrix4x4.Invert(desc.transform, out desc.invertTransform);
        return desc;
    }

    public static JointDesc Translate(PMX_Joint joint)
    {
        JointDesc desc = new JointDesc();
        desc.Type = joint.Type;
        desc.AssociatedRigidBodyIndex1 = joint.AssociatedRigidBodyIndex1;
        desc.AssociatedRigidBodyIndex2 = joint.AssociatedRigidBodyIndex2;
        desc.LinearMinimum = joint.LinearMinimum;
        desc.LinearMaximum = joint.LinearMaximum;
        desc.AngularMinimum = joint.AngularMinimum;
        desc.AngularMaximum = joint.AngularMaximum;
        desc.LinearSpring = joint.LinearSpring;
        desc.AngularSpring = joint.RotationSpring;

        desc.transform = GetTransform(joint.Position, joint.Rotation);
        Matrix4x4.Invert(desc.transform, out desc.invertTransform);
        return desc;
    }

    public static Matrix4x4 GetTransform(Vector3 position, Vector3 rotation)
    {
        return Matrix4x4.CreateFromYawPitchRoll(rotation.Y, rotation.X, rotation.Z) * Matrix4x4.CreateTranslation(position);
    }
}
