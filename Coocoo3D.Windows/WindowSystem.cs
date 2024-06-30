using Coocoo3D.Core;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using static SDL2.SDL;

namespace Coocoo3D.Windows;

public class WindowSystem
{
    public Coocoo3DMain coocoo3DMain;
    public bool quitRequested = false;
    Dictionary<SDL_Keycode, int> sdlKeycode2ImguiKey = SDLKey();
    Dictionary<ImGuiMouseCursor, IntPtr> cursors = CreateSDLCursor();

    UIHelper uiHelper;
    IntPtr window;
    public void Initialize()
    {
        window = SDL_CreateWindow("Coocoo3D", SDL_WINDOWPOS_UNDEFINED, SDL_WINDOWPOS_UNDEFINED, 1024, 768, SDL_WindowFlags.SDL_WINDOW_RESIZABLE);
        SDL_GetWindowSize(window, out int Width, out int Height);
        SDL_SysWMinfo info = new SDL_SysWMinfo();
        SDL_GetWindowWMInfo(window, ref info);
        IntPtr hwnd = info.info.win.window;
        coocoo3DMain.SetWindow(hwnd, Width, Height);
        uiHelper = new UIHelper();
        uiHelper.hwnd = hwnd;
        uiHelper.window = window;
        coocoo3DMain.EngineContext.FillProperties(uiHelper);
        coocoo3DMain.EngineContext.InitializeObject(uiHelper);
        coocoo3DMain.RequireRender();
    }

    public void Update()
    {
        var platformIO = coocoo3DMain.platformIO;
        quitRequested = !EventProcess(platformIO, sdlKeycode2ImguiKey);

        coocoo3DMain.RequireRender();
        var modState = SDL_GetModState();
        platformIO.KeyAlt = (int)(modState & SDL_Keymod.KMOD_ALT) != 0;
        platformIO.KeyShift = (int)(modState & SDL_Keymod.KMOD_SHIFT) != 0;
        platformIO.KeyControl = (int)(modState & SDL_Keymod.KMOD_CTRL) != 0;
        SDL_SetCursor(cursors[platformIO.requestCursor]);

        if (platformIO.WantTextInput)
            SDL_StartTextInput();
        else
            SDL_StopTextInput();
        SDL_CaptureMouse(platformIO.WantCaptureMouse ? SDL_bool.SDL_TRUE : SDL_bool.SDL_FALSE);
        try
        {
            uiHelper.OnFrame();
        }
        catch (Exception e)
        {
            SDL_ShowSimpleMessageBox(SDL_MessageBoxFlags.SDL_MESSAGEBOX_ERROR, "error", e.Message + "\n" + e.StackTrace, window);
        }
        if (uiHelper.wantQuit)
        {
            SDL_DestroyWindow(window);
            quitRequested = true;
        }
    }

