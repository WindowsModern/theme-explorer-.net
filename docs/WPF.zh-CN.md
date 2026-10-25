# WPF 版本的内存设计与验证

本文说明 `ThemeExplorer.Wpf` 为什么这样渲染、哪些对象持有本机资源、如何保证回收，以及在当前机器上的实测结果。

## 1. 设计目标

1. **不在 WPF 里为每个预览创建子窗口**（不用 `HwndHost`/`HwndSource`），避免 HWND、USER 句柄和跨线程消息泵的泄漏风险。
2. **主题句柄即用即关**：一次选择打开 `HTHEME`，画完 9 个预览、2 个位图并读完属性后立刻 `Dispose`。
3. **每次渲染的 GDI 对象都在 `finally` 中销毁**：DIB section、memory DC、`Graphics` 包装、`SelectObject` 的旧对象。
4. **不保留静态图像缓存**：预览控件只持有一个冻结的 `BitmapSource`；替换引用后旧图即可被 GC 回收。
5. **窗口关闭时显式清理**：清空所有图像引用，并释放 `VisualStyleContext` 的隐藏窗口。

## 2. 渲染管线

WPF 的 `DrawingContext` 不能直接接收 HDC，而 `UXTHEME` 的绘制 API 是 GDI 的。桥接方式：

```
VisualStyle (HTHEME)
      │
      ▼
CreateDIBSection(32bpp, top-down) ──► memory DC ──► Graphics.FromHdc
      │                                                │
      │                          TryDrawBackground / TryDrawText
      ▼                                                │
Marshal.Copy(bits → byte[]) ◄──────────────────────────┘
      │
      ▼
BitmapSource.Create(PixelFormats.Bgr32) + Freeze()
      │
      ▼
Image（BitmapSource）显示
```

关键点：

- 使用 `Bgr32`（无 alpha）而不是 `Pbgra32`：GDI 绘制不会写入 alpha，若用带 alpha 的格式会出现全透明或黑边。半透明部件先以控件背景色填充，再绘制主题。
- 每次渲染都创建 DIB/DC，并在 `finally` 中：

```csharp
SelectObject(dc, previous);
DeleteObject(dib);
DeleteDC(dc);
```

- 像素复制到托管数组后立即销毁本机对象；`BitmapSource` 使用托管像素并 `Freeze()`，不再依赖 DIB。

## 3. 对象生命周期

| 对象 | 生命周期 | 释放 |
| --- | --- | --- |
| `VisualStyleContext` | 主窗口整个生命周期 | `MainWindow.OnClosed` |
| `VisualStyle` | 一次选择（`UpdateSelection`） | `using`，画完即关 |
| DIB / DC / `Graphics` | 一次预览渲染 | `finally` |
| `System.Drawing.Bitmap`（主题位图） | 一次位图转换 | `using` |
| `BitmapSource` | 直到下一次选择/关闭 | 替换引用，GC 回收 |
| `WriteableBitmap` | 不使用（见下） | — |

> 早期原型使用 `WriteableBitmap` 复用缓冲，但在“窗口未呈现”的压力测试中，WPF 会为每次 `WritePixels` 保留新的合成表面，导致 commit 波动很大。最终改为冻结的 `BitmapSource`：旧图在替换后直接成为垃圾，行为更可预测。

## 4. 压力测试与实测数据

自检命令：

```powershell
ThemeExplorer.Wpf.exe --lang=zh-Hans --selftest --out=<目录>
```

测试内容（见 `src/ThemeExplorer.Wpf/SelfTest.cs`）：

1. **纯渲染路径**：200 次 `RenderPart`（DIB → 像素）+ 200 次 `RenderPartSource`（DIB → `BitmapSource`），不经过 UI。
2. **选择压力**：150 次切换 Button 的 6 个状态；每次都会重新打开/关闭主题、渲染 9 个预览、读取属性并刷新列表。测试期间窗口隐藏，以排除 WPF 合成器表面的干扰。
3. **窗口开关**：连续创建/显示/选择/关闭 20 个 `MainWindow`，验证隐藏窗口、主题句柄、图像引用都能回收。

本机结果（Windows 11 / 125% DPI / 64 位）：

| 指标 | 结果 |
| --- | --- |
| 纯渲染 200 次：commit 增量 | **≤ +4 KB（GC 时机可能为负）** |
| 纯渲染 200 次（转 `BitmapSource`）：commit 增量 | **-0.1 ~ +0.9 MB** |
| 150 次选择：managed heap 增量 | **≈ +4 KB** |
| 150 次选择：commit 增量 | 仅记录（WPF 内部缓存波动，非断言项） |
| 150 次选择：working set 增量 | 仅记录 |
| 150 次选择：GDI 对象增量 | **0** |
| 150 次选择：USER 对象增量 | **0 ~ -1** |
| 150 次选择：进程句柄增量 | **0 ~ +2** |
| 20 次窗口开关：GDI 增量 | **-4 ~ +3** |
| 20 次窗口开关后剩余窗口 | **1（仅主窗口）** |
| 20 次窗口开关：USER 增量 | **0 ~ -1** |
| 20 次窗口开关：进程句柄增量 | **-1 ~ +1** |

结论：GDI/USER/进程句柄在误差范围内回到基线；托管堆基本不增长（约 4 KB），没有随选择次数线性增长；纯渲染路径没有本机内存增长。commit/working set 会受 WPF 内部渲染缓存影响而波动，因此只记录、不作断言。

## 5. 关于“可见窗口”的说明

如果压力循环在**可见但从未被真正呈现**的窗口（例如放在屏幕外）上运行，WPF 会为渲染队列保留不定数量的合成表面，commit 可能瞬时增加几十 MB；这些表面由 WPF 合成器管理，不是 `HTHEME`/GDI/USER 句柄泄漏，正常显示和关闭窗口后会被回收。

因此自检采取两条独立的验证：

- 渲染路径（无 UI）直接测 commit；
- 选择压力隐藏窗口，测应用对象与句柄；
- 窗口开关单独测句柄回收。

这比在屏幕外高速刷新一个不可见窗口更能反映真实使用中的内存行为。

## 6. 使用与扩展建议

- 关闭主窗口时 `VisualStyleContext` 必须 `Dispose`（代码已处理）；自定义窗口请遵循同样模式。
- 不要为每个预览创建 `HwndHost`；本项目的 DIB 方案已经足够快（9 个预览通常在几毫秒内完成）。
- 如果要做实时动画，建议缓存当前 part/state 的图像，而不是每帧重新打开主题。
- 若未来升级到 .NET 4.6.2+，可改用 `VisualTreeHelper.GetDpi` 做逐显示器 DPI；当前 `net40` 版本按系统 DPI 渲染。
- 大量浏览不同类时，TreeView 会保留已展开的节点；这是正常的 UI 状态，不是泄漏。需要限制内存时可在切换类时折叠旧的节点。
