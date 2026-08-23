# 🗂️ VRCNDMenuManager

**VRChat 非破坏性改模菜单与状态机生成向导 (Non-Destructive Expressions Menu Manager & Wizard)**

[![Unity](https://img.shields.io/badge/Unity-2022.3+-black.svg?style=flat&logo=unity)](https://unity.com/)
[![VRChat](https://img.shields.io/badge/VRChat-Avatar%203.0-blue.svg?style=flat&logo=vrchat)](https://vrchat.com)
[![NDMF](https://img.shields.io/badge/NDMF-1.4.0+-green.svg?style=flat)](https://github.com/bdunderscore/ndmf)
[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](LICENSE)
[![VPM Listing](https://img.shields.io/badge/VPM-Repository-2ea44f?style=flat&logo=github)](https://pseny.github.io/vpm-repository/)
[![Release](https://img.shields.io/github/v/release/psenY/VRCNDMenuManager?color=orange&logo=github)](https://github.com/psenY/VRCNDMenuManager/releases)

---

## 📖 项目简介 (Introduction)

**VRCNDMenuManager** 是一款专为 VRChat Avatar 3.0 设计的现代、高性能、**纯非破坏性 (Non-Destructive)** 菜单与动画状态机自动化管理工具。  
基于 **NDMF (Non-Destructive Modular Framework)** 构建管线与现代 **UI Toolkit (UIElements)** 响应式架构，提供**一键拖拽批量向导**、**智能增量追加 (Smart Append)**、**3D 部件隔离视窗与缩略图拍摄**及 **256-Bit 参数预算监控与诊断中心**。在上传或测试时自动动态生成动画剪辑、FX 状态机层与 VRC 菜单，模型源文件 100% 保持干净无污染。

---

## 🌟 核心特性 (Key Features)

- 🔒 **纯非破坏性架构 (Non-Destructive NDMF Workflow)**：
  - 在点击 VRChat 上传或进入 Play 测试时，由 NDMF 编译管线在内存临时副本中动态生成动画、FX Controller 状态机及 Expressions Menu。
  - 原模型 Descriptor、FX 控制器与菜单资产保持 100% 原始干净，彻底杜绝资产污染与多插件冲突。
- 🪄 **一键批量向导与智能增量追加 (Smart Wizard & Incremental Append)**：
  - **批量向导**：将多套衣服或发型拖入向导区域，一键自动解析子散件、建立单选互斥 Int 参数组与防脱光保护。
  - **智能增量追加 (Smart Append)**：当你在已有衣柜/菜单中需要加新衣服时，无需推倒重做！向导自动识别现有菜单与最大编号，无缝追加新物品，**100% 完整保留老衣服的子开关、自定义设置与已调好的形态键 (BlendShapes)**。
- 👗 **解耦级联控制与配件子开关 (Clean Hierarchy & Sub-Toggles)**：
  - 主开关仅控制衣服根节点，自然级联内部散件；内部子开关（外套、帽子、袜子、鞋子）独立控制，杜绝主开关与子开关动画冲突与状态覆盖。
- 🔍 **部件 3D 独立隔离渲染预览 (3D Mesh Isolation & Thumbnail)**：
  - 点击任何组件可打开独立 3D 预览视窗，支持网格面数/材质统计、多角度旋转观察与 Hierarchy 一键高亮定位。
  - 支持一键拍摄并自动裁切生成 3D 缩略图图标。
- 🔘 **全场景改模覆盖 (Full Modding Support)**：
  - **单选互斥衣物组 (Exclusive Switcher)**：多套服装/发型多选一，自动生成共享 Int 参数与互斥动画。
  - **独立物体开关 (Toggle)**：支持多部件合并或独立控制。
  - **形态键/色相调节 (Radial Puppet)**：BlendShape 0~100%、材质浮点属性平滑过渡。
  - **无限层级子菜单 (SubMenu)**：自由拖拽组织与分类控制项。
- 📊 **256-Bit 参数预算监控与诊断中心 (Bit Budget & Parameter Inspector)**：
  - 实时分析 Bool (1b)、Int (8b)、Float (8b) 参数消耗并可视化进度看板。
  - 具备参数全局搜索、筛选、一键反查绑定源物体、冗余优化诊断及 Markdown 报告复制导出功能。

---

## 🚀 安装方式 (Installation)

### 方式 1：通过 VCC / ALCOM 一键安装 (推荐 ⭐)
1. 打开 **[psenY7's VPM Listing](https://pseny.github.io/vpm-repository/)** 仓库主页。
2. 点击 **Add to VCC** 按钮一键导入，或在 VCC / ALCOM 设置中添加 VPM 仓库源：
   ```text
   https://pseny.github.io/vpm-repository/index.json
   ```
3. 在工程管理页面搜索 **psenY7 ND Menu Manager**，点击安装即可。

### 方式 2：通过 Unity Package Manager (UPM Git URL)
1. 打开 Unity，在顶部菜单栏选择 **Window** -> **Package Manager**。
2. 点击左上角的 **`+`** 按钮，选择 **`Add package from git URL...`**。
3. 粘贴仓库地址：
   ```text
   https://github.com/psenY/VRCNDMenuManager.git
   ```
4. 点击 **Add** 即可完成自动安装与后续一键更新。

### 方式 3：从本地磁盘安装 (UPM Disk / 源码复制)
- **UPM 本地包**：下载仓库后在 Package Manager 中选择 `Add package from disk...` 并指向 `package.json`。
- **源码导入**：将仓库中的 `Runtime` 与 `Editor` 文件夹直接复制到 Unity 项目的 `Assets/` 目录下。

### 依赖环境
- Unity 2022.3 (VRChat 推荐版本)
- VRChat SDK - Avatars 3.0 (`com.vrchat.avatars` >= 3.5.0)
- NDMF (`nadena.dev.ndmf` >= 1.4.0)

---

## 📖 使用指南 (Usage)

1. **打开窗口**：在 Unity 顶部菜单栏点击：
   `Tools` -> `psenY7 ND Menu Manager`
2. **选择 Avatar**：窗口会自动检测并绑定场景中的 Avatar 模型（也可手动指定）。
3. **快速配置**：
   - **一键向导生成**：拖入多套衣服或发型，勾选配件子开关后一键生成完整互斥菜单体系。
   - **智能增量追加**：已有菜单时直接拖入新衣服，系统自动顺延编号并保留全部老设置。
   - **菜单层级管理**：可视化管理树状菜单，支持形态键、材质浮点参数与无限子菜单。
   - **参数总览与预算**：查看 256 位参数消耗分布，一键定位参数绑定的物体。
4. **一键上传 / 测试**：正常点击 VRChat SDK 的 **Build & Publish** 或进入 **Play 模式**，插件在后台自动完成非破坏性编译构建，模型 Descriptor 干净无痕！

---

## 🔗 系列插件 (Related Tools)

- 🦴 **[VRCPhysBoneMerger](https://github.com/psenY/VRCPhysBoneMerger)**：专为 VRChat 设计的非破坏性动骨自动合并与优化组件。
- 📦 **[VRCPackageInspector](https://github.com/psenY/VRCPackageInspector)**：UnityPackage 极速免导入资源与动骨分析器。
- 👗 **[VRCFuryBlendShapeLinkHelper](https://github.com/psenY/VRCFuryBlendShapeLinkHelper)**：智能 VRCFury 形态键连线与绑定工具。
- 🗂️ **[VRCNDMenuManager](https://github.com/psenY/VRCNDMenuManager)**：基于 NDMF 的 VRChat 非破坏性菜单与改模向导管理器。
- 🌐 **[psenY7's VPM Listing](https://pseny.github.io/vpm-repository/)**：VRChat 创作者插件与工具统一订阅源。

---

## 📄 开源许可 (License)

本项目基于 [GNU General Public License v3.0 (GPL-3.0)](LICENSE) 开源。欢迎提交 Issue 或 Pull Request！

