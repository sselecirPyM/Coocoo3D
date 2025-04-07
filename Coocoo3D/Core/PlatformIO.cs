using ImGuiNET;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;

namespace Coocoo3D.Core;

public class PlatformIO
{
    #region mouse inputs
    public bool[] mouseDown = new bool[5];
    public Vector2 mousePosition;
    public int mouseWheelH;
    public int mouseWheelV;
    public ConcurrentQueue<Vector2> _mouseMoveDelta = new();
    public List<Vector2> mouseMoveDelta = new();
    #endregion

    public bool[] keydown = new bool[256];
    public bool KeyControl;
    public bool KeyShift;
    public bool KeyAlt;
    public bool KeySuper;
    public ConcurrentQueue<uint> _inputChars = new();
    public List<uint> inputChars = new();
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

    #region SystemInput
    public string dropFile;

    #endregion

    public void Update()
    {
        var io = ImGui.GetIO();
        unsafe
        {
            if ((IntPtr)io.NativePtr == new IntPtr(8)) return;
        }
        inputChars.Clear();
        while (_inputChars.TryDequeue(out uint c))
        {
            inputChars.Add(c);
        }
        mouseMoveDelta.Clear();
        while (_mouseMoveDelta.TryDequeue(out var vec2))
        {
            mouseMoveDelta.Add(vec2);
        }

        if (previousFocus != Focus)
        {

        }
        previousFocus = Focus;
    }

    public void InputChar(char c)
    {
        _inputChars.Enqueue(c);
    }

    public void MousePosition(Vector2 position)
    {
        mousePosition = position;
    }

    public void MouseMoveDelta(Vector2 position)
    {
        _mouseMoveDelta.Enqueue(position);
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
