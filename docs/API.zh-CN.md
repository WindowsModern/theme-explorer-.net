# ThemeExplorer.Core 类库 API 参考

版本 1.0 · .NET Framework 4.0 · C# 6 · AnyCPU

`ThemeExplorer.Core` 是对 Windows `UXTHEME.DLL` 视觉样式 API 的托管封装，负责：

- 打开/关闭主题句柄（`HTHEME`），并处理窗口子类（`SetWindowTheme`）；
- 用强类型方法读取 `TMT_*` 属性；
- 提供类 / 部件 / 状态 / 属性的只读数据库；
- 提供背景、文字和位图的绘制辅助。

程序集：`ThemeExplorer.Core.dll`　命名空间：`ThemeExplorer.Core`　依赖：`System.Drawing`

> 源码中的 XML 注释为中英双语（英文 `<summary>` + 中文 `<para xml:lang="zh-CN">`），编译产物旁边的 `ThemeExplorer.Core.xml` 会同时包含两种语言，可直接被 Visual Studio 的 IntelliSense 使用。

---

## 目录

1. [快速开始](#1-快速开始)
2. [对象模型与生命周期](#2-对象模型与生命周期)
3. [线程模型](#3-线程模型)
4. [错误处理](#4-错误处理)
5. [API 参考](#5-api-参考)
6. [数据约定](#6-数据约定)
7. [绘制与 WPF 集成](#7-绘制与-wpf-集成)
8. [内存与资源管理](#8-内存与资源管理)
9. [性能建议](#9-性能建议)
10. [兼容性与已知差异](#10-兼容性与已知差异)

---

## 1. 快速开始

```csharp
using System;
using ThemeExplorer.Core;

using (var context = new VisualStyleContext())
using (VisualStyle style = context.Open("Button"))      // 未被主题定义时返回 null
{
    if (style == null)
        return;

    // 直接按属性 id 读取（TMT_TEXTCOLOR = 3803）
    System.Drawing.Color color;
    if (style.TryGetColor(1, 1, 3803, out color))
        Console.WriteLine("TextColor = {0}", color);

    // 或按数据库中的定义读取，拿到强类型值
    ThemePropertyInfo property;
    if (ThemeDatabase.TryGetProperty(3802, out property))
    {
        ThemePropertyValue value;
        if (style.TryGetProperty(1, 1, property, out value))
        {
            ThemePropertyOrigin origin = style.GetPropertyOrigin(1, 1, property.Id);
            Console.WriteLine("{0} = {1} (来源: {2})", property.Name, value, origin);
        }
    }
}
```

要点：

- 一个 `VisualStyleContext` 对应一个 UI 线程；可以反复 `Open`。
- `Open` 返回 `null` 表示当前视觉样式没有定义这个类（例如 Windows 11 上的 `Clock`）。
- 所有 `Try*` 方法在主题不支持该属性时返回 `false`，不抛异常。

---

## 2. 对象模型与生命周期

```
VisualStyleContext          拥有一个隐藏的 STATIC 窗口（用于 SetWindowTheme 子类）
        │
        └── Open(className, subClass) ──► VisualStyle
                                             │ 拥有 HTHEME（SafeHandle）
                                             └── TryGet*/GetPropertyOrigin
ThemeDatabase               进程级只读数据（44 类 + 153 子类 / 335 部件 / 670 状态 / 227 属性）
ThemeRenderer               无状态绘制/位图辅助
ThemeSystem                 进程级主题状态与当前主题文件信息
```

| 类型 | 拥有的本机资源 | 释放方式 | 生命周期建议 |
| --- | --- | --- | --- |
| `VisualStyleContext` | 一个隐藏 HWND | `Dispose()` / `using` | 每个 UI 线程一个，窗口关闭时释放 |
| `VisualStyle` | 一个 `HTHEME` | `Dispose()` / `using` | 用完立即释放；不要缓存到下次选择 |
| `ThemeRenderer.GetBitmap` 返回的 `Bitmap` | GDI+ 位图（主题位图的副本） | `Dispose()` / `using` | 转成 WPF 图像后立即释放 |
| `ThemePropertyValue` / 模型对象 / `ThemeMargins` | 无 | GC | 可安全持有 |
| `BitmapSource`（WPF 侧） | WPF 管理 | GC | 冻结后可跨线程；替换引用即可回收 |

推荐写法：

```csharp
using (var context = new VisualStyleContext())
{
    using (VisualStyle style = context.Open("TaskDialog", null))
    {
        // ... 读取属性 / 绘制 ...
    }
}
```

不要这样做：

```csharp
// 错误：句柄在 GC 之前一直存在；大量选择会累积 HTHEME
var style = context.Open("Button");
```

`VisualStyle.Dispose()` 幂等；释放后再调用实例方法会抛 `ObjectDisposedException`。

---

## 3. 线程模型

- 库不做线程封送。`VisualStyleContext` 创建的隐藏窗口有线程亲和性，必须在创建它的线程上使用和释放。
- WinForms / WPF 中请在 UI 线程创建 `VisualStyleContext`；工作线程读取属性需要自行切回 UI 线程（或为工作线程单独创建 context）。
- `ThemeDatabase` 是不可变静态数据，可跨线程读取。
- `ThemeRenderer` 无状态，可在任意线程使用，但 `Graphics`/`HDC` 必须由调用方保证线程安全。

---

## 4. 错误处理

| 情况 | 行为 |
| --- | --- |
| 主题未定义某类 | `Open` 返回 `null` |
| 主题未定义某部件/状态/属性 | `Try*` 返回 `false`，输出参数为默认值 |
| 无效参数（`null` class、`null` property） | `ArgumentNullException` / `ArgumentException` |
| 对象已释放 | `ObjectDisposedException` |
| 隐藏窗口创建失败 | `ThemeException`（含 `NativeHResult`） |
| 绘制失败（主题不支持该部分） | `ThemeRenderer.Try*` 返回 `false` |

`ThemeException`：

```csharp
public class ThemeException : Exception
{
    public string Operation { get; }      // 失败的 API 名称
    public int NativeHResult { get; }     // 原始 HRESULT
    public COMException AsComException();
}
```

注意：`GetThemePropertyOrigin` 在 Windows 10/11 上对**不存在的属性**也会返回 `PO_GLOBAL`，因此不能用它判断属性是否存在。应使用 `TryGetProperty`/`TryGetInt` 等的返回值。

---

## 5. API 参考

### 5.1 `ThemeSystem`（静态）

| 成员 | 说明 |
| --- | --- |
| `bool IsThemeActive` | 当前会话是否启用了视觉样式 |
| `bool IsAppThemed` | 当前进程是否启用了主题（清单/comctl32 v6） |
| `bool IsVisualStylesEnabled` | 上两者同时为真 |
| `ThemeInfo GetCurrentTheme()` | 当前主题文件、配色方案、尺寸名称 |

`ThemeInfo`：`FileName`（完整路径）、`ShortFileName`、`ColorScheme`、`SizeName`。

### 5.2 `VisualStyleContext`

| 成员 | 说明 |
| --- | --- |
| `VisualStyleContext()` | 创建隐藏辅助窗口；失败抛 `ThemeException` |
| `VisualStyle Open(string className, string subClass = null)` | 应用子类并打开主题；未定义返回 `null` |
| `void Dispose()` | 销毁隐藏窗口 |

`className` 使用主题类名，如 `"Button"`、`"Toolbar"`；`subClass` 使用窗口子类名，如 `"ExplorerBar"`。子类会先通过 `SetWindowTheme` 应用到辅助窗口，再调用 `OpenThemeData`。

### 5.3 `VisualStyle`

| 成员 | 说明 |
| --- | --- |
| `bool IsDisposed` | 句柄是否已释放 |
| `bool IsPartDefined(int part, int state)` | 部件/状态是否定义 |
| `bool TryGetInt(int part, int state, int propId, out int value)` | 读取 `TMT_INT`/`TMT_ENUM` |
| `bool TryGetBool(...)` | 读取 `TMT_BOOL` |
| `bool TryGetColor(...)` | 读取 `COLORREF` → `System.Drawing.Color` |
| `bool TryGetString(...)` | 读取 `TMT_STRING`（2 KB 缓冲） |
| `bool TryGetFilename(...)` | 读取 `TMT_FILENAME` |
| `bool TryGetMargins(...)` | 读取 `TMT_MARGINS` |
| `bool TryGetRect(...)` | 读取 `TMT_RECT` |
| `bool TryGetPosition(...)` | 读取 `TMT_POSITION` |
| `bool TryGetPartSize(int part, int state, ThemeSize size, out Size value)` | `GetThemePartSize` |
| `bool TryGetFont(...)` | 读取 `LOGFONT` → `ThemeFontInfo` |
| `bool TryGetIntList(...)` | 读取 `TMT_INTLIST`（最多 409 项） |
| `bool TryGetStream(...)` | 读取内存流长度（Vista+） |
| `ThemePropertyOrigin GetPropertyOrigin(...)` | 属性来源（state/part/class/global） |
| `bool TryGetProperty(int part, int state, ThemePropertyInfo property, out ThemePropertyValue value)` | 按数据库定义读取并解析 |
| `void Dispose()` | 关闭 `HTHEME` |

`TryGetProperty` 会根据 `ThemePropertyInfo.Type` 自动选择 getter，并填充对应的强类型字段。

### 5.4 `ThemeDatabase` 与模型

| 成员 | 说明 |
| --- | --- |
| `IList<ThemeClassInfo> Classes` | 197 个类/子类，按数据库顺序 |
| `IList<ThemePropertyInfo> Properties` | 227 个 `TMT_*` 属性 |
| `bool TryGetProperty(int propertyId, out ThemePropertyInfo property)` | 按数字 id 查找属性定义 |

模型：

- `ThemeClassInfo`：`Name`、`SubClass`、`DisplayName`、`Parts`
- `ThemePartInfo`：`Id`、`Name`、`DisplayName`、`States`
- `ThemeStateInfo`：`Id`、`Name`、`DisplayName`
- `ThemePropertyInfo`：`Type`、`Id`、`Name`（`TMT_*`）、`DisplayName`

所有集合都是 `ReadOnlyCollection`，不可修改。

### 5.5 枚举

| 枚举 | 值 |
| --- | --- |
| `ThemePropertyType` | `Enum`, `String`, `Int`, `Bool`, `Color`, `Margins`, `Filename`, `Size`, `Position`, `Rect`, `Font`, `IntList`, `DiskStream`, `Stream` |
| `ThemePropertyOrigin` | `NotDefined=0`, `State=1`, `Part=2`, `Class=3`, `Global=4` |
| `ThemeSize` | `Min=0`, `True=1`, `Draw=2` |
| `ThemeBitmapKind` | `Background`, `Glyph` |
| `ThemeTextFormat` | `Left`, `Center`, `Right`, `VerticalCenter`, `WordBreak`, `SingleLine`, `NoPrefix` |

### 5.6 `ThemePropertyValue`

| 字段/属性 | 何时有效 |
| --- | --- |
| `Type` | 始终 |
| `IntValue`、`EnumSymbol` | `Enum`、`Int` |
| `TextValue` | `String`、`Filename` |
| `BoolValue` | `Bool` |
| `ColorValue` | `Color` |
| `MarginsValue` | `Margins` |
| `RectValue` / `PointValue` / `SizeValue` | `Rect` / `Position` / `Size` |
| `FontValue` | `Font` |
| `IntListValue` | `IntList` |
| `StreamLength` | `Stream`、`DiskStream` |

`ToString()` 返回与语言无关的调试文本；GUI 使用自己的本地化格式化器（`ThemeExplorer.Ui.PropertyValueFormatter`）。

### 5.7 `ThemeMargins` / `ThemeFontInfo`

- `ThemeMargins`：`Left`、`Right`、`Top`、`Bottom`，值相等语义，`ToString()` 输出 `l,r (horz), t,b (vert)`。
- `ThemeFontInfo`：`FaceName`、`Height`、`Weight`、`Italic`、`Underline`；`ToFont(float dpiScale)` 创建一个近似的 `System.Drawing.Font`。

### 5.8 `ThemeRenderer`（静态）

| 成员 | 说明 |
| --- | --- |
| `bool TryDrawBackground(Graphics, VisualStyle, part, state, Rectangle)` | 画到 GDI+ 图形 |
| `bool TryDrawBackground(IntPtr hdc, ...)` | 画到已有 HDC（WPF/DIB 场景） |
| `bool TryDrawText(Graphics/IntPtr, ...)` | 画主题文字 |
| `bool TryDrawParentBackground(IntPtr hwnd, Graphics, Rectangle)` | 透明部件的父窗口背景 |
| `bool IsBackgroundPartiallyTransparent(...)` | 是否需要先画父背景 |
| `Bitmap GetBitmap(VisualStyle, part, state, ThemeBitmapKind)` | 返回主题位图的**托管副本**，调用方负责 `Dispose` |
| `bool TryGetPartSize(VisualStyle, part, state, ThemeSize, out Size)` | 部件尺寸 |
| `bool TryGetStreamLength(...)` | 内存流长度 |

`GetBitmap` 使用 `GBF_DIRECT` 取得主题拥有的位图，然后立即复制为 `System.Drawing.Bitmap`；不会把主题位图句柄暴露给调用方。

### 5.9 `ThemeException`

见[错误处理](#4-错误处理)。

---

## 6. 数据约定

- **part / state id**：直接使用 Windows SDK 中的 `xxx_P_*` / `xxx_S_*` 常量值（数据库里带有名称）。
- **属性 id**：`TMT_*` 常量值；`ThemePropertyInfo.Id` 即该数值。
- **颜色**：`COLORREF` 是 `0x00BBGGRR`；库已转换为 `System.Drawing.Color`。
- **尺寸/位置/边距**：单位是像素，受进程 DPI 影响。高 DPI 下主题返回的像素值本身会放大。
- **字体高度**：`LOGFONT.lfHeight`，通常为负值（字符高度）。
- **流**：`TryGetStream` 只返回长度；内容在主题模块内，库不复制。
- **TMT_SIZE**：`ThemePropertyType.Size` 只对 `TS_MIN`/`TS_TRUE`/`TS_DRAW`（id 0–2）有效；`TMT_SIZE`(207) 不是合法的 `THEMESIZE`，`TryGetProperty` 会返回 `false`。

---

## 7. 绘制与 WPF 集成

### WinForms

```csharp
protected override void OnPaint(PaintEventArgs e)
{
    using (var context = new VisualStyleContext())
    using (VisualStyle style = context.Open("Button"))
    {
        if (style != null)
            ThemeRenderer.TryDrawBackground(e.Graphics, style, 1, 1, ClientRectangle);
    }
}
```

实际使用时应复用 `VisualStyleContext`，不要每次 `OnPaint` 都创建。

### WPF

WPF 没有 HDC，推荐用 DIB 过渡：

1. `CreateDIBSection` 创建 32bpp 顶朝下的位图；
2. `CreateCompatibleDC` + `SelectObject`；
3. 用 `Graphics.FromHdc` 填充背景，再调用 `ThemeRenderer.TryDrawBackground(hdc, ...)`；
4. 把像素复制到 `byte[]`；
5. `BitmapSource.Create(..., PixelFormats.Bgr32, ...)` 并 `Freeze()`；
6. `finally` 中 `SelectObject` 还原、`DeleteObject`、`DeleteDC`。

完整实现见 [`src/ThemeExplorer.Wpf/ThemeRenderService.cs`](../src/ThemeExplorer.Wpf/ThemeRenderService.cs)，内存策略见 [WPF 内存设计](WPF.zh-CN.md)。

---

## 8. 内存与资源管理

库的原则：**不缓存句柄、不持有静态图像、所有本机资源在 `Dispose`/`finally` 中释放。**

| 资源 | 谁释放 | 备注 |
| --- | --- | --- |
| 隐藏窗口 HWND | `VisualStyleContext.Dispose` | 每个 context 一个 |
| `HTHEME` | `VisualStyle.Dispose`（SafeHandle） | 即使忘记 Dispose，SafeHandle 最终也会关闭，但不要依赖终结器 |
| GDI 位图（`GetBitmap`） | 调用方 `Dispose` | 已复制，和主题生命周期无关 |
| DIB/DC（WPF 渲染） | `ThemeRenderService` 的 `finally` | 每次渲染即建即毁 |
| `BitmapSource` | GC | `Freeze()` 后无 UI 线程亲和性 |

建议：

- 每次选择只打开一个主题，画完/读完立即 `Dispose`；不要跨选择缓存 `VisualStyle`。
- 不要在 `OnPaint` 中打开主题或枚举 227 个属性；在选中变化时计算一次。
- GUI 关闭时释放 `VisualStyleContext` 并清空图像引用。
- 如果自己缓存 `Bitmap`/`BitmapSource`，请实现上限或清除策略。

---

## 9. 性能建议

- `OpenThemeData` + 首次属性读取有固定开销；一次选择打开一次即可。
- 枚举全部 227 个属性会产生 200+ 次本机调用，建议只在选中变化时做，不要每帧做。
- 字符串/格式化结果可缓存；`ThemePropertyValue` 很小，可安全保留。
- WPF 中按需渲染预览（9 张图）通常只需几毫秒；避免在滚动/动画中重复渲染同一 part/state。
- 高 DPI 下按设备像素渲染一次并缓存，避免每次缩放重绘。

---

## 10. 兼容性与已知差异

- 目标框架 .NET Framework 4.0；C# 6；AnyCPU（32/64 位均可，已在 64 位下验证）。
- 支持 Windows Vista 及以上；`GetThemeBitmap`、`GetThemeStream` 在缺少导出的系统上自动降级为 `false`。
- 与原 C 实现的差异：
  - `TMT_SIZE` 不再把属性 id 当 `THEMESIZE` 传入（见 §6）。
  - 不提供 `DrawThemeText` 的 GDI 字体选择；主题字体由 UXTHEME 决定。
  - 数据库保留原版的类/部件/状态名称与结构。
- `GetThemePropertyOrigin` 的 `PO_GLOBAL` 行为是操作系统差异，不是库的 bug。

---

## 附：完整示例

```csharp
using System;
using ThemeExplorer.Core;

class Program
{
    static void Main()
    {
        using (var context = new VisualStyleContext())
        {
            foreach (ThemeClassInfo cls in ThemeDatabase.Classes)
            {
                using (VisualStyle style = context.Open(cls.Name, cls.SubClass))
                {
                    if (style == null)
                        continue;

                    foreach (ThemePartInfo part in cls.Parts)
                    {
                        foreach (ThemeStateInfo state in part.States)
                        {
                            Console.WriteLine("{0} / {1} / {2}",
                                cls.DisplayName, part.DisplayName, state.DisplayName);

                            foreach (ThemePropertyInfo property in ThemeDatabase.Properties)
                            {
                                ThemePropertyValue value;
                                if (style.TryGetProperty(part.Id, state.Id, property, out value))
                                    Console.WriteLine("  {0} = {1}", property.Name, value);
                            }
                        }
                    }
                }
            }
        }
    }
}
```

注意：这个示例会遍历所有状态并读取所有属性，仅用于演示 API；实际工具应按需读取。
