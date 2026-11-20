using System.Runtime.InteropServices;
using System.Text;

namespace WindowCover;

/// <summary>
/// Win32 互操作：枚举窗口、取窗口标题与矩形。
/// 注意：GetWindowRect 返回的是屏幕坐标（含标题栏与边框），
/// 本程序直接用该矩形换算，不做额外 DPI 缩放假设（见 README）。
/// </summary>
internal static class NativeMethods
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    /// <summary>
    /// 查找第一个可见的、标题包含 <paramref name="titlePart"/>（不区分大小写）的窗口。
    /// <paramref name="exclude"/> 中的窗口句柄会被跳过（用于排除自己的主窗/遮挡窗）。
    /// 找不到返回 IntPtr.Zero。
    /// </summary>
    public static IntPtr FindWindowByTitleContains(string titlePart, IReadOnlySet<IntPtr>? exclude = null)
    {
        if (string.IsNullOrWhiteSpace(titlePart))
        {
            return IntPtr.Zero;
        }

        IntPtr found = IntPtr.Zero;
        EnumWindows((hWnd, _) =>
        {
            if (found != IntPtr.Zero)
            {
                return false; // 已找到，停止枚举
            }
            if (exclude is not null && exclude.Contains(hWnd))
            {
                return true;
            }
            if (!IsWindowVisible(hWnd))
            {
                return true;
            }

            int len = GetWindowTextLength(hWnd);
            if (len == 0)
            {
                return true;
            }
            var sb = new StringBuilder(len + 1);
            GetWindowText(hWnd, sb, sb.Capacity);
            string title = sb.ToString();
            if (title.Contains(titlePart, StringComparison.OrdinalIgnoreCase))
            {
                found = hWnd;
                return false;
            }
            return true;
        }, IntPtr.Zero);

        return found;
    }

    public static bool TryGetWindowRect(IntPtr hWnd, out RECT rect)
    {
        return GetWindowRect(hWnd, out rect);
    }

    public static bool IsMinimized(IntPtr hWnd) => IsIconic(hWnd);
}
