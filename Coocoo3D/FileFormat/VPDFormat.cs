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
    public class VPDFormat
    {
        public string Name;
        public Dictionary<string, BoneKeyFrame> bonePose = new();
        public static VPDFormat Load(TextReader reader)
        {
            VPDFormat vmd = new VPDFormat();
            vmd.Reload(reader);
            return vmd;
        }

        public void Reload(TextReader reader)
        {
            bonePose.Clear();
            ReadNotCommentLine(reader);
            Name = ReadNotCommentLine(reader);
            ReadNotCommentLine(reader);
            while (true)
            {
                ReadLine(reader);
                string l = ReadLine(reader);
                if (string.IsNullOrEmpty(l))
                {
                    return;
                }
                if (!l.Contains('{')) return;
                string t = ReadLine(reader);
                string r = ReadLine(reader);
                string eu = ReadLine(reader);
                if (!eu.Contains('}')) return;
                string boneName = l.Substring(l.IndexOf('{'));
                BoneKeyFrame boneKeyFrame = new BoneKeyFrame();
                boneKeyFrame.Translation = ReadVector3(t);
                boneKeyFrame.Rotation = ReadQuaternion(t);
                bonePose[boneName] = boneKeyFrame;
            }
        }

        private string ReadLine(TextReader reader)
        {
            while (true)
            {
                string x = reader.ReadLine();
                int i = 0;
                while (char.IsSeparator(x[i]) && i < x.Length)
                {
                    i++;
                }
                x = x.Substring(i);
                if (!string.IsNullOrEmpty(x))
                {
                    return x;
                }
                if (reader.Peek() == -1)
                {
                    return "";
                }
            }
        }

        private string ReadNotCommentLine(TextReader reader)
        {
            while (true)
            {
                string x = reader.ReadLine();
                int index = x.IndexOf("//");
                if (index != -1)
                    x = x.Remove(index);
                int i = 0;
                while (char.IsSeparator(x[i]) && i < x.Length)
                {
                    i++;
                }
                x = x.Substring(i);
                if (!string.IsNullOrEmpty(x))
                {
                    return x;
                }
                if (reader.Peek() == -1)
                {
                    return "";
                }
            }
        }

        private Vector3 ReadVector3(string input)
        {
            float x = 0.0f;
            float y = 0.0f;
            float z = 0.0f;
            StringBuilder stringBuilder = new StringBuilder(64);
            int status = 0;
            int index = 0;
            for (int i = 0; i < input.Length; i++)
            {
                if (char.IsNumber(input[i]) || input[i] == '.')
                {
                    stringBuilder.Append(input[i]);
                    status = 1;
                }
                else if (status == 1)
                {
                    if (index == 0)
                        x = float.Parse(stringBuilder.ToString());
                    if (index == 1)
                        y = float.Parse(stringBuilder.ToString());
                    if (index == 2)
                        z = float.Parse(stringBuilder.ToString());
                    stringBuilder.Clear();
                    index++;
                    status = 0;
                }
                else if (index > 2)
                    break;
            }
            return new Vector3(x, y, z);
        }
        private Quaternion ReadQuaternion(string input)
        {
            float x = 0.0f;
            float y = 0.0f;
            float z = 0.0f;
            float w = 0.0f;
            StringBuilder stringBuilder = new StringBuilder(64);
            int status = 0;
            int index = 0;
            for (int i = 0; i < input.Length; i++)
            {
                if (char.IsNumber(input[i]) || input[i] == '.')
                {
                    stringBuilder.Append(input[i]);
                    status = 1;
                }
                else if (status == 1)
                {
                    if (index == 0)
                        x = float.Parse(stringBuilder.ToString());
                    if (index == 1)
                        y = float.Parse(stringBuilder.ToString());
                    if (index == 2)
                        z = float.Parse(stringBuilder.ToString());
                    if (index == 3)
                        w = float.Parse(stringBuilder.ToString());
                    stringBuilder.Clear();
                    index++;
                    status = 0;
                }
                else if (index > 3)
                    break;
            }
            return new Quaternion(x, y, z, w);
        }
    }
}
