using Coocoo3D.Present;
using Coocoo3D.Numerics;
using Coocoo3D.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Coocoo3D.ResourceWrap;

namespace Coocoo3D.Components
{
    public class MMDRendererComponent : Component
    {
        public string meshPath;
        public string motionPath = "";

        public MMDMorphStateComponent morphStateComponent = new();
        public bool LockMotion;

        public bool skinning;
        public bool enableIK = true;

        public List<RenderMaterial> Materials = new();

        public Vector3[] meshPositionCache;
        public bool meshNeedUpdate;

        public void ComputeVertexMorph(ModelPack model)
        {
            var morphs = morphStateComponent.morphs;
            for (int i = 0; i < morphs.Count; i++)
            {
                if (morphs[i].Type == MorphType.Vertex && morphStateComponent.IsWeightChanged(i))
                {
                    meshNeedUpdate = true;
                }
            }
            if (!meshNeedUpdate) return;
            new Span<Vector3>(model.position).CopyTo(meshPositionCache);

            for (int i = 0; i < morphs.Count; i++)
            {
                if (morphs[i].Type != MorphType.Vertex) continue;
                MorphVertexDesc[] morphVertices = morphs[i].MorphVertexs;

                float computedWeight = morphStateComponent.Weights.Computed[i];
                if (computedWeight != 0)
                    for (int j = 0; j < morphVertices.Length; j++)
                    {
                        meshPositionCache[morphVertices[j].VertexIndex] += morphVertices[j].Offset * computedWeight;
                    }
            }
        }


        #region bone

        public Matrix4x4[] boneMatricesData;

        public List<BoneEntity> bones = new();
        public List<(Vector3, Quaternion)> cachedBoneKeyFrames = new();

        public List<RigidBodyDesc> rigidBodyDescs = new();
        public List<JointDesc> jointDescs = new();


        public Matrix4x4 LocalToWorld = Matrix4x4.Identity;
        public Matrix4x4 WorldToLocal = Matrix4x4.Identity;

        public List<int> AppendNeedUpdateMatIndexs = new();
        public List<int> PhysicsNeedUpdateMatrixIndexs = new();

        public void SetTransform(Transform transform)
        {
            LocalToWorld = transform.GetMatrix();
            Matrix4x4.Invert(LocalToWorld, out WorldToLocal);
        }

        public void WriteMatriticesData()
        {
            for (int i = 0; i < bones.Count; i++)
                boneMatricesData[i] = bones[i].GeneratedTransform;
        }
        public void SetPoseWithMotion(float time, MMDMotion motion)
        {
            morphStateComponent.SetPose(motion, time);
            morphStateComponent.ComputeWeight();
            foreach (var bone in bones)
            {
                var keyframe = motion.GetBoneMotion(bone.Name, time);
                bone.rotation = keyframe.Item2;
                bone.dynamicPosition = keyframe.Item1;
                cachedBoneKeyFrames[bone.index] = keyframe;
            }
        }
        public void SetPoseDefault()
        {
            morphStateComponent.SetPoseDefault();
            morphStateComponent.ComputeWeight();
            foreach (var bone in bones)
            {
                var keyframe = new ValueTuple<Vector3, Quaternion>(Vector3.Zero, Quaternion.Identity);
                bone.rotation = keyframe.Item2;
                bone.dynamicPosition = keyframe.Item1;
                cachedBoneKeyFrames[bone.index] = keyframe;
            }
        }
        public void BoneMorphIKAppend()
        {
            for (int i = 0; i < morphStateComponent.morphs.Count; i++)
            {
                if (morphStateComponent.morphs[i].Type != MorphType.Bone) continue;
                MorphBoneDesc[] morphBoneStructs = morphStateComponent.morphs[i].MorphBones;
                float computedWeight = morphStateComponent.Weights.Computed[i];
                for (int j = 0; j < morphBoneStructs.Length; j++)
                {
                    var morphBoneStruct = morphBoneStructs[j];
                    bones[morphBoneStruct.BoneIndex].rotation *= Quaternion.Slerp(Quaternion.Identity, morphBoneStruct.Rotation, computedWeight);
                    bones[morphBoneStruct.BoneIndex].dynamicPosition += morphBoneStruct.Translation * computedWeight;
                }
            }
            if (enableIK)
                for (int i = 0; i < bones.Count; i++)
                {
                    IK(i, bones);
                }
            UpdateAppendBones();
        }

        public void SetPoseNoMotion()
        {
            morphStateComponent.ComputeWeight();
            for (int i = 0; i < bones.Count; i++)
            {
                var keyframe = cachedBoneKeyFrames[i];
                bones[i].rotation = keyframe.Item2;
                bones[i].dynamicPosition = keyframe.Item1;
            }
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
            UpdateMatrices(AppendNeedUpdateMatIndexs);
        }

