namespace WindowCover;

internal static class Program
{
    /// <summary>
    /// 应用程序主入口点。
    /// </summary>
    [STAThread]
    private static void Main()
    {
        // 每显示器 DPI 感知：125%/150% 缩放下布局按实际 DPI 缩放，不再按 96 DPI 固定像素溢出。
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
