using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.Components
{
    public class MMDMorphStateComponent
    {
        public List<MorphDesc> morphs = new();
        public WeightGroup Weights = new ();

        public const float c_frameInterval = 1 / 30.0f;
        public Dictionary<string, int> stringToMorphIndex = new();
        public void SetPose(MMDMotion motionComponent, float time)
        {
            foreach (var pair in stringToMorphIndex)
            {
                Weights.Origin[pair.Value] = motionComponent.GetMorphWeight(pair.Key, time);
            }
        }
        public void SetPoseDefault()
        {
            foreach (var pair in stringToMorphIndex)
            {
                Weights.Origin[pair.Value] = 0;
            }
        }

        public void ComputeWeight()
        {
            ComputeWeight1();
        }

        void ComputeWeight1()
        {
            WeightGroup weightGroup = Weights;
            for (int i = 0; i < morphs.Count; i++)
            {
                weightGroup.ComputedPrev[i] = weightGroup.Computed[i];
                weightGroup.Computed[i] = 0;
            }
            for (int i = 0; i < morphs.Count; i++)
            {
                MorphDesc morph = morphs[i];
                if (morph.Type == MorphType.Group)
                    ComputeWeightGroup(morphs, morph, weightGroup.Origin[i], weightGroup.Computed);
                else
                    weightGroup.Computed[i] += weightGroup.Origin[i];
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
        public bool IsWeightChanged(int index)
        {
            return Weights.Computed[index] != Weights.ComputedPrev[index];
        }
    }

    public class WeightGroup
    {
        public float[] Origin;
        public float[] Computed;
        public float[] ComputedPrev;
        public void Load(int count)
        {
            Origin = new float[count];
            Computed = new float[count];
            ComputedPrev = new float[count];
        }
    }

    public enum MorphCategory
    {
        System = 0,
        Eyebrow = 1,
        Eye = 2,
        Mouth = 3,
        Other = 4,
    };
    public enum MorphMaterialMethon
    {
        Mul = 0,
        Add = 1,
    };

    public struct MorphSubMorphDesc
    {
        public int GroupIndex;
        public float Rate;
        public override string ToString()
        {
            return string.Format("{0},{1}", GroupIndex, Rate);
        }
    }
    public struct MorphMaterialDesc
    {
        public int MaterialIndex;
        public MorphMaterialMethon MorphMethon;
        public Vector4 Diffuse;
        public Vector4 Specular;
        public Vector3 Ambient;
        public Vector4 EdgeColor;
        public float EdgeSize;
        public Vector4 Texture;
        public Vector4 SubTexture;
        public Vector4 ToonTexture;
    }
    public struct MorphUVDesc
    {
        public int VertexIndex;
        public Vector4 Offset;
    }

    public class MorphDesc
    {
        public string Name;
        public string NameEN;
        public MorphCategory Category;
        public MorphType Type;

        public MorphSubMorphDesc[] SubMorphs;
        public MorphVertexDesc[] MorphVertexs;
        public MorphBoneDesc[] MorphBones;
        public MorphUVDesc[] MorphUVs;
        public MorphMaterialDesc[] MorphMaterials;

        public override string ToString()
        {
            return string.Format("{0}", Name);
        }
    }
}