    static bool EventProcess(PlatformIO platformIO, Dictionary<SDL_Keycode, int> sdlKeycode2ImguiKey)
    {
        SDL_WaitEvent(out var sdlEvent);
        bool quitRequested = false;
        do
        {
            switch (sdlEvent.type)
            {
                case SDL_EventType.SDL_QUIT:
                    quitRequested = true;
                    break;
                case SDL_EventType.SDL_WINDOWEVENT:
                    if (sdlEvent.window.windowEvent == SDL_WindowEventID.SDL_WINDOWEVENT_RESIZED)
                    {
                        platformIO.windowSize = new(sdlEvent.window.data1, sdlEvent.window.data2);
                    }
                    if (sdlEvent.window.windowEvent == SDL_WindowEventID.SDL_WINDOWEVENT_FOCUS_GAINED)
                    {
                        platformIO.Focus = true;
                    }
                    if (sdlEvent.window.windowEvent == SDL_WindowEventID.SDL_WINDOWEVENT_FOCUS_LOST)
                    {
                        platformIO.Focus = false;
                    }
                    break;
                case SDL_EventType.SDL_KEYDOWN:
                    {
                        if (sdlKeycode2ImguiKey.TryGetValue(sdlEvent.key.keysym.sym, out int imkey))
                            platformIO.keydown[imkey] = true;
                    }
                    break;
                case SDL_EventType.SDL_KEYUP:
                    {
                        if (sdlKeycode2ImguiKey.TryGetValue(sdlEvent.key.keysym.sym, out int imkey))
                            platformIO.keydown[imkey] = false;
                        break;
                    }
                case SDL_EventType.SDL_TEXTINPUT:
                    {
                        string utf8Str;
                        unsafe
                        {
                            utf8Str = Marshal.PtrToStringUTF8(new IntPtr(sdlEvent.text.text));
                        }
                        foreach (var c in utf8Str)
                            platformIO.InputChar(c);
                        break;
                    }
                case SDL_EventType.SDL_MOUSEBUTTONDOWN:
                    platformIO.mouseDown[SDLMouse2ImguiMouse(sdlEvent.button.button)] = true;
                    break;
                case SDL_EventType.SDL_MOUSEBUTTONUP:
                    platformIO.mouseDown[SDLMouse2ImguiMouse(sdlEvent.button.button)] = false;
                    break;
                case SDL_EventType.SDL_MOUSEMOTION:
                    {
                        int x = sdlEvent.motion.x;
                        int y = sdlEvent.motion.y;
                        platformIO.MousePosition(new(x, y));
                        platformIO.MouseMoveDelta(new(sdlEvent.motion.xrel, sdlEvent.motion.yrel));
                    }
                    break;
                case SDL_EventType.SDL_MOUSEWHEEL:
                    platformIO.mouseWheelH += sdlEvent.wheel.x;
                    platformIO.mouseWheelV += sdlEvent.wheel.y;
                    break;
                case SDL_EventType.SDL_APP_WILLENTERFOREGROUND:
                    break;
                case SDL_EventType.SDL_DROPFILE:
                    {
                        IntPtr file = sdlEvent.drop.file;
                        string file1 = Marshal.PtrToStringUTF8(file);
                        platformIO.dropFile = file1;
                    }
                    break;
            }
        } while (SDL_PollEvent(out sdlEvent) == 1);
        return !quitRequested;
    }

    static Dictionary<SDL_Keycode, int> SDLKey()
    {
        Dictionary<SDL_Keycode, int> sdlKeycode2ImguiKey = new Dictionary<SDL_Keycode, int>();
        for (int i = 'a'; i <= 'z'; i++)
            sdlKeycode2ImguiKey[(SDL_Keycode)i] = i - 32;
        for (int i = '0'; i <= '9'; i++)
            sdlKeycode2ImguiKey[(SDL_Keycode)i] = i;
        sdlKeycode2ImguiKey[SDL_Keycode.SDLK_BACKSPACE] = (int)ImGuiKey.Backspace;
        sdlKeycode2ImguiKey[SDL_Keycode.SDLK_DELETE] = (int)ImGuiKey.Delete;
        sdlKeycode2ImguiKey[SDL_Keycode.SDLK_RETURN] = (int)ImGuiKey.Enter;
        sdlKeycode2ImguiKey[SDL_Keycode.SDLK_RETURN2] = (int)ImGuiKey.Enter;
        sdlKeycode2ImguiKey[SDL_Keycode.SDLK_KP_ENTER] = (int)ImGuiKey.KeyPadEnter;
        sdlKeycode2ImguiKey[SDL_Keycode.SDLK_ESCAPE] = (int)ImGuiKey.Escape;
        sdlKeycode2ImguiKey[SDL_Keycode.SDLK_TAB] = (int)ImGuiKey.Tab;
        sdlKeycode2ImguiKey[SDL_Keycode.SDLK_KP_TAB] = (int)ImGuiKey.Tab;
        sdlKeycode2ImguiKey[SDL_Keycode.SDLK_PAGEDOWN] = (int)ImGuiKey.PageDown;
        sdlKeycode2ImguiKey[SDL_Keycode.SDLK_PAGEUP] = (int)ImGuiKey.PageUp;
        sdlKeycode2ImguiKey[SDL_Keycode.SDLK_AC_HOME] = (int)ImGuiKey.Home;
        sdlKeycode2ImguiKey[SDL_Keycode.SDLK_HOME] = (int)ImGuiKey.Home;
        sdlKeycode2ImguiKey[SDL_Keycode.SDLK_END] = (int)ImGuiKey.End;
        sdlKeycode2ImguiKey[SDL_Keycode.SDLK_SPACE] = (int)ImGuiKey.Space;
        sdlKeycode2ImguiKey[SDL_Keycode.SDLK_UP] = (int)ImGuiKey.UpArrow;
        sdlKeycode2ImguiKey[SDL_Keycode.SDLK_DOWN] = (int)ImGuiKey.DownArrow;
        sdlKeycode2ImguiKey[SDL_Keycode.SDLK_LEFT] = (int)ImGuiKey.LeftArrow;
        sdlKeycode2ImguiKey[SDL_Keycode.SDLK_RIGHT] = (int)ImGuiKey.RightArrow;
        sdlKeycode2ImguiKey[SDL_Keycode.SDLK_INSERT] = (int)ImGuiKey.Insert;
        return sdlKeycode2ImguiKey;
    }

