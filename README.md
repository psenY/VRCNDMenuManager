# ND Menu Manager (VRChat 非破坏性菜单管理器)

基于 **NDMF (Non-Destructive Modular Framework)** 和现代 **UI Toolkit (UIElements)** 的 VRChat Avatars 3.0 菜单与状态机生成插件。

---

## ✨ 核心特性

- 🛡️ **真正非破坏性**：基于 NDMF 构建管线，在上传或进入 Play Mode 时动态生成动画剪辑、FX 状态机层和 VRC 菜单，原模型 Descriptor 和 Controller 资产 100% 保持干净。
- 🎨 **现代暗黑风 UI Toolkit**：可视化树状列表、分栏即选即编、拖拽排序与响应式数据绑定。
- 🔘 **全场景改模覆盖**：
  - **物体显隐开关 (Toggle)**：衣服/饰品开启与关闭。
  - **互斥衣物组 (Toggle Group)**：多套服装/发型多选一，自动生成 Int 参数与互斥动画。
  - **形态键/色相滑条 (Radial Puppet)**：BlendShape 0~100%、材质浮点属性滑条。
  - **无限层级子菜单 (SubMenu)**：自由组织和分类控制项。
- 📊 **256-Bit 参数预算监控**：
  - 实时计算 Bool (1-bit)、Int (8-bit)、Float (8-bit) 参数消耗。
  - 超出预算时变色警示，但**不进行硬性阻断**（兼容 VRCFury 上传自动压缩机制）。

---

## 📦 安装与引入

### 方法 1：通过 Unity Package Manager (本地添加)
1. 打开 Unity 编辑器菜单栏：`Window` -> `Package Manager`。
2. 点击左上角 `+` 号，选择 **Add package from disk...**。
3. 选中本仓库的 `package.json` 文件即可。

### 依赖环境
- Unity 2022.3 (VRChat 推荐版本)
- VRChat SDK - Avatars 3.0 (`com.vrchat.avatars` >= 3.5.0)
- NDMF (`nadena.dev.ndmf` >= 1.4.0)

---

## 🚀 快速上手

1. 打开菜单窗口：点击顶部菜单 `Tools` -> **ND Menu Manager**。
2. 窗口会自动检测并选择场景中的 Avatar（也可手动拖拽指定）。
3. 使用左上角快捷按钮：
   - 点击 **`+ 开关 (Toggle)`** 为当前选中的物体添加开关组件。
   - 点击 **`+ 互斥组 (Group)`** 创建多选一服装组。
   - 点击 **`+ 滑条 (Radial)`** 创建 BlendShape 渐变滑条。
   - 点击 **`+ 子菜单 (SubMenu)`** 创建分类目录。
4. 在右侧面板配置目标 GameObject 或 BlendShape 名称。
5. **无需手动合并菜单或编写 Animator Controller**，直接进入 Play Mode 或使用 VRChat SDK 上传即可！
