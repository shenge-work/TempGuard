# Changelog

All notable changes to TempGuard are documented here. This file is the
**human-maintained ledger**; every build additionally produces an automatic,
more granular `RELEASE_NOTES.md` (commit-level detail, SHA-256, build metadata)
which is attached to the corresponding GitHub Release.

Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/).

---

## [1.0.0] - 2026-11-10

首个正式可安装版本。从「PowerShell 便携脚本」重写为原生 .NET Framework 4.8
WinForms 应用，并建立完整的安装包 / CI / 版本说明工程链路。

### Added / 新增

- **原生 `TempGuard.exe`**：托盘图标归属进程不再是 `powershell.exe`，
  `AssemblyTitle("TempGuard")` 使 Windows「选择哪些图标显示在任务栏上」
  列表正确显示软件名 **TempGuard**（此前显示「Windows PowerShell」）。
- **实时温度数字托盘图标**：16 / 32 / 48 三种尺寸的 PNG-based 多分辨率 ICO，
  按 DPI 自动选取，两位数清晰不糊。
- **四级颜色分级**：绿 / 黄 / 橙 / 红，按当前阈值动态计算，达到阈值变红。
- **高温预警**：气泡通知 + 系统提示音，30 秒冷却防轰炸。
- **开机自启**：写入 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\TempGuard`，
  与旧 PowerShell 版同名，升级时自动覆盖，不残留双份。
- **右键菜单**：阈值切换（70/75/80/85/90 °C）、悬浮温度、开机自启、
  打开日志目录、编辑配置文件、关于、退出。
- **悬浮温度窗**：置顶、点击穿透（`WS_EX_TRANSPARENT | WS_EX_NOACTIVATE`）、
  不抢焦点，默认关闭。
- **CSV 日志**：按天分文件写到 `%LOCALAPPDATA%\TempGuard\logs\`，
  字段含时间戳 / 最高温 / 全部热区 / 阈值 / 状态，自动清理 30 天前文件。
- **配置文件** `settings.ini`：采样间隔、阈值、悬浮窗、日志开关。
- **命令行参数**：`--interval` / `--threshold` / `--overlay` /
  `--no-overlay` / `--minimized` / `--help`。
- **单实例 Mutex** 与 **DPI per-monitor-v2 自适应**。
- **温度读取双通道**：ACPI `MSAcpi_ThermalZoneTemperature` 优先，
  失败自动回退 `Thermal Zone Information` 性能计数器。
- **Inno Setup 6 安装包**：中文向导、当前用户安装（免 UAC）、
  可勾选自启与桌面图标、开始菜单 / 卸载入口、升级前自动结束旧进程。
- **GitHub Actions 自动打包**：推送 `main` 即编译 + 打包 + 上传 Artifacts
  + 更新 `dev` 预发布；推送 `v*` 标签自动创建正式 Release。
- **自动版本迭代详细说明**：`RELEASE_NOTES.md` 含元信息表、
  Conventional Commits 分组变更、产物 SHA-256、升级指引。
- **仓库自动分类**：通过 topic 前缀命名空间标记为「工具类 / Utilities」。
- **文档**：README（含完整 Roadmap）、CHANGELOG、MIT LICENSE。

### Changed / 变更

- 项目语言/形态：PowerShell 脚本 → C# / .NET Framework 4.8 WinForms。
- 图标生成：单一 `SystemIcons` → 运行时动态绘制 + 多尺寸 ICO。
- 分发方式：手动复制 `.bat` → 标准 exe 安装包 + GitHub Releases。
- 旧 PowerShell 版保留在 `legacy/TempGuard.ps1`，作为无编译环境的便携备选。

### Fixed / 修复

- 修复 Windows 任务栏图标列表显示宿主进程名而非软件名的问题。
- 修复大字体数字在小尺寸托盘图标中被裁切、只显示一位数的问题。
- 修复每次刷新都重建图标造成的 GDI 句柄泄漏（改为仅在温度整数
  或阈值变化时才重绘，并显式释放旧 Icon）。
- 修复通知文本超过 63 字符导致 `NotifyIcon.Text` 抛异常的问题。

### Notes / 说明

- 目标框架 .NET Framework 4.8 为 Windows 10 1903+ 系统自带组件，
  最终用户无需额外安装运行时。
- 安装包暂未做代码签名，首次运行可能出现 SmartScreen 提示，
  已列入 Roadmap v2.0。

---

## 版本规则

| 推送内容 | 生成版本 | 产物位置 |
| --- | --- | --- |
| `main` 分支普通提交 | `1.0.0.<构建号>` | Actions Artifacts + `dev` 预发布 |
| `v*` 标签 | 标签号去掉 `v` | 正式 GitHub Release |

版本号基值来自根目录 `version.txt`，发版时请同步更新该文件与 `CHANGELOG.md`。
