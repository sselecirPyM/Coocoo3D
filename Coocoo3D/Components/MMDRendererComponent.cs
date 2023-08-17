using Coocoo3D.Present;
using Coocoo3D.ResourceWrap;
using Coocoo3D.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Coocoo3D.Components;

public class MMDRendererComponent
{
    public string meshPath;
    public ModelPack model;

    public List<RenderMaterial> Materials = new();

    public Vector3[] MeshPosition;

    public float[] Weights;
    public List<MorphDesc> Morphs = new();

    internal bool meshNeedUpdate;
    public bool skinning;

    public void ComputeVertexMorph(Vector3[] origin)
    {
        if (!meshNeedUpdate)
            return;
        meshNeedUpdate = false;
        new Span<Vector3>(origin).CopyTo(MeshPosition);

        for (int i = 0; i < Morphs.Count; i++)
        {
            if (Morphs[i].Type != MorphType.Vertex)
                continue;
            MorphVertexDesc[] morphVertices = Morphs[i].MorphVertexs;

            float computedWeight = Weights[i];
            if (computedWeight != 0)
                for (int j = 0; j < morphVertices.Length; j++)
                {
                    MeshPosition[morphVertices[j].VertexIndex] += morphVertices[j].Offset * computedWeight;
                }
        }
    }


    #region bone

    public Matrix4x4[] BoneMatricesData;

    public List<BoneInstance> bones = new();

    public List<RigidBodyDesc> rigidBodyDescs = new();
    public List<JointDesc> jointDescs = new();


    public Matrix4x4 LocalToWorld = Matrix4x4.Identity;
    public Matrix4x4 WorldToLocal = Matrix4x4.Identity;

    public List<int> AppendUpdateMatIndexs = new();
    public List<int> PhysicsUpdateMatrixIndexs = new();

    public void SetTransform(Transform transform)
    {
        LocalToWorld = transform.GetMatrix();
        Matrix4x4.Invert(LocalToWorld, out WorldToLocal);
    }

    public void WriteMatriticesData()
    {
        for (int i = 0; i < bones.Count; i++)
            BoneMatricesData[i] = bones[i].GeneratedTransform;
    }
    public void BoneMorphIKAppend()
    {
        for (int i = 0; i < Morphs.Count; i++)
        {
            if (Morphs[i].Type != MorphType.Bone)
                continue;
            MorphBoneDesc[] morphBoneStructs = Morphs[i].MorphBones;
            float computedWeight = Weights[i];
            for (int j = 0; j < morphBoneStructs.Length; j++)
            {
                var morphBoneStruct = morphBoneStructs[j];
                bones[morphBoneStruct.BoneIndex].rotation *= Quaternion.Slerp(Quaternion.Identity, morphBoneStruct.Rotation, computedWeight);
                bones[morphBoneStruct.BoneIndex].translation += morphBoneStruct.Translation * computedWeight;
            }
        }
        for (int i = 0; i < bones.Count; i++)
        {
            IK(bones[i]);
        }
        UpdateAppendBones();
    }

    public void UpdateAppendBones()
    {
        for (int i = 0; i < bones.Count; i++)
        {
            var bone = bones[i];
            if (bone.IsAppendTranslation)
            {
                var mat1 = bones[bone.AppendParentIndex].GeneratedTransform;
                Matrix4x4.Decompose(mat1, out _, out var rotation, out var translation);
                bone.appendTranslation = translation * bone.AppendRatio;
            }
            if (bone.IsAppendRotation)
            {
                bone.appendRotation = Quaternion.Slerp(Quaternion.Identity, bones[bone.AppendParentIndex].rotation, bone.AppendRatio);
            }
        }
        UpdateMatrices(AppendUpdateMatIndexs);
    }

