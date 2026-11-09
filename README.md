# TempGuard

> **项目分类：工具类 / Utilities** · 轻量 Windows 后台温度监控（托盘实时数字图标）
>
> [![Build installer](https://github.com/shenge-work/TempGuard/actions/workflows/build.yml/badge.svg)](https://github.com/shenge-work/TempGuard/actions/workflows/build.yml)
> [![Release](https://img.shields.io/github/v/release/shenge-work/TempGuard?include_prereleases)](https://github.com/shenge-work/TempGuard/releases)
> [![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
> [![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-blue)](#运行环境)
> [![Category](https://img.shields.io/badge/category-工具类%20%2F%20Tools-informational)](#项目分类与分组)

一个**零依赖、不联网、不上传任何数据**的 Windows 托盘小工具：把 CPU 温度直接画成任务栏图标上的数字，超过阈值变红并报警。最初为排查 Surface Laptop 3 高温反复重启而写，现在包装成标准 Windows 安装包开源分享。

---

## 项目分类与分组

GitHub 没有仓库文件夹分组，本项目用 **topic 前缀命名空间**做逻辑分组（由 CI 自动打标签，见 [`.github/workflows/build.yml`](.github/workflows/build.yml)）：

| 分组 / 标签 | 含义 |
| --- | --- |
| `category-tools`、`tools`、`utilities`、`utility` | **主分类：工具类 / Utilities** |
| `category-desktop-app`、`desktop-app`、`system-tray` | 桌面应用 / 托盘常驻类 |
| `category-monitoring`、`temperature-monitor`、`hardware-monitoring`、`thermal-management` | 硬件温度监控类 |
| `category-windows`、`windows` | Windows 平台 |
| `csharp`、`dotnet-framework`、`winforms` | 技术栈 |
| `inno-setup`、`github-actions`、`lightweight` | 构建 / 分发 / 特性 |

> 仓库设置页若显示分类未自动生效（GitHub 有时要求仓库管理员权限），在仓库右侧 **About → ⚙** 手动粘一次：
> `tools, utilities, category-tools, category-desktop-app, category-monitoring, category-windows, windows, temperature-monitor, csharp, winforms`

---

## 为什么之前显示「Windows PowerShell」（本次核心修复）

这是很多人踩过的坑，值得单独说明：

- Windows「设置 → 个性化 → 任务栏 → 选择哪些图标显示在任务栏上」列表里显示的名字，**取自创建托盘图标的那个进程的可执行文件版本资源（AssemblyTitle / FileDescription）**。
- 旧版是 `.ps1` 脚本，由 `powershell.exe` 宿主执行，所以图标归属进程是 `powershell.exe` → 列表里显示 **「Windows PowerShell」**，副标题才是我设的 `TempGuard CPU zone: 49C`。
- 本次改写为**原生 .NET WinForms 独立程序 `TempGuard.exe`**，并在 [`AssemblyInfo.cs`](src/TempGuard/Properties/AssemblyInfo.cs) 里写入 `AssemblyTitle("TempGuard")`。图标归属进程变成 `TempGuard.exe` → 列表直接显示 **「TempGuard」**。

额外收益：单实例保护、DPI 自适应、多尺寸图标、免 UAC 安装、开始菜单与卸载项，以及真正的「软件」体验。

---

## 功能

| 功能 | 说明 |
| --- | --- |
| 托盘实时数字图标 | 直接把温度画成图标；16/32/48 三种尺寸自适应 DPI，两位数不会糊 |
| 颜色分级 | 绿（安全）→ 黄 → 橙（接近阈值）→ 红（达到阈值），随阈值动态计算 |
| 高温预警 | 达到阈值弹出气泡 + 系统提示音，30 秒冷却避免轰炸 |
| 开机自启 | 安装向导可勾选；托盘右键可随时开关（写 `HKCU\...\Run\TempGuard`） |
| 悬浮温度 | 可选置顶悬浮窗，点击穿透、不抢焦点 |
| CSV 日志 | `%LOCALAPPDATA%\TempGuard\logs\temp_YYYYMMDD.csv`，自动清理 30 天前文件 |
| 右键菜单 | 阈值（70/75/80/85/90 °C）、悬浮窗、自启、日志目录、配置文件、关于、退出 |
| 温度读取 | ACPI `MSAcpi_ThermalZoneTemperature`，失败自动回退性能计数器 |
| 单实例 | Mutex 防重复启动；安装包升级前自动结束旧进程 |
| 隐私 | 纯本地、零网络请求、零遥测、零第三方依赖 |

---

## 安装

### 方式一：直接下载安装包（推荐，给别人用就发这个）

到 [Releases](https://github.com/shenge-work/TempGuard/releases) 下载 `TempGuard-Setup-x.y.z.exe`，双击安装：

- **当前用户安装**，装到 `%LOCALAPPDATA%\Programs\TempGuard`，**不需要管理员权限、不弹 UAC**
- 可勾选「开机自动启动」「创建桌面快捷方式」
- 开始菜单有快捷方式与卸载入口，控制面板「应用和功能」可正常卸载
- 装完自动在后台启动，任务栏托盘出现实时温度数字

> 首次运行若 SmartScreen 提示「未知发布者」，点 **更多信息 → 仍要运行**。原因是开源项目暂无 EV 代码签名证书，已列入 [Roadmap](#后续迭代路线图-roadmap)。

### 方式二：从源码构建

要求 Windows + Visual Studio 2022（含 **.NET Framework 4.8 目标包**）或 Build Tools：

```powershell
# 1. 生成图标
pwsh ./scripts/New-AppIcon.ps1

# 2. 编译
msbuild src/TempGuard/TempGuard.csproj /t:Rebuild /p:Configuration=Release /p:Platform=AnyCPU

# 3. 打包（需 Inno Setup 6）
ISCC.exe /DAppVersion=1.0.0 installer/TempGuard.iss
```

产物在 `artifacts/TempGuard-Setup-1.0.0.exe`。也可直接运行 `src/TempGuard/bin/Release/TempGuard.exe`（免安装便携用法）。

### 方式三：便携脚本（无编译环境）

[`legacy/TempGuard.ps1`](legacy/TempGuard.ps1) 是初代 PowerShell 版，零安装、双击即用，但托盘列表会显示「Windows PowerShell」。适合临时排查，正式使用建议装 exe 版。

---

## 让图标显示在底部任务栏主区

托盘数字图标默认可能落在 `^` 溢出区。设置一次即永久记住：

1. 右键任务栏 → **任务栏设置** → 「选择哪些图标显示在任务栏上」
2. 找到 **TempGuard** → 开关拨到 **开**

之后图标就与电量、音量同一行常驻。

---

## 配置

托盘右键「编辑配置文件」会打开 `%LOCALAPPDATA%\TempGuard\settings.ini`：

```ini
interval=3        ; 采样间隔（秒，1-300），默认 3
threshold=80      ; 高温告警阈值（°C，40-110），默认 80
overlay=0         ; 是否显示右上角悬浮温度（1/0）
log=1             ; 是否写 CSV 日志（1/0）
```

也可以用命令行参数覆盖（不写回配置文件）：

```
TempGuard.exe --interval 5 --threshold 75 --overlay --minimized
```

| 参数 | 说明 |
| --- | --- |
| `--interval N` | 采样间隔 1–300 秒 |
| `--threshold N` | 高温阈值 40–110 °C |
| `--overlay` / `--no-overlay` | 显示 / 隐藏悬浮温度 |
| `--minimized` | 静默启动，不弹气泡（安装器与自启使用） |
| `--help` | 显示参数帮助 |

---

## 上传代码 → 自动打包安装包 + 版本迭代详细说明

`.github/workflows/build.yml` 已开启，**无需任何手动操作**：

| 触发条件 | 自动做的事 |
| --- | --- |
| 推送到 `main` | 云端编译 → 生成图标 → 打包 `TempGuard-Setup-<版本>.exe` → 生成 `RELEASE_NOTES.md` → 上传 Artifacts（保留 90 天）→ 更新 `dev` 预发布 |
| 推送 `v*` 标签 | 上述全部 + 创建**正式 GitHub Release**，附上安装包与版本迭代说明 |
| Pull Request | 只编译验证，不发版 |
| 手动触发 | 可指定版本号覆盖 |

版本号规则：标签 `v1.2.3` → `1.2.3`；普通推送 → `version.txt` 基版本 + 构建号（如 `1.0.0.17`）。

### 版本迭代详细说明

每次构建由 [`scripts/New-ReleaseNotes.ps1`](scripts/New-ReleaseNotes.ps1) 自动生成 `RELEASE_NOTES.md`，内容包括：

1. **元信息表**：版本号、发布时间、代码提交、对比基线 tag、提交数、构建编号
2. **变更总览**：按 Conventional Commits 分组（新增功能 / 问题修复 / 性能与重构 / 文档 / 构建与 CI / 其它），逐条列出 `hash + 说明 + 作者 + 日期`
3. **交付内容**：安装包文件名、大小、**SHA-256 校验和**
4. **安装与升级指引**：含从旧 PowerShell 版迁移的注意事项
5. **版本状态说明**：稳定版 / 开发版 / CHANGELOG 的关系

人工维护的完整历史见 [CHANGELOG.md](CHANGELOG.md)。因此**每个版本都有两份说明**：Release 里的自动明细 + 仓库里的手工总账。

发版示例：

```bash
git tag -a v1.1.0 -m "feat: 多传感器支持"
git push origin v1.1.0
# 几分钟后 Release 页面自动出现安装包 + 详细迭代说明
```

---

## 后续迭代路线图 (Roadmap)

> 以下为已规划的持续优化方向，欢迎提 Issue / PR 认领。优先级会按实际反馈调整。

### v1.1 · 体验与个性化
- [ ] **图形化设置窗口**：告别 ini 手改，提供阈值、间隔、配色、自启的可视化面板
- [ ] **中英文界面 i18n**：菜单、气泡、安装向导全量多语言
- [ ] **自定义托盘外观**：主题色、字体、圆角、单位 °C/°F、纯数字 / 带单位 / 迷你趋势箭头
- [ ] **悬浮窗可拖拽 + 位置记忆 + 透明度**，多显示器可选屏幕
- [ ] **静默时段 / 免打扰**：会议、夜间不弹提示
- [ ] 托盘图标点击行为可配置（显示详情 / 显示曲线 / 无动作）

### v1.2 · 数据源与准确性
- [ ] **多传感器**：接入 LibreHardwareMonitor / WMI，支持 CPU 各核心、GPU、SSD、电池、风扇转速
- [ ] **温度曲线与历史回放**：内置轻量图表窗口，1h / 24h / 7d 视图
- [ ] **数据导出**：CSV / JSON 导出与按时间段筛选
- [ ] **采样去抖与异常值过滤**：ACPI 瞬时跳变、休眠唤醒后的脏数据
- [ ] **机型校准**：不同主板 ACPI 偏移补偿，提供用户可调 offset
- [ ] **温度不可读时的诊断提示**：自动检测 WMI 权限 / 服务状态并给出修复建议

### v1.3 · 智能化与系统联动
- [ ] **趋势预测预警**：基于斜率提前 N 秒预警，而不是等撞到阈值
- [ ] **高温自动处置**（可开关）：切换电源方案、临时限频、结束白名单外高负载进程
- [ ] **与系统事件联动**：关联 `Kernel-Power 41` 意外断电事件，生成散热健康诊断报告（源自本项目最初的 Surface 排查场景）
- [ ] **风扇 / 负载关联分析**：温度 + CPU 占用 + 电池状态联合定位散热瓶颈
- [ ] 通知中心 Toast 通知、自定义提示音

### v2.0 · 分发与企业化
- [ ] **自动更新**：内置 updater，检查 GitHub Release 并增量升级
- [ ] **代码签名**：接入证书 / GitHub Artifact Attestation，消除 SmartScreen 警告
- [ ] **包管理器分发**：winget、Chocolatey、Scoop
- [ ] **MSI / 静默部署**：`/SILENT`、`/DIR=`、组策略与 Intune 批量部署
- [ ] **单文件便携版**：无需安装、配置随目录走（绿色版）
- [ ] **Windows 服务模式**：无人登录时也记录温度（可选组件）

### 工程化
- [ ] 单元测试（图标生成、配置解析、温度换算）+ 覆盖率
- [ ] CI 增加病毒扫描、产物签名验证、多 .NET 目标矩阵
- [ ] 崩溃日志与本地诊断包导出（用户可一键生成脱敏报告）
- [ ] CONTRIBUTING / Issue 模板 / PR 模板 / 行为准则
- [ ] 无障碍：高对比度、屏幕阅读器标签、键盘操作

---

## 项目结构

```
TempGuard/
├── .github/workflows/build.yml     # 上传代码即自动打包 + 版本说明 + 打标签
├── src/TempGuard/
│   ├── TempGuard.csproj            # .NET Framework 4.8 WinForms
│   ├── Program.cs                  # 全部应用逻辑
│   ├── Properties/AssemblyInfo.cs  # ★ 决定任务栏显示名为 TempGuard
│   ├── app.manifest                # 免 UAC / DPI 自适应
│   └── App.config
├── installer/TempGuard.iss         # Inno Setup 安装包脚本（中文向导）
├── scripts/
│   ├── New-AppIcon.ps1             # 生成应用图标（无外部依赖）
│   └── New-ReleaseNotes.ps1        # 生成版本迭代详细说明
├── legacy/TempGuard.ps1            # 初代 PowerShell 便携版
├── version.txt                     # 版本号单一来源
├── CHANGELOG.md
├── LICENSE
└── README.md
```

---

## 运行环境

| 项 | 要求 |
| --- | --- |
| 操作系统 | Windows 10 1809+ / Windows 11（x86 / x64 / ARM64） |
| 运行时 | .NET Framework 4.8（Win10 1903+ 系统自带，无需另装） |
| 权限 | 普通用户即可；个别机型读 ACPI 温度需管理员（程序会显示 `N/A` 并提示） |
| 资源占用 | 单进程、约 15–25 MB 内存、无后台服务、无开机联网 |

## 隐私与安全

- 不联网、不上传温度数据、不含任何遥测或第三方 SDK
- 日志只写本机用户目录，可随时在右键菜单打开并删除
- 所有源码公开，可自行审计或编译

## 常见问题

**Q：托盘显示 `N/A` 或 `--`？**
A：该机型 ACPI 温度区未暴露给 WMI。右键退出后「以管理员身份运行」再试；仍不行说明主板不提供标准 ACPI 热区，可考虑 Roadmap v1.2 的 LibreHardwareMonitor 方案。

**Q：任务栏列表里还是显示 Windows PowerShell？**
A：说明运行的是旧 `.ps1` 便携版。请先右键旧图标 → Exit，再安装 exe 版；安装包会覆盖同名自启项。

**Q：温度是 49 但感觉机器很烫？**
A：ACPI 热区通常是主板/CPU 综合传感器，与核心瞬时温度有差异；且本机最初的重启问题是「电池下散热策略配反」，温度数值只是参考，详见 CHANGELOG 与 Roadmap 中的系统联动项。

## 许可

[MIT](LICENSE) © 2026 shenge-work
