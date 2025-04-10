using Coocoo3D.Present;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Coocoo3D.Components;

public class AnimationStateComponent
{
    public MMDMotion motion;
    public bool LockMotion;
    public WeightGroup Weights = new();

    public Dictionary<string, int> stringToMorphIndex = new();
    public List<BoneKeyFrame1> cachedBoneKeyFrames = new();

    public float Time;

    public AnimationStateComponent GetClone()
    {
        AnimationStateComponent newComponent = (AnimationStateComponent)MemberwiseClone();
        newComponent.cachedBoneKeyFrames = new(cachedBoneKeyFrames);
        newComponent.stringToMorphIndex = new(stringToMorphIndex);
        newComponent.Weights = Weights.GetClone();

        return newComponent;
    }

    void SetPose(MMDMotion motionComponent, float time)
    {
        foreach (var pair in stringToMorphIndex)
        {
            Weights.Origin[pair.Value] = motionComponent.GetMorphWeight(pair.Key, time);
        }
    }
    void SetPoseDefault()
    {
        foreach (var pair in stringToMorphIndex)
        {
            Weights.Origin[pair.Value] = 0;
        }
    }

    void ComputeWeight(List<MorphDesc> morphs)
    {
        for (int i = 0; i < morphs.Count; i++)
        {
            Weights.Computed[i] = 0;
        }
        for (int i = 0; i < morphs.Count; i++)
        {
            MorphDesc morph = morphs[i];
            if (morph.Type == MorphType.Group)
                ComputeWeightGroup(morphs, morph, Weights.Origin[i], Weights.Computed);
            else
                Weights.Computed[i] += Weights.Origin[i];
        }
    }
    void ComputeWeightGroup(IReadOnlyList<MorphDesc> morphs, MorphDesc morph, float rate, float[] computedWeights)
    {
        for (int i = 0; i < morph.SubMorphs.Length; i++)
        {
            MorphSubMorphDesc subMorphStruct = morph.SubMorphs[i];
            MorphDesc subMorph = morphs[subMorphStruct.GroupIndex];
            if (subMorph.Type == MorphType.Group)
                ComputeWeightGroup(morphs, subMorph, rate * subMorphStruct.Rate, computedWeights);
            else
                computedWeights[subMorphStruct.GroupIndex] += rate * subMorphStruct.Rate;
        }
    }


    void SetBonePoseMotion(MMDRendererComponent renderer, float time, MMDMotion motion)
    {
        foreach (var bone in renderer.bones)
        {
            var keyframe = motion.GetBoneMotion(bone.Name, time);
            bone.rotation = keyframe.Rotation;
            bone.translation = keyframe.Position;
            cachedBoneKeyFrames[bone.index] = keyframe;
        }
        foreach(var bone in renderer.ikBones)
        {
            bone.EnableIK = motion.GetIKState(bone.Name, time);
        }
    }
    void SetBonePoseDefault(MMDRendererComponent renderer)
    {
        foreach (var bone in renderer.bones)
        {
            var keyframe = new BoneKeyFrame1
            {
                Position = Vector3.Zero,
                Rotation = Quaternion.Identity
            };
            bone.rotation = keyframe.Rotation;
            bone.translation = keyframe.Position;
            cachedBoneKeyFrames[bone.index] = keyframe;
        }
        foreach (var bone in renderer.ikBones)
        {
            bone.EnableIK = true;
        }
    }
    void SetBoneDefaultPose(List<BoneInstance> bones)
    {
        for (int i = 0; i < bones.Count; i++)
        {
            var keyframe = cachedBoneKeyFrames[i];
            bones[i].rotation = keyframe.Rotation;
            bones[i].translation = keyframe.Position;
        }
    }

    public void ComputeMotion(MMDMotion motion, MMDRendererComponent renderer)
    {
        List<MorphDesc> morphs = renderer.Morphs;
        List<BoneInstance> bones = renderer.bones;

        if (!LockMotion)
        {
            if (motion != null)
                SetPose(motion, Time);
            else
                SetPoseDefault();
        }

        ComputeWeight(morphs);

        if (!LockMotion)
        {
            if (motion != null)
                SetBonePoseMotion(renderer, Time, motion);
            else
                SetBonePoseDefault(renderer);
        }
        else
        {
            SetBoneDefaultPose(bones);
        }


        for (int j = 0; j < renderer.Morphs.Count; j++)
        {
            if (renderer.Morphs[j].Type == MorphType.Vertex && Weights.Computed[j] != renderer.MorphWeights[j])
            {
                renderer.meshNeedUpdate = true;
                break;
            }
        }
        Weights.Computed.CopyTo(renderer.MorphWeights, 0);
    }
}

public class WeightGroup
{
    public float[] Origin;
    public float[] Computed;
    public void Load(int count)
    {
        Origin = new float[count];
        Computed = new float[count];
    }

    public WeightGroup GetClone()
    {
        var clone = (WeightGroup)MemberwiseClone();
        clone.Origin = new float[Origin.Length];
        clone.Computed = new float[Computed.Length];
        Array.Copy(Origin, clone.Origin, Origin.Length);
        Array.Copy(Computed, clone.Computed, Computed.Length);
        return clone;
    }
}