    void IK(BoneInstance sourceBone)
    {
        if (!sourceBone.EnableIK)
            return;
        int ikTargetIndex = sourceBone.IKTargetIndex;
        if (ikTargetIndex == -1)
            return;
        var ikTargetBone = bones[ikTargetIndex];

        var posTargetFixed = sourceBone.GetPos();


        int h1 = sourceBone.CCDIterateLimit / 2;
        Vector3 posSource = ikTargetBone.GetPos();
        if ((posTargetFixed - posSource).LengthSquared() < 1e-6f)
            return;
        for (int i = 0; i < sourceBone.CCDIterateLimit; i++)
        {
            bool axis_lim = i < h1;
            for (int j = 0; j < sourceBone.boneIKLinks.Length; j++)
            {
                ref var IKLINK = ref sourceBone.boneIKLinks[j];
                BoneInstance itBone = bones[IKLINK.LinkedIndex];

                var itPosition = itBone.GetPos();

                Vector3 targetDirection = Vector3.Normalize(posTargetFixed - itPosition);
                Vector3 ikDirection = Vector3.Normalize(posSource - itPosition);
                float dotV = Math.Clamp(Vector3.Dot(targetDirection, ikDirection), -1, 1);

                Matrix4x4 matXi = Matrix4x4.Transpose(itBone.GeneratedTransform);

                Vector3 ikRotateAxis = SafeNormalize(Vector3.TransformNormal(Vector3.Cross(targetDirection, ikDirection), matXi));

                if (axis_lim)
                    switch (IKLINK.FixTypes)
                    {
                        case AxisFixType.FixX:
                            ikRotateAxis = new Vector3(ikRotateAxis.X >= 0 ? 1 : -1, 0, 0);
                            break;
                        case AxisFixType.FixY:
                            ikRotateAxis = new Vector3(0, ikRotateAxis.Y >= 0 ? 1 : -1, 0);
                            break;
                        case AxisFixType.FixZ:
                            ikRotateAxis = new Vector3(0, 0, ikRotateAxis.Z >= 0 ? 1 : -1);
                            break;
                    }
                var limit = sourceBone.CCDAngleLimit * (i + 1);
                var itResult = Quaternion.Normalize(itBone.rotation * QAxisAngle(ikRotateAxis, -Math.Clamp((float)Math.Acos(dotV), -limit, limit)));

                if (IKLINK.HasLimit)
                {
                    Vector3 angle = Vector3.Zero;
                    switch (IKLINK.TransformOrder)
                    {
                        case IKTransformOrder.Zxy:
                            {
                                Vector3 cachedE = MathHelper.QuaternionToZxy(itResult);
                                angle = LimitAngle(cachedE, axis_lim, IKLINK.LimitMin, IKLINK.LimitMax);
                                itResult = QAxisAngle(Vector3.UnitZ, angle.Z) * QAxisAngle(Vector3.UnitX, angle.X) * QAxisAngle(Vector3.UnitY, angle.Y);
                                break;
                            }
                        case IKTransformOrder.Xyz:
                            {
                                Vector3 cachedE = MathHelper.QuaternionToXyz(itResult);
                                angle = LimitAngle(cachedE, axis_lim, IKLINK.LimitMin, IKLINK.LimitMax);
                                itResult = QAxisAngle(Vector3.UnitX, angle.X) * QAxisAngle(Vector3.UnitY, angle.Y) * QAxisAngle(Vector3.UnitZ, angle.Z);
                                break;
                            }
                        case IKTransformOrder.Yzx:
                            {
                                Vector3 cachedE = MathHelper.QuaternionToYzx(itResult);
                                angle = LimitAngle(cachedE, axis_lim, IKLINK.LimitMin, IKLINK.LimitMax);
                                itResult = QAxisAngle(Vector3.UnitY, angle.Y) * QAxisAngle(Vector3.UnitZ, angle.Z) * QAxisAngle(Vector3.UnitX, angle.X);
                                break;
                            }
                        default:
                            throw new NotImplementedException();
                    }
                }
                itBone.rotation = itResult;


                var it2source = Vector3.TransformNormal(posSource - itPosition, matXi);
                var parent = bones[itBone.ParentIndex];
                var rotatedVec = Vector3.TransformNormal(Vector3.Transform(it2source, itResult), parent.GeneratedTransform);

                posSource = rotatedVec + itPosition;

            }
            UpdateIKMatrices(sourceBone);
            posSource = ikTargetBone.GetPos();
            if ((posTargetFixed - posSource).LengthSquared() < 1e-6f)
                break;
        }
    }

    public void Precompute()
    {
        bool[] bonesTest = new bool[bones.Count];

        Array.Clear(bonesTest, 0, bones.Count);
        AppendUpdateMatIndexs.Clear();
        for (int i = 0; i < bones.Count; i++)
        {
            var bone = bones[i];
            if (bone.ParentIndex != -1)
                bonesTest[i] |= bonesTest[bone.ParentIndex];
            bonesTest[i] |= bone.IsAppendTranslation || bone.IsAppendRotation;
            if (bone.IsPhysicsFreeBone)
                bonesTest[i] = false;
            if (bonesTest[i])
            {
                AppendUpdateMatIndexs.Add(i);
            }
        }
        Array.Clear(bonesTest, 0, bones.Count);
        PhysicsUpdateMatrixIndexs.Clear();
        for (int i = 0; i < bones.Count; i++)
        {
            var bone = bones[i];
            if (bone.ParentIndex == -1)
                continue;
            if (bone.IsPhysicsFreeBone)
                continue;
            var parent = bones[bone.ParentIndex];
            bonesTest[i] |= bonesTest[bone.ParentIndex];
            bonesTest[i] |= parent.IsPhysicsFreeBone;
            if (bonesTest[i])
            {
                PhysicsUpdateMatrixIndexs.Add(i);
            }
        }
    }

    public void UpdateAllMatrix()
    {
        for (int i = 0; i < bones.Count; i++)
            bones[i].GetTransformMatrixG(bones);
    }
    public void UpdateMatrices(List<int> indices)
    {
        for (int i = 0; i < indices.Count; i++)
            bones[indices[i]].GetTransformMatrixG(bones);
    }
    public void UpdateIKMatrices(BoneInstance source)
    {
        var links = source.boneIKLinks;
        for (int i = links.Length - 1; i >= 0; i--)
        {
            bones[links[i].LinkedIndex].GetTransformMatrixG(bones);
        }
        bones[source.IKTargetIndex].GetTransformMatrixG(bones);
    }