        void IK(int boneIndex, List<BoneEntity> bones)
        {
            int ikTargetIndex = bones[boneIndex].IKTargetIndex;
            if (ikTargetIndex == -1) return;
            var entity = bones[boneIndex];
            var entitySource = bones[ikTargetIndex];

            entity.GetPosRot2(out var posTarget, out var rot0);


            int h1 = entity.CCDIterateLimit / 2;
            Vector3 posSource = entitySource.GetPos2();
            if ((posTarget - posSource).LengthSquared() < 1e-6f) return;
            for (int i = 0; i < entity.CCDIterateLimit; i++)
            {
                bool axis_lim = i < h1;
                for (int j = 0; j < entity.boneIKLinks.Length; j++)
                {
                    posSource = entitySource.GetPos2();
                    ref var IKLINK = ref entity.boneIKLinks[j];
                    BoneEntity itEntity = bones[IKLINK.LinkedIndex];

                    itEntity.GetPosRot2(out var itPosition, out var itRot);

                    Vector3 targetDirection = Vector3.Normalize(itPosition - posTarget);
                    Vector3 ikDirection = Vector3.Normalize(itPosition - posSource);
                    float dotV = Math.Clamp(Vector3.Dot(targetDirection, ikDirection), -1, 1);

                    Matrix4x4 matXi = Matrix4x4.Transpose(itEntity.GeneratedTransform);
                    Vector3 ikRotateAxis = SafeNormalize(Vector3.TransformNormal(Vector3.Cross(targetDirection, ikDirection), matXi));

                    if (axis_lim)
                        switch (IKLINK.FixTypes)
                        {
                            case AxisFixType.FixX:
                                ikRotateAxis.X = ikRotateAxis.X >= 0 ? 1 : -1;
                                ikRotateAxis.Y = 0;
                                ikRotateAxis.Z = 0;
                                break;
                            case AxisFixType.FixY:
                                ikRotateAxis.X = 0;
                                ikRotateAxis.Y = ikRotateAxis.Y >= 0 ? 1 : -1;
                                ikRotateAxis.Z = 0;
                                break;
                            case AxisFixType.FixZ:
                                ikRotateAxis.X = 0;
                                ikRotateAxis.Y = 0;
                                ikRotateAxis.Z = ikRotateAxis.Z >= 0 ? 1 : -1;
                                break;
                        }
                    //重命名函数以缩短函数名
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    static Quaternion QAxisAngle(Vector3 axis, float angle) => Quaternion.CreateFromAxisAngle(axis, angle);

                    itEntity.rotation = Quaternion.Normalize(itEntity.rotation * QAxisAngle(ikRotateAxis, -Math.Min((float)Math.Acos(dotV), entity.CCDAngleLimit * (i + 1))));

                    if (IKLINK.HasLimit)
                    {
                        Vector3 angle = Vector3.Zero;
                        switch (IKLINK.TransformOrder)
                        {
                            case IKTransformOrder.Zxy:
                                {
                                    angle = MathHelper.QuaternionToZxy(itEntity.rotation);
                                    Vector3 cachedE = angle;
                                    angle = LimitAngle(angle, axis_lim, IKLINK.LimitMin, IKLINK.LimitMax);
                                    if (cachedE != angle)
                                        itEntity.rotation = Quaternion.Normalize(QAxisAngle(Vector3.UnitZ, angle.Z) * QAxisAngle(Vector3.UnitX, angle.X) * QAxisAngle(Vector3.UnitY, angle.Y));
                                    break;
                                }
                            case IKTransformOrder.Xyz:
                                {
                                    angle = MathHelper.QuaternionToXyz(itEntity.rotation);
                                    Vector3 cachedE = angle;
                                    angle = LimitAngle(angle, axis_lim, IKLINK.LimitMin, IKLINK.LimitMax);
                                    if (cachedE != angle)
                                        itEntity.rotation = Quaternion.Normalize(QAxisAngle(Vector3.UnitX, angle.X) * QAxisAngle(Vector3.UnitY, angle.Y) * QAxisAngle(Vector3.UnitZ, angle.Z));
                                    break;
                                }
                            case IKTransformOrder.Yzx:
                                {
                                    angle = MathHelper.QuaternionToYzx(itEntity.rotation);
                                    Vector3 cachedE = angle;
                                    angle = LimitAngle(angle, axis_lim, IKLINK.LimitMin, IKLINK.LimitMax);
                                    if (cachedE != angle)
                                        itEntity.rotation = Quaternion.Normalize(QAxisAngle(Vector3.UnitY, angle.Y) * QAxisAngle(Vector3.UnitZ, angle.Z) * QAxisAngle(Vector3.UnitX, angle.X));
                                    break;
                                }
                            default:
                                throw new NotImplementedException();
                        }
                    }
                    UpdateMatrices(entity.IKChildrenIndex[j]);
                }
                posSource = entitySource.GetPos2();
                if ((posTarget - posSource).LengthSquared() < 1e-6f) break;
            }
        }

