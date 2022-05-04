using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Numerics;
using Coocoo3D.Present;

namespace Coocoo3D.FileFormat
{
    public class VMDFormat
    {
        public static VMDFormat Load(BinaryReader reader)
        {
            VMDFormat vmd = new VMDFormat();
            vmd.Reload(reader);
            return vmd;
        }

        public void Reload(BinaryReader reader)
        {
            headerChars = reader.ReadBytes(30);
            var uName = reader.ReadBytes(20);
            var jpEncoding = CodePagesEncodingProvider.Instance.GetEncoding("shift_jis");
            Name = jpEncoding.GetString(uName);
            var stream = reader.BaseStream;

            int numOfBone = reader.ReadInt32();
            for (int i = 0; i < numOfBone; i++)
            {
                uName = reader.ReadBytes(15);
                int j = 0;
                for (; j < uName.Length; j++)
                {
                    if (uName[j] == 0) break;
                }
                string nName = jpEncoding.GetString(uName, 0, j);
                if (!BoneKeyFrameSet.TryGetValue(nName, out List<BoneKeyFrame> keyFrames))
                {
                    keyFrames = new List<BoneKeyFrame>();
                    BoneKeyFrameSet.Add(nName, keyFrames);
                }
                BoneKeyFrame keyFrame = new BoneKeyFrame();
                keyFrame.Frame = reader.ReadInt32();
                keyFrame.translation = ReadVector3XInv(reader);
                keyFrame.rotation = ReadQuaternionYZInv(reader);
                keyFrame.xInterpolator = ReadBoneInterpolator(reader);
                keyFrame.yInterpolator = ReadBoneInterpolator(reader);
                keyFrame.zInterpolator = ReadBoneInterpolator(reader);
                keyFrame.rInterpolator = ReadBoneInterpolator(reader);

                keyFrames.Add(keyFrame);
            }
            if (stream.Length - stream.Position == 0)
                goto endlabel;

            int numOfMorph = reader.ReadInt32();
            for (int i = 0; i < numOfMorph; i++)
            {
                uName = reader.ReadBytes(15);
                int j = 0;
                for (; j < uName.Length; j++)
                {
                    if (uName[j] == 0) break;
                }
                string nName = jpEncoding.GetString(uName, 0, j);
                if (!MorphKeyFrameSet.TryGetValue(nName, out List<MorphKeyFrame> keyFrames))
                {
                    keyFrames = new List<MorphKeyFrame>();
                    MorphKeyFrameSet.Add(nName, keyFrames);
                }
                MorphKeyFrame keyFrame = new MorphKeyFrame();
                keyFrame.Frame = reader.ReadInt32();
                keyFrame.Weight = reader.ReadSingle();

                keyFrames.Add(keyFrame);
            }
            if (stream.Length - stream.Position == 0)
                goto endlabel;

            int numOfCam = reader.ReadInt32();
            for (int i = 0; i < numOfCam; i++)
            {
                CameraKeyFrame keyFrame = new CameraKeyFrame();
                keyFrame.Frame = reader.ReadInt32();
                keyFrame.distance = reader.ReadSingle();
                keyFrame.position = ReadVector3XInv(reader);
                keyFrame.rotation = ReadVector3YZInv(reader);
                keyFrame.mxInterpolator = ReadCameraInterpolator(reader);
                keyFrame.myInterpolator = ReadCameraInterpolator(reader);
                keyFrame.mzInterpolator = ReadCameraInterpolator(reader);
                keyFrame.rInterpolator = ReadCameraInterpolator(reader);
                keyFrame.dInterpolator = ReadCameraInterpolator(reader);
                keyFrame.fInterpolator = ReadCameraInterpolator(reader);
                keyFrame.FOV = reader.ReadInt32();
                keyFrame.orthographic = reader.ReadByte() != 0;

                CameraKeyFrames.Add(keyFrame);
            }
            if (stream.Length - stream.Position == 0)
                goto endlabel;

            int numOfLight = reader.ReadInt32();
            for (int i = 0; i < numOfLight; i++)
            {
                LightKeyFrame lightKeyFrame = new LightKeyFrame();
                lightKeyFrame.Frame = reader.ReadInt32();
                lightKeyFrame.Color = ReadVector3(reader);
                lightKeyFrame.Position = ReadVector3XInv(reader);

                LightKeyFrames.Add(lightKeyFrame);
            }
            if (stream.Length - stream.Position == 0)
                goto endlabel;

            endlabel:
            foreach (var keyframes in BoneKeyFrameSet.Values)
            {
                keyframes.Sort();
            }
            foreach (var keyframes in MorphKeyFrameSet.Values)
            {
                keyframes.Sort();
            }
            CameraKeyFrames.Sort();
            LightKeyFrames.Sort();
        }

