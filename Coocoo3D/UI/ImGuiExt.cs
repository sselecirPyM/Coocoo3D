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
    public struct UIViewport
    {
        public Vector2 leftTop;
        public Vector2 rightBottom;
        public Matrix4x4 mvp;

        public Vector2 size { get => rightBottom - leftTop; }
    }
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

        public static void Draw3DLine(ImDrawListPtr drawList, Vector3 from, Vector3 to, UIViewport viewport, uint color, float thickness = 1.0f)
        {
            (Vector2 p1, Vector2 p2) = ScreenClipLine(from, to, viewport.mvp);
            if (p1 != p2)
            {
                Vector2 imageSize = viewport.size;
                drawList.AddLine(viewport.leftTop + p1 * imageSize, viewport.leftTop + p2 * imageSize, color, thickness);
            }
        }

        public static bool HitTest(Vector3 from, Vector3 to, Vector2 mouse, UIViewport viewport, float thickness)
        {
            (Vector2 p0, Vector2 p1) = ScreenClipLine(from, to, viewport.mvp);
            if (p0 == p1)
                return false;
            Vector2 size = viewport.size;
            Vector2 z0 = p0 * size;
            Vector2 z1 = p1 * size;
            Vector2 a = mouse - viewport.leftTop;

            float d = Vector2.Distance(z1, z0);
            float d1 = Vector2.Dot(a - z0, z1 - z0) / d;

            if (d1 < 0 || d1 > d)
                return false;
            float s = Vector2.DistanceSquared(a, z0) - MathF.Pow(d1, 2);

            return s <= thickness * thickness;
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


        public static void DrawCube(ImDrawListPtr drawList, UIViewport viewport)
        {
            for (int i = 0; i < 4; i++)
            {
                float signY = ((i & 2) - 1);
                float signZ = (((i << 1) & 2) - 1);
                Draw3DLine(drawList, new Vector3(1, signY, signZ), new Vector3(-1, signY, signZ), viewport, 0xffffffff);
            }
            for (int i = 0; i < 4; i++)
            {
                float signX = ((i & 2) - 1);
                float signZ = (((i << 1) & 2) - 1);
                Draw3DLine(drawList, new Vector3(signX, 1, signZ), new Vector3(signX, -1, signZ), viewport, 0xffffffff);
            }
            for (int i = 0; i < 4; i++)
            {
                float signX = ((i & 2) - 1);
                float signY = (((i << 1) & 2) - 1);
                Draw3DLine(drawList, new Vector3(signX, signY, 1), new Vector3(signX, signY, -1), viewport, 0xffffffff);
            }
        }

        public static bool PositionController(ImDrawListPtr drawList, ref Vector3 position, bool draging, UIViewport viewport)
        {
            uint id = (uint)ImGui.GetID("XYZ");
            if (!controllers.TryGetValue(id, out var controller))
            {
                controller = new _data
                {

                };
                controllers[id] = controller;
            }
            dragType hitDragType = dragType.DragNone;
            Vector2 mousePos = ImGui.GetMousePos();
            Vector2 size = viewport.size;
            uint color;
            if (HitTest(position, position + Vector3.UnitX, mousePos, viewport, 10))
                hitDragType = dragType.DragX;
            if (HitTest(position, position + Vector3.UnitY, mousePos, viewport, 10))
                hitDragType = dragType.DragY;
            if (HitTest(position, position + Vector3.UnitZ, mousePos, viewport, 10))
                hitDragType = dragType.DragZ;

            bool dragResult = false;
            if (controller.dragType != dragType.DragNone)
            {
                dragResult = true;
            }
            if (draging)
            {
                if (controller.dragType == dragType.DragNone && !controller.dragMiss)
                {
                    controller.dragType = hitDragType;
                    if (hitDragType == dragType.DragX)
                    {
                        controller.dragAxis = Vector3.UnitX;
                        controller.dragStartPoint = position;
                        controller.dragScreenStartPoint = mousePos;
                    }
                    else if (hitDragType == dragType.DragY)
                    {
                        controller.dragAxis = Vector3.UnitY;
                        controller.dragStartPoint = position;
                        controller.dragScreenStartPoint = mousePos;
                    }
                    else if (hitDragType == dragType.DragZ)
                    {
                        controller.dragAxis = Vector3.UnitZ;
                        controller.dragStartPoint = position;
                        controller.dragScreenStartPoint = mousePos;
                    }
                    else
                    {
                        controller.dragMiss = true;
                    }
                }
                if (controller.dragType != dragType.DragNone && !controller.dragMiss)
                {
                    position = DragVector(mousePos, controller, viewport);
                    dragResult = true;
                }
            }
            else
            {
                controller.dragType = dragType.DragNone;
                controller.dragMiss = false;
            }
            bool _isDragAxis(dragType d1)
            {
                return hitDragType == d1 && controller.dragType == dragType.DragNone || controller.dragType == d1;
            }

            color = _isDragAxis(dragType.DragX) ? 0xffffffff : 0x7f7f7fff;
            Draw3DLine(drawList, position, position + Vector3.UnitX, viewport, color, 10);

            color = _isDragAxis(dragType.DragY) ? 0xffffffff : 0x7f7fff7f;
            Draw3DLine(drawList, position, position + Vector3.UnitY, viewport, color, 10);

            color = _isDragAxis(dragType.DragZ) ? 0xffffffff : 0x7fff7f7f;
            Draw3DLine(drawList, position, position + Vector3.UnitZ, viewport, color, 10);


            return dragResult;
        }

        static (Vector4, Vector4) ClipLineX(Vector3 start, Vector3 end, Matrix4x4 mvp)
        {
            Vector4 vx = Vector4.Transform(new Vector4(start, 1), mvp);
            Vector4 vy = Vector4.Transform(new Vector4(end, 1), mvp);
            Vector4 delta = vy - vx;
            delta /= delta.Z;

            if (vx.Z < 0 && vy.Z < 0)
            {
                return (Vector4.Zero, Vector4.Zero);
            }

            if (vx.Z < 0)
            {
                vx = vy + delta * (-vy.Z);
            }
            if (vy.Z < 0)
            {
                vy = vx + delta * (-vx.Z);
            }

            return (vx, vy);
        }

        static Vector3 DragVector(Vector2 mouse, _data data, UIViewport viewport)
        {
            var position = data.dragStartPoint;
            var direction = data.dragAxis;
            Matrix4x4.Invert(viewport.mvp, out var invMvp);

            (Vector4 p0, Vector4 p1) = ClipLineX(position, position + direction, viewport.mvp);
            p0 /= p0.W;
            p1 /= p1.W;
            Vector4 dragUnit = (p1 - p0);
            dragUnit /= new Vector2(dragUnit.X, dragUnit.Y).Length();

            Vector2 mouseProj = (mouse - viewport.leftTop) / viewport.size;
            mouseProj.X = mouseProj.X * 2 - 1;
            mouseProj.Y = 1 - mouseProj.Y * 2;

            Vector2 mouseProj1 = (data.dragScreenStartPoint - viewport.leftTop) / viewport.size;
            mouseProj1.X = mouseProj1.X * 2 - 1;
            mouseProj1.Y = 1 - mouseProj1.Y * 2;

            float l = Vector2.Dot(mouseProj - mouseProj1, new Vector2(dragUnit.X, dragUnit.Y));
            p0 += l * dragUnit;

            Vector4 result = Vector4.Transform(p0, invMvp);
            result /= result.W;
            return new Vector3(result.X, result.Y, result.Z);
        }

        static Dictionary<uint, _data> controllers = new Dictionary<uint, _data>();

        public class _data
        {
            public bool click;
            public dragType dragType;
            public Vector2 dragScreenStartPoint;
            public Vector3 dragStartPoint;
            public Vector3 dragAxis;
            public bool dragMiss;

        }

        public enum dragType
        {
            DragNone,
            DragX,
            DragY,
            DragZ,
        }

        static Dictionary<Type, string[]> enumNames = new();
        static Dictionary<Type, object[]> enumValues = new();
    }
}