    static Dictionary<ImGuiMouseCursor, IntPtr> CreateSDLCursor()
    {
        Dictionary<ImGuiMouseCursor, IntPtr> cursors = new Dictionary<ImGuiMouseCursor, IntPtr>();
        void createCursor(SDL_SystemCursor systemCursor, ImGuiMouseCursor imguiMouseCursor)
        {
            cursors[imguiMouseCursor] = SDL_CreateSystemCursor(systemCursor);
        }
        createCursor(SDL_SystemCursor.SDL_SYSTEM_CURSOR_ARROW, ImGuiMouseCursor.Arrow);
        createCursor(SDL_SystemCursor.SDL_SYSTEM_CURSOR_IBEAM, ImGuiMouseCursor.TextInput);
        //createCursor(SDL_SystemCursor.SDL_SYSTEM_CURSOR_WAIT);
        //createCursor(SDL_SystemCursor.SDL_SYSTEM_CURSOR_CROSSHAIR);
        //createCursor(SDL_SystemCursor.SDL_SYSTEM_CURSOR_WAITARROW);
        createCursor(SDL_SystemCursor.SDL_SYSTEM_CURSOR_SIZENWSE, ImGuiMouseCursor.ResizeNWSE);
        createCursor(SDL_SystemCursor.SDL_SYSTEM_CURSOR_SIZENESW, ImGuiMouseCursor.ResizeNESW);
        createCursor(SDL_SystemCursor.SDL_SYSTEM_CURSOR_SIZEWE, ImGuiMouseCursor.ResizeEW);
        createCursor(SDL_SystemCursor.SDL_SYSTEM_CURSOR_SIZENS, ImGuiMouseCursor.ResizeNS);
        createCursor(SDL_SystemCursor.SDL_SYSTEM_CURSOR_SIZEALL, ImGuiMouseCursor.ResizeAll);
        createCursor(SDL_SystemCursor.SDL_SYSTEM_CURSOR_NO, ImGuiMouseCursor.NotAllowed);
        createCursor(SDL_SystemCursor.SDL_SYSTEM_CURSOR_HAND, ImGuiMouseCursor.Hand);
        createCursor(SDL_SystemCursor.SDL_NUM_SYSTEM_CURSORS, ImGuiMouseCursor.COUNT);
        return cursors;
    }

    static int SDLMouse2ImguiMouse(uint sdlMouse)
    {
        return sdlMouse switch
        {
            SDL_BUTTON_LEFT => 0,
            SDL_BUTTON_MIDDLE => 2,
            SDL_BUTTON_RIGHT => 1,
            SDL_BUTTON_X1 => 3,
            SDL_BUTTON_X2 => 4,
            _ => -1,
        };
    }
}
