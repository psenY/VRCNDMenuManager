# psenY7 ND Menu Manager (VRChat 非破坏性改模菜单管理器)

基于 **NDMF (Non-Destructive Modular Framework)** 和现代 **UI Toolkit (UIElements)** 构建的 VRChat Avatars 3.0 非破坏性菜单与动画状态机自动化管理器。

---

## 核心特性

- **真正非破坏性架构**：基于 NDMF 构建管线，在上传或进入 Play Mode 时动态编译生成 Animation Clips、FX 状态机层和 VRC 菜单，原模型 Descriptor 和 Controller 资产 100% 保持干净无污染。
- **一键向导生成与智能增量追加**：
  - 支持多套衣服、发型、体型一键拖拽自动生成完整互斥菜单与防脱光保护。
  - **智能增量追加 (Smart Append)**：支持在已有衣柜/菜单中直接追加新衣服，自动顺延编号，100% 保留老衣服及调好的形态键。
- **部件 3D 独立隔离渲染预览**：
  - 点击任何组件可打开独立 3D 预览视窗，支持网格面数/材质查看与 Hierarchy 一键定位。
  - 支持一键拍摄并生成 3D 缩略图图标。
- **全场景改模覆盖**：
  - **单选互斥衣物组 (Exclusive Switcher)**：多套服装/发型多选一，自动生成 Int 参数与互斥动画。
  - **独立子配件开关 (Sub Toggles)**：支持多部件合并或独立控制外套、帽子、袜子。
  - **形态键/色相调节 (Radial Puppet)**：BlendShape 0~100%、材质浮点属性平滑过渡。
  - **无限层级子菜单 (SubMenu)**：自由组织和分类控制项。
- **256-Bit 参数预算监控与优化诊断中心**：
  - 实时分析 Bool (1-bit)、Int (8-bit)、Float (8-bit) 参数消耗。
  - 具备参数搜索、筛选、一键定位、优化诊断及 Markdown 报告复制导出功能。

---

## 安装与引入

### 方法 1：通过 Unity Package Manager (Git URL 安装)
1. 打开 Unity 编辑器菜单栏：`Window` -> `Package Manager`。
2. 点击左上角 `+` 号，选择 **Add package from git URL...**。
3. 输入以下地址并点击 Add：
   ```text
   https://github.com/psenY/VRCNDMenuManager.git
   ```

### 方法 2：通过 Package Manager (本地文件夹添加)
1. 打开 `Window` -> `Package Manager`。
2. 点击 `+` 号，选择 **Add package from disk...**。
3. 选择本仓库根目录下的 `package.json` 文件。

### 依赖环境
- Unity 2022.3 (VRChat 推荐版本)
- VRChat SDK - Avatars 3.0 (`com.vrchat.avatars` >= 3.5.0)
- NDMF (`nadena.dev.ndmf` >= 1.4.0)

---

## 快速上手

1. 打开菜单窗口：点击顶部菜单 `Tools` -> **psenY7 ND Menu Manager**。
2. 窗口会自动检测并绑定当前场景中的 Avatar。
3. 切换视图：
   - **一键向导生成**：批量拖入衣服或发型，快速生成完整互斥菜单与子开关。
   - **菜单层级管理**：可视化树状管理、拖拽排序、形态键与材质控制项编辑。
   - **参数总览与预算**：查看 256 位预算分布、一键定位参数绑定的物体。
4. **无需手动合并菜单或编写 Animator Controller**，直接进入 Play Mode 或使用 VRChat SDK 上传即可！