    #region helper functions

    //重命名函数以缩短函数名
    static Quaternion QAxisAngle(Vector3 axis, float angle) => Quaternion.CreateFromAxisAngle(axis, angle);

    public static Quaternion ToQuaternion(Vector3 angle)
    {
        return Quaternion.CreateFromYawPitchRoll(angle.Y, angle.X, angle.Z);
    }

    private Vector3 LimitAngle(Vector3 angle, bool axis_lim, Vector3 low, Vector3 high)
    {
        if (!axis_lim)
        {
            return Vector3.Clamp(angle, low, high);
        }
        Vector3 vecL1 = 2.0f * low - angle;
        Vector3 vecH1 = 2.0f * high - angle;
        if (angle.X < low.X)
        {
            angle.X = (vecL1.X <= high.X) ? vecL1.X : low.X;
        }
        else if (angle.X > high.X)
        {
            angle.X = (vecH1.X >= low.X) ? vecH1.X : high.X;
        }
        if (angle.Y < low.Y)
        {
            angle.Y = (vecL1.Y <= high.Y) ? vecL1.Y : low.Y;
        }
        else if (angle.Y > high.Y)
        {
            angle.Y = (vecH1.Y >= low.Y) ? vecH1.Y : high.Y;
        }
        if (angle.Z < low.Z)
        {
            angle.Z = (vecL1.Z <= high.Z) ? vecL1.Z : low.Z;
        }
        else if (angle.Z > high.Z)
        {
            angle.Z = (vecH1.Z >= low.Z) ? vecH1.Z : high.Z;
        }
        return angle;
    }

    Vector3 SafeNormalize(Vector3 vector3)
    {
        float dp3 = Math.Max(0.00001f, Vector3.Dot(vector3, vector3));
        return vector3 / MathF.Sqrt(dp3);
    }
    #endregion

    #endregion

    public void ComputeMotion()
    {
        UpdateAllMatrix();
        BoneMorphIKAppend();
    }

    public MMDRendererComponent GetClone()
    {
        MMDRendererComponent rendererComponent = (MMDRendererComponent)MemberwiseClone();
        rendererComponent.MeshPosition = (Vector3[])MeshPosition.Clone();
        rendererComponent.Materials = Materials.Select(u => u.GetClone()).ToList();
        rendererComponent.bones = bones.Select(u => u.GetClone()).ToList();
        rendererComponent.BoneMatricesData = (Matrix4x4[])BoneMatricesData.Clone();
        rendererComponent.Weights = (float[])Weights.Clone();
        return rendererComponent;
    }
}

public class BoneInstance
{
    public int index;
    public Vector3 staticPosition;
    public Vector3 translation;
    public Quaternion rotation = Quaternion.Identity;
    public Vector3 appendTranslation;
    public Quaternion appendRotation = Quaternion.Identity;

    public Matrix4x4 _generatedTransform = Matrix4x4.Identity;
    public Matrix4x4 GeneratedTransform { get => _generatedTransform; }
    public Matrix4x4 inverseBindMatrix;

    public int ParentIndex = -1;
    public string Name;
    public string NameEN;

    public int IKTargetIndex = -1;
    public int CCDIterateLimit = 0;
    public float CCDAngleLimit = 0;
    public IKLink[] boneIKLinks;

    public int AppendParentIndex = -1;
    public float AppendRatio;
    public bool EnableIK;
    public bool IsAppendRotation;
    public bool IsAppendTranslation;
    public bool IsPhysicsFreeBone;
    public BoneFlags Flags;

    public void GetTransformMatrixG(List<BoneInstance> list)
    {
        var currentPosition = staticPosition + appendTranslation + translation;
        var currentRotation = rotation * appendRotation;

        _generatedTransform = inverseBindMatrix * MatrixExt.Transform(currentPosition, currentRotation);
        if (ParentIndex != -1)
        {
            _generatedTransform *= list[ParentIndex]._generatedTransform;
        }
    }
    public Vector3 GetPos()
    {
        return Vector3.Transform(staticPosition, _generatedTransform);
    }

    public void GetPosRot(out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.Transform(staticPosition, _generatedTransform);
        Matrix4x4.Decompose(_generatedTransform, out _, out rotation, out _);
    }

    public struct IKLink
    {
        public int LinkedIndex;
        public bool HasLimit;
        public Vector3 LimitMin;
        public Vector3 LimitMax;
        public IKTransformOrder TransformOrder;
        public AxisFixType FixTypes;
    }
    public override string ToString()
    {
        return string.Format("{0}_{1}", Name, NameEN);
    }

    public BoneInstance GetClone()
    {
        return (BoneInstance)MemberwiseClone();
    }
}