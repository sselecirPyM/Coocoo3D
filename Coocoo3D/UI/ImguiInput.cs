using ImGuiNET;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Coocoo3D.UI
{
    public class ImguiInput
    {
        #region mouse inputs
        public bool[] mouseDown = new bool[5];
        public Vector2 mousePos;
        public int mouseWheelH;
        public int mouseWheelV;
        public ConcurrentQueue<Vector2> mouseMoveDelta = new();
        #endregion

        public bool[] keydown = new bool[256];
        public bool KeyControl;
        public bool KeyShift;
        public bool KeyAlt;
        public bool KeySuper;
        public ConcurrentQueue<uint> inputChars = new();
        #region outputs
        public bool WantCaptureMouse;
        public bool WantCaptureKeyboard;
        public bool WantSetMousePos;
        public bool WantTextInput;

        public Vector2 setMousePos;

        public ImGuiMouseCursor requestCursor;
        #endregion
        bool previousFocus;
        public bool Focus;

        public void Update()
        {
            var io = ImGui.GetIO();
            unsafe
            {
                if ((IntPtr)io.NativePtr == new IntPtr(8)) return;
            }
            for (int i = 0; i < 256; i++)
            {
                io.KeysDown[i] = keydown[i];
            }
            while (inputChars.TryDequeue(out uint char1))
                io.AddInputCharacter(char1);

            io.MouseWheel += Interlocked.Exchange(ref mouseWheelV, 0);
            io.MouseWheelH += Interlocked.Exchange(ref mouseWheelH, 0);

            io.KeyCtrl = KeyControl;
            io.KeyShift = KeyShift;
            io.KeyAlt = KeyAlt;
            io.KeySuper = KeySuper;
            if (previousFocus != Focus)
            {
                io.AddFocusEvent(Focus);
            }
            previousFocus = Focus;

            #region outputs
            WantCaptureKeyboard = io.WantCaptureKeyboard;
            WantCaptureMouse = io.WantCaptureMouse;
            WantSetMousePos = io.WantSetMousePos;
            WantTextInput = io.WantTextInput;

            setMousePos = io.MousePos;
            requestCursor = ImGui.GetMouseCursor();
            #endregion

            #region mouse inputs
            for (int i = 0; i < 5; i++)
                io.MouseDown[i] = mouseDown[i];
            io.MousePos = mousePos;
            #endregion
        }

        public void InputChar(char c)
        {
            inputChars.Enqueue(c);
        }

        public void MousePosition(Vector2 position)
        {
            mousePos = position;
        }

        public void MouseMoveDelta(Vector2 position)
        {
            mouseMoveDelta.Enqueue(position);
        }

        public void KeyDown(int key)
        {
            keydown[key] = true;
        }
        public void KeyUp(int key)
        {
            keydown[key] = false;
        }
    }
}
