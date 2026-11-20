using System.Text.Json;

namespace WindowCover;

public enum CoverMode
{
    /// <summary>跟随目标窗口：遮挡位置 = 目标窗口左上角 + 相对偏移。</summary>
    Follow,
    /// <summary>固定屏幕位置：X/Y 为屏幕绝对坐标，与目标窗口无关。</summary>
    Fixed
}

/// <summary>
/// 配置，保存到 exe 同目录的 cover-settings.json，启动时自动加载。
/// </summary>
public sealed class CoverSettings
{
    public string TargetTitle { get; set; } = "Notepad";

    /// <summary>
    /// 跟随模式：相对目标窗口左上角的偏移；固定模式：屏幕绝对坐标。
    /// </summary>
    public int X { get; set; } = 200;

    public int Y { get; set; } = 50;

    public int Width { get; set; } = 300;

    public int Height { get; set; } = 80;

    /// <summary>遮挡颜色，存 ARGB 整数，默认黑色。</summary>
    public int ColorArgb { get; set; } = unchecked((int)0xFF000000);

    public string ColorName { get; set; } = "黑色";

    public CoverMode Mode { get; set; } = CoverMode.Follow;

    /// <summary>穿透点击：开启后鼠标点击会穿过遮挡块落到下层窗口。</summary>
    public bool ClickThrough { get; set; } = false;

    public static string FilePath =>
        Path.Combine(AppContext.BaseDirectory, "cover-settings.json");

    public static CoverSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                string json = File.ReadAllText(FilePath);
                var settings = JsonSerializer.Deserialize<CoverSettings>(json);
                if (settings is not null)
                {
                    // 钳制到合理范围，防止手改配置文件导致异常尺寸
                    settings.Width = Math.Clamp(settings.Width, 10, 8000);
                    settings.Height = Math.Clamp(settings.Height, 10, 8000);
                    settings.X = Math.Clamp(settings.X, -10000, 10000);
                    settings.Y = Math.Clamp(settings.Y, -10000, 10000);
                    return settings;
                }
            }
        }
        catch
        {
            // 配置损坏时用默认值，不让程序起不来
        }
        return new CoverSettings();
    }

    public void Save()
    {
        try
        {
            string json = JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(FilePath, json);
        }
        catch
        {
            // 保存失败不弹窗打断遮挡流程
        }
    }
}
