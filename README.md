# Moba - 移动端 MOBA 最小可行性 Demo

这是一个基于 Unity 6 与 URP（通用渲染管线）构建的移动端 MOBA 核心手感与移动控制验证 Demo。

---

## 📱 最新安卓可运行包下载

* **版本**：`v0.1.1` (Android)
* **发布日期**：2026-10-31
* **架构**：ARM64 (`arm64-v8a`)，IL2CPP，横屏运行
* **下载地址**：[📦 点击下载 MobaDemo.apk (GitHub Release v0.1.1)](https://github.com/LinHouYu/Moba/releases/download/v0.1.1/MobaDemo.apk)
* **Releases 页面**：[查看所有版本与更新历史](https://github.com/LinHouYu/Moba/releases)

---

## 🛠️ 本次版本修复的 Bug 详述 (v0.1.1)

### 1. 修复真机启动全屏灰色、无 3D 人物与场景问题
* **现象**：在手机上启动 APK 后，屏幕整体呈深灰色，没有地面网格和角色胶囊体。
* **根本原因**：
  * 在 URP 移动端打包时，`UniversalRenderPipelineGlobalSettings` 启用了激进的代码剔除（`m_StripUnusedPostProcessingVariants: 1`），导致打包阶段 URP 核心的 `UberPost.shader` 变体被 Unity 自动剥离。
  * 主相机配置了后处理与动态缩放，管线必须通过 `PostProcessPass`（Blit）将 3D 渲染缓冲拷贝至物理屏幕，因 Shader 丢失导致真机上 Blit Pass 执行异常中止，3D 画面未能成功拷贝到屏幕，屏幕上遗留的是 Unity Splash 启动界面的深灰色背景。
* **修复措施**：
  * 将 `UniversalRenderPipelineGlobalSettings` 中的 `StripUnusedPostProcessingVariants` 设为 `0`，禁止剔除核心 Blit 变体。
  * 将 `Mobile_RPAsset` 渲染缩放率设为 `1.0`（直出 Backbuffer），关闭 `SupportsHDR`。
  * 主相机关闭 `RenderPostProcessing` 与 `AllowHDROutput`，确保在无后处理开销下以原生画质直出。
  * 关闭 `Mobile_Renderer` 中的 `UseNativeRenderPass`，避免骁龙 Adreno 750 与 Vulkan Subpass 驱动冲突。

### 2. 修复拖拽虚拟摇杆产生大量 UI 重叠残影与拖尾
* **现象**：在真机上滑动左下角虚拟摇杆时，摇杆把手在移动路径上不断重叠留下残影。
* **根本原因**：
  * UI Canvas 运行于 `ScreenSpace - Overlay` 模式，直接向屏幕物理显存绘制。
  * 由于上述 3D 场景 Blit 拷贝中断，且主相机的 ClearFlags 原为默认模式，导致物理屏幕缓冲区在每帧之间从未被清空。新一帧绘制的 UI 图标直接叠在旧像素上，造成了“千层饼”残影拖尾。
* **修复措施**：
  * 主相机 ClearFlags 明确设置为 `SolidColor`（`#1F242E`），强制每帧渲染开始时彻底抹平屏幕缓冲。
  * 优化虚拟摇杆的绘制与复位逻辑，滑动时实时刷新，松手后平滑干净回弹。

### 3. 历史问题修复汇总 (v0.1.0)
* **相机跟随与绑定**：修复了主相机未自动挂载跟随脚本以及摇杆 UI 未与输入管理器绑定的问题。
* **角色半截下沉**：修复了胶囊体 Collider 偏移与高度不匹配导致的半截入地问题。
* **双模操控**：支持电脑端 WASD / 方向键与移动端触摸虚拟摇杆无缝切换。

---

## 🎮 操作说明

* **电脑端**：
  * 移动：`W` `A` `S` `D` 或方向键
  * 虚拟摇杆：鼠标左键按住左下角区域拖拽
* **手机端**：
  * 移动：左下角半屏任意位置滑动虚拟摇杆（支持动态浮动摇杆与固定底座）
  * 视角：相机自动平滑跟随角色移动

---

## ⚙️ 技术栈与工程配置

* **引擎版本**：Unity 6 (`6000.4.1f1` / 推荐生产使用 `Unity 6000.0 LTS`)
* **渲染管线**：Universal Render Pipeline (URP Forward+)
* **脚本后端**：IL2CPP (ARM64)
* **图形 API**：Vulkan / GLES3
* **测试套件**：.NET 9 + VSTest 自动化单元测试（26 项用例）