        public void Bake()
        {
            bool[] bonesTest = new bool[bones.Count];

            for (int i = 0; i < bones.Count; i++)
            {
                int ikTargetIndex = bones[i].IKTargetIndex;
                if (ikTargetIndex == -1) continue;
                List<List<int>> ax = new();
                var bone = bones[i];
                for (int j = 0; j < bone.boneIKLinks.Length; j++)
                {
                    List<int> bx = new List<int>();

                    Array.Clear(bonesTest, 0, bones.Count);
                    bonesTest[bone.boneIKLinks[j].LinkedIndex] = true;
                    for (int k = 0; k < bones.Count; k++)
                    {
                        if (bones[k].ParentIndex == -1) continue;
                        bonesTest[k] |= bonesTest[bones[k].ParentIndex];
                        if (bonesTest[k])
                            bx.Add(k);
                    }
                    ax.Add(bx);
                }
                bone.IKChildrenIndex = ax;
            }
            Array.Clear(bonesTest, 0, bones.Count);
            AppendNeedUpdateMatIndexs.Clear();
            for (int i = 0; i < bones.Count; i++)
            {
                var bone = bones[i];
                if (bone.ParentIndex != -1)
                    bonesTest[i] |= bonesTest[bone.ParentIndex];
                bonesTest[i] |= bone.IsAppendTranslation || bone.IsAppendRotation;
                if (bonesTest[i])
                {
                    AppendNeedUpdateMatIndexs.Add(i);
                }
            }
            Array.Clear(bonesTest, 0, bones.Count);
            PhysicsNeedUpdateMatrixIndexs.Clear();
            for (int i = 0; i < bones.Count; i++)
            {
                var bone = bones[i];
                if (bone.ParentIndex == -1)
                    continue;
                var parent = bones[bone.ParentIndex];
                bonesTest[i] |= bonesTest[bone.ParentIndex];
                bonesTest[i] |= parent.IsPhysicsFreeBone;
                if (bonesTest[i])
                {
                    PhysicsNeedUpdateMatrixIndexs.Add(i);
                }
            }
        }

        public void UpdateAllMatrix()
        {
            for (int i = 0; i < bones.Count; i++)
                bones[i].GetTransformMatrixG(bones);
        }
        public void UpdateMatrices(List<int> indexs)
        {
            for (int i = 0; i < indexs.Count; i++)
                bones[indexs[i]].GetTransformMatrixG(bones);
        }

        #region helper functions

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
        public void ComputeMotion(float time, MMDMotion motion)
        {
            if (!LockMotion)
            {
                if (motion != null)
                    SetPoseWithMotion(time, motion);
                else
                    SetPoseDefault();
            }
            else
            {
                SetPoseNoMotion();
            }
            UpdateAllMatrix();
            BoneMorphIKAppend();
        }
    }

    public class BoneEntity
    {
        public int index;
        public Vector3 staticPosition;
        public Vector3 dynamicPosition;
        public Quaternion rotation = Quaternion.Identity;
        public Vector3 appendTranslation;
        public Quaternion appendRotation = Quaternion.Identity;

        public Matrix4x4 _generatedTransform = Matrix4x4.Identity;
        public Matrix4x4 GeneratedTransform { get => _generatedTransform; }

        public int ParentIndex = -1;
        public string Name;
        public string NameEN;

        public int IKTargetIndex = -1;
        public int CCDIterateLimit = 0;
        public float CCDAngleLimit = 0;
        public IKLink[] boneIKLinks;

        public int AppendParentIndex = -1;
        public float AppendRatio;
        public bool IsAppendRotation;
        public bool IsAppendTranslation;
        public bool IsPhysicsFreeBone;
        public BoneFlags Flags;
        public List<List<int>> IKChildrenIndex;

        public void GetTransformMatrixG(List<BoneEntity> list)
        {
            if (ParentIndex != -1)
            {
                _generatedTransform = Matrix4x4.CreateTranslation(-staticPosition) *
                    MatrixExt.Transform(staticPosition + appendTranslation + dynamicPosition, rotation * appendRotation) * list[ParentIndex]._generatedTransform;
            }
            else
            {
                _generatedTransform = Matrix4x4.CreateTranslation(-staticPosition) *
                    MatrixExt.Transform(staticPosition + appendTranslation + dynamicPosition, rotation * appendRotation);
            }
        }
        public Vector3 GetPos2()
        {
            return Vector3.Transform(staticPosition, _generatedTransform);
        }

        public void GetPosRot2(out Vector3 pos, out Quaternion rot)
        {
            pos = Vector3.Transform(staticPosition, _generatedTransform);
            Matrix4x4.Decompose(_generatedTransform, out _, out rot, out _);
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

        public BoneEntity GetClone()
        {
            return (BoneEntity)MemberwiseClone();
        }
    }
}