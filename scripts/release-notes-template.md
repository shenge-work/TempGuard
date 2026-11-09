# TempGuard v@@VERSION@@ 版本迭代说明

> 本文件由 CI 在每次代码推送/发版时自动生成，包含本次迭代的完整变更明细、构建产物与校验信息。

| 项目 | 值 |
| --- | --- |
| 版本号 | `@@VERSION@@` |
| 发布日期 | @@DATE@@ |
| 代码提交 | `@@HEAD@@` |
| 对比基线 | @@BASELINE@@ |
| 本次提交数 | @@COMMIT_COUNT@@ |
| 仓库 | https://github.com/@@REPO@@ |
| 构建编号 | @@RUN_ID@@ |

## 一、变更总览

本次共纳入 **@@COMMIT_COUNT@@** 次提交，按 Conventional Commits 类型分组如下：

### 新增功能 Features （@@N1@@）

@@G1@@

### 问题修复 Bug Fixes （@@N2@@）

@@G2@@

### 性能与重构 Performance （@@N3@@）

@@G3@@

### 文档 Documentation （@@N4@@）

@@G4@@

### 构建与 CI Build / CI （@@N5@@）

@@G5@@

### 其它变更 Other （@@N6@@）

@@G6@@

## 二、本次交付内容

| 文件 | 大小 (MB) | SHA-256 |
| --- | --- | --- |
@@ARTIFACTS@@

## 三、安装与升级

1. 下载上表中的 `TempGuard-Setup-@@VERSION@@.exe`，双击安装。
2. 安装包为**当前用户安装**，不需要管理员权限，不会弹 UAC。
3. 安装向导中可勾选「开机自动启动 TempGuard」与「创建桌面快捷方式」。
4. 从旧版 PowerShell 脚本升级：安装包使用同名自启项 `HKCU\...\Run\TempGuard`，会自动覆盖旧条目；安装前请先在旧托盘图标上点「Exit」退出。
5. 安装完成后，任务栏右下角托盘图标即显示实时温度数字（绿 → 橙 → 红表示接近阈值）。

> 提示：若要把图标从 `^` 溢出区移到主任务栏，请在「设置 → 个性化 → 任务栏 → 选择哪些图标显示在任务栏上」中把 **TempGuard** 开关打开。

## 四、版本状态

- 稳定版：以 `v*` 标签触发，自动生成 GitHub Release 并附带本说明。
- 开发版：推送到 `main` 即自动打包，产物见 Actions 的 Artifacts 与 `dev` 预发布。
- 完整人工维护的变更历史见仓库 [CHANGELOG.md](https://github.com/@@REPO@@/blob/main/CHANGELOG.md)。

---

*由 GitHub Actions 自动生成 · TempGuard · @@DATE@@*
