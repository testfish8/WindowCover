# WindowCover — 跟随遮挡

用一个置顶无边框窗口，盖住其它软件界面的固定区域。目标窗口拖动、移动时，遮挡块每 100ms 跟随一次，始终盖住目标窗口内的同一个位置。

## 功能

- 主配置窗：目标窗口标题（包含匹配）、X/Y、宽/高、颜色、模式、穿透点击开关、开始/停止、状态显示。
- 跟随模式：遮挡位置 = 目标窗口左上角 + 相对偏移（X/Y）。
- 固定模式：X/Y 为屏幕绝对坐标，与目标窗口无关，始终盖在屏幕固定位置。
- 颜色：黑 / 白 / 红 / 黄 / 绿 / 蓝 / 自定义（颜色选择器）。
- 穿透点击：开启后鼠标点击穿过遮挡块，落到下层窗口；关闭时遮挡块挡住点击。
- 遮挡窗：无边框、置顶（TopMost）、不显示在任务栏、不抢焦点（`WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW`）。
- 目标窗口找不到或最小化时，遮挡块自动隐藏；找到后自动恢复。
- 配置自动保存到 exe 同目录的 `cover-settings.json`，下次启动自动加载。
- 开始后主窗自动最小化到托盘；托盘右键可显示主窗 / 退出；运行中点主窗右上角关闭是藏到托盘，不是退出。

## 运行要求

- Windows 10 / 11（64 位）
- .NET 8 Desktop Runtime（只运行 exe 时需要；从源码编译需要 .NET 8 SDK）
- 本程序在 Linux 沙箱里用 .NET 8 SDK 交叉编译验证通过（`EnableWindowsTargeting=true`），但**只能在 Windows 上运行**，Linux 上无法运行 WinForms。

## 怎么用

1. 在 Windows 上运行 `WindowCover.exe`（Release 编译产物在 `bin/Release/net8.0-windows/`）。
2. 目标窗口标题：填要遮挡的软件窗口标题的一部分，如 `微信`、`Chrome`、`Notepad`。标题包含匹配，不区分大小写。
3. X/Y、宽/高：跟随模式下 X/Y 是相对目标窗口左上角的偏移；固定模式下 X/Y 是屏幕绝对坐标。
4. 选颜色、选模式，按需打开穿透点击。
5. 点「开始遮挡」，主窗藏到托盘，遮挡块出现。点「停止」或托盘右键「退出」结束。

## 怎么测坐标

- 先填一个大概值（如 X=200, Y=50, 宽=300, 高=80），开始遮挡，看黑块落在哪，再回主窗微调 X/Y/宽/高，调 2–3 次就准了。
- 精确量法：用 AutoHotkey 自带的 Window Spy（或任何取窗口坐标的工具）看目标窗口左上角的屏幕坐标，再量出要盖的区域相对它的偏移。
- 注意：`GetWindowRect` 返回的是包含标题栏和边框的整窗矩形，所以 Y 偏移要把标题栏高度算进去（一般 30 像素左右）；如果要盖的是窗口内部内容区，Y 要再往下加标题栏高度。

## 管理员权限

- 如果目标软件是以管理员身份运行的，WindowCover 也必须以管理员身份运行（右键 exe → 以管理员身份运行），否则置顶遮挡可能盖不住，或取不到窗口位置。
- 普通软件之间不需要管理员权限。

## DPI / 多显示器

- 本程序直接用 `GetWindowRect` 返回的屏幕坐标换算，不做额外 DPI 缩放假设。
- 在单显示器 100% 缩放下最准。多显示器、不同显示器缩放比例不同（如主屏 100%、副屏 150%）时，Windows 的 DPI 虚拟化可能导致矩形与实际显示有偏差，表现为遮挡块错位几个到几十像素，调一下 X/Y 偏移即可对齐。
- 目标窗口跨显示器拖动时，遮挡块跟着窗口矩形走，不需要额外处理。

## 限制

- 全屏游戏 / DirectX 独占全屏盖不住：这类程序直接接管屏幕，普通置顶窗口（包括本程序）会被盖在下面。这是 Windows 顶层窗口机制的限制，不是 bug。
- 目标窗口标题经常变（如浏览器标签页标题）时，用标题中不变的那部分匹配；如果标题完全匹配不上，遮挡块会隐藏并提示未找到。
- 遮挡块是实色色块，不是马赛克/模糊；要半透明效果可自行改 `CoverForm` 加 `WS_EX_LAYERED` + `SetLayeredWindowAttributes`。

## 构建

```powershell
dotnet build -c Release
```

产物：`bin/Release/net8.0-windows/WindowCover.exe`，与 `cover-settings.json`（首次运行时自动生成）放在同一目录即可使用。

## 手动发布到 Releases

工作流在 `.github/workflows/release.yml`，只手动触发，不自动跑。

1. 打开仓库的 Actions > Release > Run workflow。
2. 填 tag，如 `v1.0.0`，要不要预发布自己选，点 Run workflow。
3. 等跑完，去 Releases 下载 `WindowCover-win-x64.zip`，解压就有 `WindowCover.exe`，需要 .NET 8 Desktop Runtime 才能运行。

每次发布换一个新 tag，如 `v1.0.1`，不要重复用同一个 tag。

## 文件清单

| 文件 | 说明 |
| --- | --- |
| `WindowCover.csproj` | 项目文件，`net8.0-windows` + `UseWindowsForms` |
| `Program.cs` | 入口，`[STAThread]` 启动 `MainForm` |
| `MainForm.cs` | 主配置窗 + 托盘 + 每 100ms 跟随逻辑 |
| `CoverForm.cs` | 遮挡窗（无边框/置顶/不抢焦点/穿透点击） |
| `NativeMethods.cs` | P/Invoke：`EnumWindows` / `GetWindowText` / `GetWindowRect` / `IsIconic` |
| `CoverSettings.cs` | 配置模型，`cover-settings.json` 读写 |
| `README.md` | 本文件 |
| `.gitignore` | 忽略 `bin/`、`obj/` 等构建产物 |