        public void SaveToFile(BinaryWriter writer)
        {
            writer.Write(headerChars);
            var jpEncoding = Encoding.GetEncoding("shift_jis");
            byte[] sChars = jpEncoding.GetBytes(Name);
            byte[] sChars2 = new byte[20];
            Array.Copy(sChars, sChars2, Math.Min(sChars.Length, sChars2.Length));
            writer.Write(sChars2);

            int numOfBone = 0;
            foreach (var collection in BoneKeyFrameSet.Values)
            {
                numOfBone += collection.Count;
            }
            writer.Write(numOfBone);
            foreach (var NKPair in BoneKeyFrameSet)
            {
                string objName = NKPair.Key;
                sChars = jpEncoding.GetBytes(objName);
                sChars2 = new byte[15];
                Array.Copy(sChars, sChars2, Math.Min(sChars.Length, sChars2.Length));
                foreach (var keyFrame in NKPair.Value)
                {
                    writer.Write(sChars2);
                    writer.Write(keyFrame.Frame);
                    WriteVector3XInv(writer, keyFrame.translation);
                    WriteQuaternionYZInv(writer, keyFrame.rotation);
                    WriteBoneInterpolator(writer, keyFrame.xInterpolator);
                    WriteBoneInterpolator(writer, keyFrame.yInterpolator);
                    WriteBoneInterpolator(writer, keyFrame.zInterpolator);
                    WriteBoneInterpolator(writer, keyFrame.rInterpolator);
                }
            }

            int numOfMorph = 0;
            foreach (var collection in MorphKeyFrameSet.Values)
            {
                numOfMorph += collection.Count;
            }
            writer.Write(numOfMorph);
            foreach (var NMPair in MorphKeyFrameSet)
            {
                string objName = NMPair.Key;
                sChars = jpEncoding.GetBytes(objName);
                sChars2 = new byte[15];
                Array.Copy(sChars, sChars2, Math.Min(sChars.Length, sChars2.Length));
                foreach (var keyFrame in NMPair.Value)
                {
                    writer.Write(sChars2);
                    writer.Write(keyFrame.Frame);
                    writer.Write(keyFrame.Weight);
                }
            }

            int numOfCam = CameraKeyFrames.Count;
            writer.Write(numOfCam);
            foreach (var keyframe in CameraKeyFrames)
            {
                writer.Write(keyframe.Frame);
                writer.Write(keyframe.distance);
                WriteVector3XInv(writer, keyframe.position);
                WriteVector3XInv(writer, keyframe.rotation);
                WriteCameraInterpolator(writer, keyframe.mxInterpolator);
                WriteCameraInterpolator(writer, keyframe.myInterpolator);
                WriteCameraInterpolator(writer, keyframe.mzInterpolator);
                WriteCameraInterpolator(writer, keyframe.rInterpolator);
                WriteCameraInterpolator(writer, keyframe.dInterpolator);
                WriteCameraInterpolator(writer, keyframe.fInterpolator);
                writer.Write(keyframe.FOV);
                writer.Write(Convert.ToByte(keyframe.orthographic));
            }

            int numOfLight = LightKeyFrames.Count;
            writer.Write(numOfLight);
            foreach (var keyframe in LightKeyFrames)
            {
                writer.Write(keyframe.Frame);
                WriteVector3XInv(writer, keyframe.Color);
                WriteVector3XInv(writer, keyframe.Position);
            }
        }
        public byte[] headerChars;
        public string Name;
        public Dictionary<string, List<BoneKeyFrame>> BoneKeyFrameSet { get; set; } = new Dictionary<string, List<BoneKeyFrame>>();
        public Dictionary<string, List<MorphKeyFrame>> MorphKeyFrameSet { get; set; } = new Dictionary<string, List<MorphKeyFrame>>();
        public List<CameraKeyFrame> CameraKeyFrames { get; set; } = new List<CameraKeyFrame>();
        public List<LightKeyFrame> LightKeyFrames { get; set; } = new List<LightKeyFrame>();

