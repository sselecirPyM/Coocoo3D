using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.FileFormat
{
    enum PMX_BoneWeightDeformType
    {
        BDEF1 = 0,
        BDEF2 = 1,
        BDEF4 = 2,
        SDEF = 3,
        QDEF = 4
    }

    public struct PMX_Vertex
    {
        public Vector3 Coordinate;
        public Vector3 Normal;
        public Vector2 UvCoordinate;
        public float EdgeScale;

        public Vector4[] ExtraUvCoordinate;
        public int boneId0;
        public int boneId1;
        public int boneId2;
        public int boneId3;
        public Vector4 Weights;
        public override string ToString()
        {
            return Coordinate.ToString();
        }
    }

    public struct PMX_Texture
    {
        public string TexturePath;
        public override string ToString()
        {
            return string.Format("{0}", TexturePath);
        }
    }

    public class PMX_Material
    {
        public string Name;
        public string NameEN;
        public Vector4 DiffuseColor;
        public Vector4 SpecularColor;
        public Vector3 AmbientColor;
        public PMX_DrawFlag DrawFlags;
        public Vector4 EdgeColor;
        public float EdgeScale;
        public int TextureIndex;
        public int SecondaryTextureIndex;
        public byte SecondaryTextureType;
        public bool UseToon;
        public int ToonIndex;
        public string Meta;
        public int TriangeIndexStartNum;
        public int TriangeIndexNum;
        public override string ToString()
        {
            return string.Format("{0}", Name);
        }
    }

    public enum PMX_BoneFlag
    {
        ChildUseId = 1,
        Rotatable = 2,
        Movable = 4,
        Visible = 8,
        Controllable = 16,
        HasIK = 32,
        AcquireRotate = 256,
        AcquireTranslate = 512,
        RotAxisFixed = 1024,
        UseLocalAxis = 2048,
        PostPhysics = 4096,
        ReceiveTransform = 8192
    }
    public class PMX_BoneIKLink
    {
        public int LinkedIndex;
        public bool HasLimit;
        public Vector3 LimitMin;
        public Vector3 LimitMax;
    }

    public class PMX_BoneIK
    {
        public int IKTargetIndex;
        public int CCDIterateLimit;
        public float CCDAngleLimit;
        public PMX_BoneIKLink[] IKLinks;
    }

    public class PMX_Bone
    {
        public string Name;
        public string NameEN;
        public Vector3 Position;
        public int ParentIndex;
        public int TransformLevel;
        public PMX_BoneFlag Flags;
        public int ChildId;
        public Vector3 ChildOffset;
        public Vector3 RotAxisFixed;

        public int AppendBoneIndex;
        public float AppendBoneRatio;

        public Vector3 LocalAxisX;
        public Vector3 LocalAxisY;
        public Vector3 LocalAxisZ;

        public int ExportKey;

        public PMX_BoneIK boneIK;
        public override string ToString()
        {
            return string.Format("{0}", Name);
        }
    }

    public enum PMX_DrawFlag
    {
        DrawDoubleFace = 1,
        DrawGroundShadow = 2,
        CastSelfShadow = 4,
        DrawSelfShadow = 8,
        DrawEdge = 16,
    }

    public enum PMX_MorphType
    {
        Group = 0,
        Vertex = 1,
        Bone = 2,
        UV = 3,
        ExtUV1 = 4,
        ExtUV2 = 5,
        ExtUV3 = 6,
        ExtUV4 = 7,
        Material = 8
    }

    public enum PMX_MorphCategory
    {
        System = 0,
        Eyebrow = 1,
        Eye = 2,
        Mouth = 3,
        Other = 4,
    };
    public enum PMX_MorphMaterialMethon
    {
        Mul = 0,
        Add = 1,
    };
    public struct PMX_MorphSubMorphDesc
    {
        public int GroupIndex;
        public float Rate;
    }
    public struct PMX_MorphUVDesc
    {
        public int VertexIndex;
        public Vector4 Offset;
    }

    public struct PMX_MorphMaterialDesc
    {
        public int MaterialIndex;
        public PMX_MorphMaterialMethon MorphMethon;
        public Vector4 Diffuse;
        public Vector4 Specular;
        public Vector3 Ambient;
        public Vector4 EdgeColor;
        public float EdgeSize;
        public Vector4 Texture;
        public Vector4 SubTexture;
        public Vector4 ToonTexture;
    }

    public struct PMX_MorphVertexDesc
    {
        public int VertexIndex;
        public Vector3 Offset;
    }

    public struct PMX_MorphBoneDesc
    {
        public int BoneIndex;
        public Vector3 Translation;
        public Quaternion Rotation;
    }

    public class PMX_Morph
    {
        public string Name;
        public string NameEN;
        public PMX_MorphCategory Category;
        public PMX_MorphType Type;

        public PMX_MorphSubMorphDesc[] SubMorphs;
        public PMX_MorphVertexDesc[] MorphVertexs;
        public PMX_MorphBoneDesc[] MorphBones;
        public PMX_MorphUVDesc[] MorphUVs;
        public PMX_MorphMaterialDesc[] MorphMaterials;

        public override string ToString()
        {
            return string.Format("{0}", Name);
        }
    }

    public class PMX_Joint
    {
        public string Name;
        public string NameEN;
        public byte Type;
        public int AssociatedRigidBodyIndex1;
        public int AssociatedRigidBodyIndex2;
        public Vector3 Position;
        public Vector3 Rotation;
        public Vector3 PositionMinimum;
        public Vector3 PositionMaximum;
        public Vector3 RotationMinimum;
        public Vector3 RotationMaximum;
        public Vector3 PositionSpring;
        public Vector3 RotationSpring;
        public override string ToString()
        {
            return string.Format("{0}", Name);
        }
    }

    public enum PMX_RigidBodyType
    {
        Kinematic = 0,
        Physics = 1,
        PhysicsStrict = 2,
        PhysicsGhost = 3
    }

    public enum PMX_RigidBodyShape
    {
        Sphere = 0,
        Box = 1,
        Capsule = 2
    }

    public class PMX_RigidBody
    {
        public string Name;
        public string NameEN;
        public int AssociatedBoneIndex;
        public byte CollisionGroup;
        public ushort CollisionMask;
        public PMX_RigidBodyShape Shape;
        public Vector3 Dimemsions;
        public Vector3 Position;
        public Vector3 Rotation;
        public float Mass;
        public float TranslateDamp;
        public float RotateDamp;
        public float Restitution;
        public float Friction;
        public PMX_RigidBodyType Type;
        public override string ToString()
        {
            return string.Format("{0}", Name);
        }
    }

    public struct PMX_EntryElement
    {
        public byte Type;
        public int Index;
        public override string ToString()
        {
            return string.Format("{0},{1}", Type, Index);
        }
    }

    public class PMX_Entry
    {
        public string Name;
        public string NameEN;
        public PMX_EntryElement[] elements;
        public override string ToString()
        {
            return string.Format("{0}", Name);
        }
    }

    public class PMXFormat
    {
        public bool Ready;

        public string Name;
        public string NameEN;
        public string Description;
        public string DescriptionEN;

        public PMX_Vertex[] Vertices;
        public int[] TriangleIndexs;
        public List<PMX_Texture> Textures = new List<PMX_Texture>();
        public List<PMX_Material> Materials = new List<PMX_Material>();
        public List<PMX_Bone> Bones = new List<PMX_Bone>();
        public List<PMX_Morph> Morphs = new List<PMX_Morph>();
        public List<PMX_Entry> Entries = new List<PMX_Entry>();
        public List<PMX_RigidBody> RigidBodies = new List<PMX_RigidBody>();
        public List<PMX_Joint> Joints = new List<PMX_Joint>();

        public static PMXFormat Load(BinaryReader reader)
        {
            PMXFormat pmxFormat = new PMXFormat();
            pmxFormat.Reload(reader);
            return pmxFormat;
        }

        public void Reload(BinaryReader reader)
        {
            Textures.Clear();
            Materials.Clear();
            Bones.Clear();
            Morphs.Clear();
            Entries.Clear();
            RigidBodies.Clear();
            Joints.Clear();


            int fileHeader = reader.ReadInt32();
            if (fileHeader != 0x20584D50) throw new NotImplementedException("File is not Pmx format.");//' XMP'
            float version = reader.ReadSingle();
            byte flagsSize = reader.ReadByte();//useless

            bool isUtf8Encoding = reader.ReadByte() != 0;
            byte extraUVNumber = reader.ReadByte();
            byte vertexIndexSize = reader.ReadByte();
            byte textureIndexSize = reader.ReadByte();
            byte materialIndexSize = reader.ReadByte();
            byte boneIndexSize = reader.ReadByte();
            byte morphIndexSize = reader.ReadByte();
            byte rigidBodyIndexSize = reader.ReadByte();

            Encoding encoding = isUtf8Encoding ? Encoding.UTF8 : Encoding.Unicode;

            Name = ReadString(reader, encoding);
            NameEN = ReadString(reader, encoding);
            Description = ReadString(reader, encoding);
            DescriptionEN = ReadString(reader, encoding);

            int countOfVertex = reader.ReadInt32();
            Vertices = new PMX_Vertex[countOfVertex];
            for (int i = 0; i < countOfVertex; i++)
            {
                ref PMX_Vertex vertex = ref Vertices[i];
                vertex.Coordinate = ReadVector3XInv(reader);
                vertex.Normal = ReadVector3XInv(reader);
                vertex.UvCoordinate = ReadVector2(reader);
                if (extraUVNumber > 0)
                {
                    vertex.ExtraUvCoordinate = new Vector4[extraUVNumber];
                    for (int j = 0; j < extraUVNumber; j++)
                    {
                        vertex.ExtraUvCoordinate[j] = ReadVector4(reader);
                    }
                }
                int skinningType = reader.ReadByte();
                if (skinningType == (int)PMX_BoneWeightDeformType.BDEF1)
                {
                    vertex.boneId0 = ReadIndex(reader, boneIndexSize);
                    vertex.boneId1 = -1;
                    vertex.boneId2 = -1;
                    vertex.boneId3 = -1;
                    vertex.Weights.X = 1;
                }
                else if (skinningType == (int)PMX_BoneWeightDeformType.BDEF2)
                {
                    vertex.boneId0 = ReadIndex(reader, boneIndexSize);
                    vertex.boneId1 = ReadIndex(reader, boneIndexSize);
                    vertex.boneId2 = -1;
                    vertex.boneId3 = -1;
                    vertex.Weights.X = reader.ReadSingle();
                    vertex.Weights.Y = 1.0f - vertex.Weights.X;
                }
                else if (skinningType == (int)PMX_BoneWeightDeformType.BDEF4)
                {
                    vertex.boneId0 = ReadIndex(reader, boneIndexSize);
                    vertex.boneId1 = ReadIndex(reader, boneIndexSize);
                    vertex.boneId2 = ReadIndex(reader, boneIndexSize);
                    vertex.boneId3 = ReadIndex(reader, boneIndexSize);
                    vertex.Weights = ReadVector4(reader);
                }
                else if (skinningType == (int)PMX_BoneWeightDeformType.SDEF)
                {

                    vertex.boneId0 = ReadIndex(reader, boneIndexSize);
                    vertex.boneId1 = ReadIndex(reader, boneIndexSize);
                    vertex.boneId2 = -1;
                    vertex.boneId3 = -1;
                    vertex.Weights.X = reader.ReadSingle();
                    vertex.Weights.Y = 1.0f - vertex.Weights.X;
                    ReadVector3(reader);
                    ReadVector3(reader);
                    ReadVector3(reader);
                }
                else
                {

                }
                vertex.EdgeScale = reader.ReadSingle();
            }

            int countOfTriangleIndex = reader.ReadInt32();
            TriangleIndexs = new int[countOfTriangleIndex];
            for (int i = 0; i < countOfTriangleIndex; i++)
            {
                TriangleIndexs[i] = ReadUIndex(reader, vertexIndexSize);
            }

            int countOfTexture = reader.ReadInt32();
            Textures.Capacity = countOfTexture;
            for (int i = 0; i < countOfTexture; i++)
            {
                PMX_Texture texture = new PMX_Texture();
                texture.TexturePath = ReadString(reader, encoding);
                Textures.Add(texture);
            }

            int countOfMaterial = reader.ReadInt32();
            int triangleIndexBaseShift = 0;
            Materials.Capacity = countOfMaterial;
            for (int i = 0; i < countOfMaterial; i++)
            {
                PMX_Material material = new PMX_Material();
                material.Name = ReadString(reader, encoding);
                material.NameEN = ReadString(reader, encoding);
                material.DiffuseColor = ReadVector4(reader);
                material.SpecularColor = ReadVector4(reader);
                material.AmbientColor = ReadVector3(reader);
                material.DrawFlags = (PMX_DrawFlag)reader.ReadByte();
                material.EdgeColor = ReadVector4(reader);
                material.EdgeScale = reader.ReadSingle();

                material.TextureIndex = ReadIndex(reader, textureIndexSize);
                material.SecondaryTextureIndex = ReadIndex(reader, textureIndexSize);
                material.SecondaryTextureType = reader.ReadByte();
                material.UseToon = reader.ReadByte() != 0;
                if (material.UseToon) material.ToonIndex = reader.ReadByte();
                else material.ToonIndex = ReadIndex(reader, textureIndexSize);
                material.Meta = ReadString(reader, encoding);

                material.TriangeIndexStartNum = triangleIndexBaseShift;
                material.TriangeIndexNum = reader.ReadInt32();
                triangleIndexBaseShift += material.TriangeIndexNum;

                Materials.Add(material);
            }

            int countOfBone = reader.ReadInt32();
            Bones.Capacity = countOfBone;
            for (int i = 0; i < countOfBone; i++)
            {
                PMX_Bone bone = new PMX_Bone();
                bone.Name = ReadString(reader, encoding);
                bone.NameEN = ReadString(reader, encoding);
                bone.Position = ReadVector3XInv(reader);
                bone.ParentIndex = ReadIndex(reader, boneIndexSize);
                bone.TransformLevel = reader.ReadInt32();
                bone.Flags = (PMX_BoneFlag)reader.ReadUInt16();
                if (bone.Flags.HasFlag(PMX_BoneFlag.ChildUseId))
                {
                    bone.ChildId = ReadIndex(reader, boneIndexSize);
                }
                else
                {
                    bone.ChildOffset = ReadVector3XInv(reader);
                }
                if (bone.Flags.HasFlag(PMX_BoneFlag.RotAxisFixed))
                {
                    bone.RotAxisFixed = ReadVector3(reader);
                }
                if (bone.Flags.HasFlag(PMX_BoneFlag.AcquireRotate) | bone.Flags.HasFlag(PMX_BoneFlag.AcquireTranslate))
                {
                    bone.AppendBoneIndex = ReadIndex(reader, boneIndexSize);
                    bone.AppendBoneRatio = reader.ReadSingle();
                }
                else
                {
                    bone.AppendBoneIndex = -1;
                }
                if (bone.Flags.HasFlag(PMX_BoneFlag.UseLocalAxis))
                {
                    bone.LocalAxisX = ReadVector3XInv(reader);
                    bone.LocalAxisZ = ReadVector3XInv(reader);
                    bone.LocalAxisY = Vector3.Cross(bone.LocalAxisX, bone.LocalAxisZ);
                    bone.LocalAxisZ = Vector3.Cross(bone.LocalAxisX, bone.LocalAxisY);
                    bone.LocalAxisX = Vector3.Normalize(bone.LocalAxisX);
                    bone.LocalAxisY = Vector3.Normalize(bone.LocalAxisY);
                    bone.LocalAxisZ = Vector3.Normalize(bone.LocalAxisZ);
                }
                if (bone.Flags.HasFlag(PMX_BoneFlag.ReceiveTransform))
                {
                    bone.ExportKey = reader.ReadInt32();
                }
                if (bone.Flags.HasFlag(PMX_BoneFlag.HasIK))
                {
                    PMX_BoneIK boneIK = new PMX_BoneIK();
                    boneIK.IKTargetIndex = ReadIndex(reader, boneIndexSize);
                    boneIK.CCDIterateLimit = reader.ReadInt32();
                    boneIK.CCDAngleLimit = reader.ReadSingle();
                    int countOfIKLinks = reader.ReadInt32();
                    boneIK.IKLinks = new PMX_BoneIKLink[countOfIKLinks];
                    for (int j = 0; j < countOfIKLinks; j++)
                    {
                        PMX_BoneIKLink boneIKLink = new PMX_BoneIKLink();
                        boneIKLink.LinkedIndex = ReadIndex(reader, boneIndexSize);
                        boneIKLink.HasLimit = reader.ReadByte() != 0;
                        if (boneIKLink.HasLimit)
                        {
                            boneIKLink.LimitMin = ReadVector3(reader);
                            boneIKLink.LimitMax = ReadVector3(reader);
                        }
                        boneIK.IKLinks[j] = boneIKLink;
                    }
                    bone.boneIK = boneIK;
                }
                Bones.Add(bone);
            }

            int countOfMorph = reader.ReadInt32();
            Morphs.Capacity = countOfMorph;
            for (int i = 0; i < countOfMorph; i++)
            {
                PMX_Morph morph = new PMX_Morph();
                morph.Name = ReadString(reader, encoding);
                morph.NameEN = ReadString(reader, encoding);
                morph.Category = (PMX_MorphCategory)reader.ReadByte();
                morph.Type = (PMX_MorphType)reader.ReadByte();

                int countOfMorphData = reader.ReadInt32();
                switch (morph.Type)
                {
                    case PMX_MorphType.Group:
                        morph.SubMorphs = new PMX_MorphSubMorphDesc[countOfMorphData];
                        for (int j = 0; j < countOfMorphData; j++)
                        {
                            PMX_MorphSubMorphDesc subMorph = new PMX_MorphSubMorphDesc();
                            subMorph.GroupIndex = ReadIndex(reader, morphIndexSize);
                            subMorph.Rate = reader.ReadSingle();
                            morph.SubMorphs[j] = subMorph;
                        }
                        break;
                    case PMX_MorphType.Vertex:
                        morph.MorphVertexs = new PMX_MorphVertexDesc[countOfMorphData];
                        for (int j = 0; j < countOfMorphData; j++)
                        {
                            PMX_MorphVertexDesc vertexStruct = new PMX_MorphVertexDesc();
                            vertexStruct.VertexIndex = ReadUIndex(reader, vertexIndexSize);
                            vertexStruct.Offset = ReadVector3XInv(reader);
                            morph.MorphVertexs[j] = vertexStruct;
                        }
                        Array.Sort(morph.MorphVertexs, _morphVertexCmp);//optimize for cpu L1 cache
                        break;
                    case PMX_MorphType.Bone:
                        morph.MorphBones = new PMX_MorphBoneDesc[countOfMorphData];
                        for (int j = 0; j < countOfMorphData; j++)
                        {
                            PMX_MorphBoneDesc morphBoneStruct = new PMX_MorphBoneDesc();
                            morphBoneStruct.BoneIndex = ReadIndex(reader, boneIndexSize);
                            morphBoneStruct.Translation = ReadVector3XInv(reader);
                            morphBoneStruct.Rotation = ReadQuaternionYZInv(reader);
                            morph.MorphBones[j] = morphBoneStruct;
                        }
                        break;
                    case PMX_MorphType.UV:
                    case PMX_MorphType.ExtUV1:
                    case PMX_MorphType.ExtUV2:
                    case PMX_MorphType.ExtUV3:
                    case PMX_MorphType.ExtUV4:
                        morph.MorphUVs = new PMX_MorphUVDesc[countOfMorphData];
                        for (int j = 0; j < countOfMorphData; j++)
                        {
                            PMX_MorphUVDesc morphUVStruct = new PMX_MorphUVDesc();
                            morphUVStruct.VertexIndex = ReadUIndex(reader, vertexIndexSize);
                            morphUVStruct.Offset = ReadVector4(reader);
                            morph.MorphUVs[j] = morphUVStruct;
                        }
                        break;
                    case PMX_MorphType.Material:
                        morph.MorphMaterials = new PMX_MorphMaterialDesc[countOfMaterial];
                        for (int j = 0; j < countOfMorphData; j++)
                        {
                            PMX_MorphMaterialDesc morphMaterial = new PMX_MorphMaterialDesc();
                            morphMaterial.MaterialIndex = ReadIndex(reader, materialIndexSize);
                            morphMaterial.MorphMethon = (PMX_MorphMaterialMethon)reader.ReadByte();
                            morphMaterial.Diffuse = ReadVector4(reader);
                            morphMaterial.Specular = ReadVector4(reader);
                            morphMaterial.Ambient = ReadVector3(reader);
                            morphMaterial.EdgeColor = ReadVector4(reader);
                            morphMaterial.EdgeSize = reader.ReadSingle();
                            morphMaterial.Texture = ReadVector4(reader);
                            morphMaterial.SubTexture = ReadVector4(reader);
                            morphMaterial.ToonTexture = ReadVector4(reader);
                            morph.MorphMaterials[j] = morphMaterial;
                        }
                        break;
                    default:
                        throw new NotImplementedException("Read morph fault.");
                }
                Morphs.Add(morph);
            }

            int countOfEntry = reader.ReadInt32();
            Entries.Capacity = countOfEntry;
            for (int i = 0; i < countOfEntry; i++)
            {
                PMX_Entry entry = new PMX_Entry();
                entry.Name = ReadString(reader, encoding);
                entry.NameEN = ReadString(reader, encoding);
                reader.ReadByte();//Unknow
                int countOfElement = reader.ReadInt32();
                entry.elements = new PMX_EntryElement[countOfElement];
                for (int j = 0; j < countOfElement; j++)
                {
                    PMX_EntryElement element = new PMX_EntryElement();
                    element.Type = reader.ReadByte();
                    if (element.Type == 1)
                    {
                        element.Index = ReadIndex(reader, morphIndexSize);
                    }
                    else
                    {
                        element.Index = ReadIndex(reader, boneIndexSize);
                    }
                    entry.elements[j] = element;
                }
                Entries.Add(entry);
            }

            int countOfRigidBody = reader.ReadInt32();
            RigidBodies.Capacity = countOfRigidBody;
            for (int i = 0; i < countOfRigidBody; i++)
            {
                PMX_RigidBody rigidBody = new PMX_RigidBody();
                rigidBody.Name = ReadString(reader, encoding);
                rigidBody.NameEN = ReadString(reader, encoding);
                rigidBody.AssociatedBoneIndex = ReadIndex(reader, boneIndexSize);
                rigidBody.CollisionGroup = reader.ReadByte();
                rigidBody.CollisionMask = reader.ReadUInt16();
                rigidBody.Shape = (PMX_RigidBodyShape)reader.ReadByte();
                rigidBody.Dimemsions = ReadVector3(reader);
                rigidBody.Position = ReadVector3XInv(reader);
                rigidBody.Rotation = ReadVector3YZInv(reader);
                rigidBody.Mass = reader.ReadSingle();
                rigidBody.TranslateDamp = reader.ReadSingle();
                rigidBody.RotateDamp = reader.ReadSingle();
                rigidBody.Restitution = reader.ReadSingle();
                rigidBody.Friction = reader.ReadSingle();
                rigidBody.Type = (PMX_RigidBodyType)reader.ReadByte();

                RigidBodies.Add(rigidBody);
            }

            int countOfJoint = reader.ReadInt32();
            Joints.Capacity = countOfJoint;
            for (int i = 0; i < countOfJoint; i++)
            {
                PMX_Joint joint = new PMX_Joint();
                joint.Name = ReadString(reader, encoding);
                joint.NameEN = ReadString(reader, encoding);
                joint.Type = reader.ReadByte();

                joint.AssociatedRigidBodyIndex1 = ReadIndex(reader, rigidBodyIndexSize);
                joint.AssociatedRigidBodyIndex2 = ReadIndex(reader, rigidBodyIndexSize);
                joint.Position = ReadVector3XInv(reader);
                joint.Rotation = ReadVector3YZInv(reader);
                joint.PositionMinimum = ReadVector3(reader);
                joint.PositionMaximum = ReadVector3(reader);
                joint.RotationMinimum = ReadVector3(reader);
                joint.RotationMaximum = ReadVector3(reader);
                joint.PositionSpring = ReadVector3(reader);
                joint.RotationSpring = ReadVector3(reader);

                Joints.Add(joint);
            }
        }

        private int _morphVertexCmp(PMX_MorphVertexDesc x, PMX_MorphVertexDesc y)
        {
            return x.VertexIndex.CompareTo(y.VertexIndex);
        }

        private int ReadIndex(BinaryReader reader, int size)
        {
            if (size == 1) return reader.ReadSByte();
            if (size == 2) return reader.ReadInt16();
            return reader.ReadInt32();
        }
        private int ReadUIndex(BinaryReader reader, int size)
        {
            if (size == 1) return reader.ReadByte();
            if (size == 2) return reader.ReadUInt16();
            return reader.ReadInt32();
        }

        private Vector2 ReadVector2(BinaryReader reader)
        {
            Vector2 vector2 = new Vector2();
            vector2.X = reader.ReadSingle();
            vector2.Y = reader.ReadSingle();
            return vector2;
        }
        private Vector3 ReadVector3(BinaryReader reader)
        {
            Vector3 vector3 = new Vector3();
            vector3.X = reader.ReadSingle();
            vector3.Y = reader.ReadSingle();
            vector3.Z = reader.ReadSingle();
            return vector3;
        }
        private Vector3 ReadVector3XInv(BinaryReader reader)
        {
            Vector3 vector3 = new Vector3();
            vector3.X = -reader.ReadSingle();
            vector3.Y = reader.ReadSingle();
            vector3.Z = reader.ReadSingle();
            return vector3;
        }
        private Vector3 ReadVector3YZInv(BinaryReader reader)
        {
            Vector3 vector3 = new Vector3();
            vector3.X = reader.ReadSingle();
            vector3.Y = -reader.ReadSingle();
            vector3.Z = -reader.ReadSingle();
            return vector3;
        }
        private Vector4 ReadVector4(BinaryReader reader)
        {
            Vector4 vector4 = new Vector4();
            vector4.X = reader.ReadSingle();
            vector4.Y = reader.ReadSingle();
            vector4.Z = reader.ReadSingle();
            vector4.W = reader.ReadSingle();
            return vector4;
        }
        private Quaternion ReadQuaternionYZInv(BinaryReader reader)
        {
            Quaternion quaternion = new Quaternion();
            quaternion.X = reader.ReadSingle();
            quaternion.Y = -reader.ReadSingle();
            quaternion.Z = -reader.ReadSingle();
            quaternion.W = reader.ReadSingle();
            return quaternion;
        }
        private string ReadString(BinaryReader reader, Encoding encoding)
        {
            int size = reader.ReadInt32();
            return encoding.GetString(reader.ReadBytes(size));
        }
        private void WriteVector3(BinaryWriter writer, Vector3 vector3)
        {
            writer.Write(vector3.X);
            writer.Write(vector3.Y);
            writer.Write(vector3.Z);
        }
        private void WriteQuaternion(BinaryWriter writer, Quaternion quaternion)
        {
            writer.Write(quaternion.X);
            writer.Write(quaternion.Y);
            writer.Write(quaternion.Z);
            writer.Write(quaternion.W);
        }
    }
}
