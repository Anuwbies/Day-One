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
            FullscreenGameView.Toggle();
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
    static readonly Type GameViewType =
        Type.GetType("UnityEditor.GameView,UnityEditor");

    static readonly PropertyInfo ShowToolbarProperty =
        GameViewType?.GetProperty("showToolbar",
            BindingFlags.Instance | BindingFlags.NonPublic);

    static EditorWindow instance;

    // ---- Win32 ----
    const int GWL_STYLE = -16;
    const int WS_POPUP = unchecked((int)0x80000000);

    const int SWP_FRAMECHANGED = 0x0020;
    const int SWP_NOZORDER = 0x0004;
    const int SWP_SHOWWINDOW = 0x0040;

    [DllImport("user32.dll")]
    static extern IntPtr GetActiveWindow();

    [DllImport("user32.dll")]
    static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int X,
        int Y,
        int cx,
        int cy,
        int uFlags
    );

    const int SM_CXSCREEN = 0;
    const int SM_CYSCREEN = 1;

    static FullscreenGameView()
    {
        AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
    }

    static void OnBeforeAssemblyReload()
    {
        if (instance != null)
        {
            instance.Close();
            instance = null;
        }
    }

    [MenuItem("Window/General/Game (True Fullscreen) %#&2", priority = 2)]
    public static void Toggle()
    {
        if (GameViewType == null)
        {
            Debug.LogError("GameView type not found.");
            return;
        }

        if (instance != null)
        {
            instance.Close();
            instance = null;
            return;
        }

        instance = (EditorWindow)ScriptableObject.CreateInstance(GameViewType);
        ShowToolbarProperty?.SetValue(instance, false);

        instance.ShowPopup();
        instance.Focus();

        EditorApplication.delayCall += ForceWin32Fullscreen;
    }

    static void ForceWin32Fullscreen()
    {
        IntPtr hwnd = GetActiveWindow();
        if (hwnd == IntPtr.Zero)
            return;

        int width = GetSystemMetrics(SM_CXSCREEN);
        int height = GetSystemMetrics(SM_CYSCREEN);

        // Force true borderless popup
        SetWindowLong(hwnd, GWL_STYLE, WS_POPUP);

        SetWindowPos(
            hwnd,
            IntPtr.Zero,
            0,
            0,
            width,
            height,
            SWP_FRAMECHANGED | SWP_NOZORDER | SWP_SHOWWINDOW
        );
    }
}

#endif