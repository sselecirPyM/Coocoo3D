using Caprice.Display;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.UI
{
    public static class ImGuiExt
    {

        public static bool ComboBox<T>(string label, ref T val) where T : struct, Enum
        {
            string valName = val.ToString();
            string[] enums = Enum.GetNames<T>();
            string[] enumsTranslation = enums;

            int sourceI = Array.FindIndex(enums, u => u == valName);
            int sourceI2 = sourceI;

            bool result = ImGui.Combo(string.Format("{1}###{0}", label, label), ref sourceI, enumsTranslation, enumsTranslation.Length);
            if (sourceI != sourceI2)
                val = Enum.Parse<T>(enums[sourceI]);

            return result;
        }

        public static bool ComboBox(string label, ref object val)
        {
            var type = val.GetType();
            string valName = val.ToString();
            var fields = type.GetFields();


            string[] enums;

            if (enumNames.TryGetValue(type, out enums))
            {

            }
            else
            {
                enums = Enum.GetNames(type);
                var vals = enums.Select(u => Enum.Parse(type, u)).ToArray();
                enumValues[type] = vals;
                enums = enums.Select(u =>
                {
                    var uiShow = type.GetField(u).GetCustomAttribute<UIShowAttribute>();
                    if (uiShow != null)
                    {
                        return uiShow.Name;
                    }
                    return u;
                }).ToArray();
                enumNames[type] = enums;
            }
            string[] enumsTranslation = enums;
            var val1 = val;
            int sourceI = Array.FindIndex(enumValues[type], u => u.ToString() == valName);
            int sourceI2 = sourceI;

            bool result = ImGui.Combo(string.Format("{1}###{0}", label, label), ref sourceI, enumsTranslation, enumsTranslation.Length);

            if (sourceI != sourceI2)
            {
                val = enumValues[type][sourceI];
            }

            return result;
        }

        public static (Vector2, Vector2) ClipLine(Vector3 start, Vector3 end, Matrix4x4 mvp)
        {
            Vector4 vx = Vector4.Transform(new Vector4(start, 1), mvp);
            Vector4 vy = Vector4.Transform(new Vector4(end, 1), mvp);
            Vector4 delta = vy - vx;
            delta /= delta.Z;

            if (vx.Z < 0 && vy.Z < 0)
            {
                return (Vector2.Zero, Vector2.Zero);
            }

            if (vx.Z < 0)
            {
                vx = vy + delta * (-vy.Z);
            }
            if (vy.Z < 0)
            {
                vy = vx + delta * (-vx.Z);
            }

            vx /= vx.W;
            vy /= vy.W;
            return (new Vector2(vx.X, vx.Y), new Vector2(vy.X, vy.Y));
        }

        public static (Vector2, Vector2) ScreenClipLine(Vector3 start, Vector3 end, Matrix4x4 mvp)
        {
            (Vector2 v1, Vector2 v2) = ClipLine(start, end, mvp);

            v1 *= 0.5f;
            v1.Y = -v1.Y;
            v1 += new Vector2(0.5f, 0.5f);

            v2 *= 0.5f;
            v2.Y = -v2.Y;
            v2 += new Vector2(0.5f, 0.5f);

            return (v1, v2);
        }

        public static Vector2 TransformToViewport(Vector3 vector, Matrix4x4 vp, out bool canView)
        {
            Vector4 xPosition = Vector4.Transform(new Vector4(vector, 1), vp);
            if (xPosition.Z < 0) canView = false;
            else canView = true;
            xPosition /= xPosition.W;
            xPosition.Y = -xPosition.Y;
            return new Vector2(xPosition.X, xPosition.Y);
        }

        public static Vector2 TransformToImage(Vector3 vector, Matrix4x4 vp, out bool canView)
        {
            return TransformToViewport(vector, vp, out canView) * 0.5f + new Vector2(0.5f, 0.5f);
        }


        public static void DrawCube(ImDrawListPtr drawList, Vector2 leftTop, Vector2 imageSize, Matrix4x4 mvp)
        {
            for (int i = 0; i < 4; i++)
            {
                float signY = ((i & 2) - 1);
                float signZ = (((i << 1) & 2) - 1);
                (Vector2 p1, Vector2 p2) = ScreenClipLine(new Vector3(1, signY, signZ), new Vector3(-1, signY, signZ), mvp);
                if (p1 != p2)
                    drawList.AddLine(leftTop + p1 * imageSize, leftTop + p2 * imageSize, 0xffffffff);
            }
            for (int i = 0; i < 4; i++)
            {
                float signX = ((i & 2) - 1);
                float signZ = (((i << 1) & 2) - 1);
                (Vector2 p1, Vector2 p2) = ScreenClipLine(new Vector3(signX, 1, signZ), new Vector3(signX, -1, signZ), mvp);
                if (p1 != p2)
                    drawList.AddLine(leftTop + p1 * imageSize, leftTop + p2 * imageSize, 0xffffffff);
            }
            for (int i = 0; i < 4; i++)
            {
                float signX = ((i & 2) - 1);
                float signY = (((i << 1) & 2) - 1);
                (Vector2 p1, Vector2 p2) = ScreenClipLine(new Vector3(signX, signY, 1), new Vector3(signX, signY, -1), mvp);
                if (p1 != p2)
                    drawList.AddLine(leftTop + p1 * imageSize, leftTop + p2 * imageSize, 0xffffffff);
            }
        }

        static Dictionary<Type, string[]> enumNames = new();
        static Dictionary<Type, object[]> enumValues = new();
    }
}
