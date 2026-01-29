#if UNITY_EDITOR && UNITY_STANDALONE_WIN

using System;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;

public class FullscreenHotkeyHandler : MonoBehaviour
{
    [SerializeField] private bool makeFullscreenAtStart = true;
    [SerializeField] private KeyCode toggleKey = KeyCode.Backslash;

    void Start()
    {
        if (!Application.isPlaying)
            return;

        if (makeFullscreenAtStart)
            FullscreenGameView.EnterFullscreen();
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            FullscreenGameView.Toggle();
        }
    }
}

public static class FullscreenGameView
{
    // ---------- Unity ----------
    static readonly Type GameViewType =
        Type.GetType("UnityEditor.GameView,UnityEditor");

    static readonly PropertyInfo ShowToolbarProperty =
        GameViewType?.GetProperty("showToolbar",
            BindingFlags.Instance | BindingFlags.NonPublic);

    static EditorWindow instance;
    static bool isFullscreen;

    // ---------- Win32 ----------
    const int GWL_STYLE = -16;
    const int WS_POPUP = unchecked((int)0x80000000);

    const int SWP_FRAMECHANGED = 0x0020;
    const int SWP_NOZORDER = 0x0004;
    const int SWP_SHOWWINDOW = 0x0040;

    const int SM_CXSCREEN = 0;
    const int SM_CYSCREEN = 1;

    [DllImport("user32.dll")] static extern IntPtr GetActiveWindow();
    [DllImport("user32.dll")] static extern int GetSystemMetrics(int nIndex);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    [DllImport("user32.dll")]
    static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, int uFlags);

    // ---------- Lifecycle ----------
    static FullscreenGameView()
    {
        AssemblyReloadEvents.beforeAssemblyReload += ExitInternal;
        EditorApplication.focusChanged += OnEditorFocusChanged;
        EditorApplication.update += OnEditorUpdate;
    }

    static void OnEditorFocusChanged(bool hasFocus)
    {
        if (hasFocus && isFullscreen)
            EditorApplication.delayCall += ForceWin32Fullscreen;
    }

    static void OnEditorUpdate()
    {
        if (isFullscreen && instance != null && !instance.hasFocus)
        {
            // Scene view or layout stole focus — reapply
            EditorApplication.delayCall += ForceWin32Fullscreen;
        }
    }

    static void ExitInternal()
    {
        if (instance != null)
        {
            instance.Close();
            instance = null;
        }

        isFullscreen = false;
    }

    // ---------- Public API ----------
    [MenuItem("Window/General/Game (True Fullscreen) %#&2", priority = 2)]
    public static void Toggle()
    {
        if (isFullscreen)
            ExitFullscreen();
        else
            EnterFullscreen();
    }

    public static void EnterFullscreen()
    {
        if (GameViewType == null || isFullscreen)
            return;

        instance = (EditorWindow)ScriptableObject.CreateInstance(GameViewType);
        ShowToolbarProperty?.SetValue(instance, false);

        instance.ShowPopup();
        instance.Focus();

        isFullscreen = true;
        EditorApplication.delayCall += ForceWin32Fullscreen;
    }

    public static void ExitFullscreen()
    {
        ExitInternal();
    }

    // ---------- Win32 fullscreen enforcement ----------
    static void ForceWin32Fullscreen()
    {
        if (!isFullscreen)
            return;

        IntPtr hwnd = GetActiveWindow();
        if (hwnd == IntPtr.Zero)
            return;

        int width = GetSystemMetrics(SM_CXSCREEN);
        int height = GetSystemMetrics(SM_CYSCREEN);

        SetWindowLong(hwnd, GWL_STYLE, WS_POPUP);

        SetWindowPos(
            hwnd,
            IntPtr.Zero,
            0, 0,
            width, height,
            SWP_FRAMECHANGED | SWP_NOZORDER | SWP_SHOWWINDOW
        );
    }
}

#endif