        private Interpolator ReadBoneInterpolator(BinaryReader reader)
        {
            const float c_is = 1.0f / 127.0f;
            var x = new Interpolator();
            x.ax = (((reader.ReadInt32() & 0xFF) ^ 0x80) - 0x80) * c_is;
            x.ay = (((reader.ReadInt32() & 0xFF) ^ 0x80) - 0x80) * c_is;
            x.bx = (((reader.ReadInt32() & 0xFF) ^ 0x80) - 0x80) * c_is;
            x.by = (((reader.ReadInt32() & 0xFF) ^ 0x80) - 0x80) * c_is;
            return x;
        }
        private Interpolator ReadCameraInterpolator(BinaryReader reader)
        {
            const float c_is = 1.0f / 127.0f;
            var x = new Interpolator();
            uint a = reader.ReadUInt32();
            x.ax = (((a & 0xFF) ^ 0x80) - 0x80) * c_is;
            x.bx = ((((a & 0xFF00) >> 8) ^ 0x80) - 0x80) * c_is;
            x.ay = ((((a & 0xFF0000) >> 16) ^ 0x80) - 0x80) * c_is;
            x.by = ((((a & 0xFF000000) >> 24) ^ 0x80) - 0x80) * c_is;
            return x;
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
        private Quaternion ReadQuaternionYZInv(BinaryReader reader)
        {
            Quaternion quaternion = new Quaternion();
            quaternion.X = reader.ReadSingle();
            quaternion.Y = -reader.ReadSingle();
            quaternion.Z = -reader.ReadSingle();
            quaternion.W = reader.ReadSingle();
            return quaternion;
        }
        private void WriteBoneInterpolator(BinaryWriter writer, Interpolator interpolator)
        {
            writer.Write((((int)Math.Round(interpolator.ax * 127) + 0x80) ^ 0x80) & 0xFF);
            writer.Write((((int)Math.Round(interpolator.ay * 127) + 0x80) ^ 0x80) & 0xFF);
            writer.Write((((int)Math.Round(interpolator.bx * 127) + 0x80) ^ 0x80) & 0xFF);
            writer.Write((((int)Math.Round(interpolator.by * 127) + 0x80) ^ 0x80) & 0xFF);
        }
        private void WriteCameraInterpolator(BinaryWriter writer, Interpolator interpolator)
        {
            writer.Write((((byte)Math.Round(interpolator.ax * 127) + 0x80) ^ 0x80) & 0xFF);
            writer.Write((((byte)Math.Round(interpolator.bx * 127) + 0x80) ^ 0x80) & 0xFF);
            writer.Write((((byte)Math.Round(interpolator.ay * 127) + 0x80) ^ 0x80) & 0xFF);
            writer.Write((((byte)Math.Round(interpolator.by * 127) + 0x80) ^ 0x80) & 0xFF);
        }
        private void WriteVector3XInv(BinaryWriter writer, Vector3 vector3)
        {
            writer.Write(-vector3.X);
            writer.Write(vector3.Y);
            writer.Write(vector3.Z);
        }
        private void WriteQuaternionYZInv(BinaryWriter writer, Quaternion quaternion)
        {
            writer.Write(quaternion.X);
            writer.Write(-quaternion.Y);
            writer.Write(-quaternion.Z);
            writer.Write(quaternion.W);
        }
    }

}
