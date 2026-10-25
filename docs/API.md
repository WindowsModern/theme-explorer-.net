# ThemeExplorer.Core API Reference

Version 1.0 · .NET Framework 4.0 · C# 6 · AnyCPU

`ThemeExplorer.Core` is a managed wrapper around the Windows `UXTHEME.DLL` visual
styles API. It opens theme handles, resolves window subclasses, reads `TMT_*`
properties with strongly typed methods, ships a read-only class/part/state/property
database, and provides drawing helpers.

- Assembly: `ThemeExplorer.Core.dll`
- Namespace: `ThemeExplorer.Core`
- Reference: `System.Drawing`
- XML documentation ships next to the assembly (`ThemeExplorer.Core.xml`).  The comments are
  bilingual: every English summary is followed by a Chinese `<para xml:lang="zh-CN">`.

---

## Contents

1. [Quick start](#1-quick-start)
2. [Object model and lifetime](#2-object-model-and-lifetime)
3. [Threading](#3-threading)
4. [Error handling](#4-error-handling)
5. [API reference](#5-api-reference)
6. [Data conventions](#6-data-conventions)
7. [Drawing and WPF](#7-drawing-and-wpf)
8. [Memory and ownership](#8-memory-and-ownership)
9. [Performance](#9-performance)
10. [Compatibility and known differences](#10-compatibility-and-known-differences)

---

## 1. Quick start

```csharp
using System;
using ThemeExplorer.Core;

using (var context = new VisualStyleContext())
using (VisualStyle style = context.Open("Button"))      // null when the class is not themed
{
    if (style == null)
        return;

    System.Drawing.Color color;
    if (style.TryGetColor(1, 1, 3803, out color))        // TMT_TEXTCOLOR
        Console.WriteLine("TextColor = {0}", color);

    ThemePropertyInfo property;
    if (ThemeDatabase.TryGetProperty(3802, out property)) // TMT_FILLCOLOR
    {
        ThemePropertyValue value;
        if (style.TryGetProperty(1, 1, property, out value))
            Console.WriteLine("{0} = {1} ({2})", property.Name, value,
                style.GetPropertyOrigin(1, 1, property.Id));
    }
}
```

`Open` returns `null` when the current visual style does not define the class
(for example `Clock` on Windows 11). Every `Try*` method returns `false` instead of
throwing when a part/state/property is unsupported.

---

## 2. Object model and lifetime

```
VisualStyleContext          owns a hidden STATIC window (used for SetWindowTheme)
        │
        └── Open(className, subClass) ──► VisualStyle
                                             │ owns HTHEME (SafeHandle)
                                             └── TryGet* / GetPropertyOrigin
ThemeDatabase               process-wide immutable catalogue
ThemeRenderer               stateless drawing / bitmap helpers
ThemeSystem                 process theme state and current theme file
```

| Type | Owns | Release | Guidance |
| --- | --- | --- | --- |
| `VisualStyleContext` | One hidden HWND | `Dispose` / `using` | One per UI thread; dispose when the window closes |
| `VisualStyle` | One `HTHEME` | `Dispose` / `using` | Open per selection and dispose immediately |
| `Bitmap` from `ThemeRenderer.GetBitmap` | GDI+ copy of a theme bitmap | `Dispose` / `using` | Convert to a WPF image and dispose |
| `ThemePropertyValue`, models, `ThemeMargins` | Nothing native | GC | Safe to keep |
| `BitmapSource` (WPF) | WPF-managed | GC | `Freeze()` makes it thread-independent |

`VisualStyle.Dispose` is idempotent. Using a disposed instance throws
`ObjectDisposedException`.

---

## 3. Threading

- The library does not marshal calls. The hidden window created by
  `VisualStyleContext` has thread affinity; create, use and dispose it on the same
  thread.
- In WinForms/WPF, create the context on the UI thread. If a worker thread needs
  theme data, marshal back or create a separate context for that thread.
- `ThemeDatabase` is immutable and thread-safe.
- `ThemeRenderer` is stateless, but the `Graphics`/`HDC` you pass must be used by
  one thread at a time.

---

## 4. Error handling

| Situation | Behaviour |
| --- | --- |
| Class not themed | `Open` returns `null` |
| Part/state/property unsupported | `Try*` returns `false`; out value is default |
| Null/invalid argument | `ArgumentNullException` / `ArgumentException` |
| Object already disposed | `ObjectDisposedException` |
| Hidden window creation failed | `ThemeException` with `NativeHResult` |
| Drawing not supported by theme | `ThemeRenderer.Try*` returns `false` |

`GetThemePropertyOrigin` reports `PO_GLOBAL` even for property ids that do not
exist on Windows 10/11, so never use it as an existence check. Use the `Try*`
return value instead.

---

## 5. API reference

### `ThemeSystem` (static)

| Member | Description |
| --- | --- |
| `bool IsThemeActive` | Visual styles are active in this session |
| `bool IsAppThemed` | This process opted in to themed common controls |
| `bool IsVisualStylesEnabled` | Both of the above |
| `ThemeInfo GetCurrentTheme()` | Theme file, colour scheme, size name |

`ThemeInfo`: `FileName`, `ShortFileName`, `ColorScheme`, `SizeName`.

### `VisualStyleContext`

| Member | Description |
| --- | --- |
| `VisualStyleContext()` | Creates the hidden helper window; throws `ThemeException` on failure |
| `VisualStyle Open(string className, string subClass = null)` | Applies the subclass, opens the theme; `null` if not themed |
| `void Dispose()` | Destroys the hidden window |

### `VisualStyle`

| Member | Description |
| --- | --- |
| `bool IsDisposed` | Handle state |
| `bool IsPartDefined(int part, int state)` | `IsThemePartDefined` |
| `bool TryGetInt(part, state, propId, out int)` | `TMT_INT` / `TMT_ENUM` |
| `bool TryGetBool(...)` | `TMT_BOOL` |
| `bool TryGetColor(...)` | `COLORREF` → `System.Drawing.Color` |
| `bool TryGetString(...)` | `TMT_STRING` |
| `bool TryGetFilename(...)` | `TMT_FILENAME` |
| `bool TryGetMargins(...)` | `TMT_MARGINS` |
| `bool TryGetRect(...)` | `TMT_RECT` |
| `bool TryGetPosition(...)` | `TMT_POSITION` |
| `bool TryGetPartSize(part, state, ThemeSize, out Size)` | `GetThemePartSize` |
| `bool TryGetFont(...)` | `LOGFONT` → `ThemeFontInfo` |
| `bool TryGetIntList(...)` | `TMT_INTLIST` (up to 409 values) |
| `bool TryGetStream(...)` | In-memory stream length (Vista+) |
| `ThemePropertyOrigin GetPropertyOrigin(...)` | state / part / class / global |
| `bool TryGetProperty(part, state, ThemePropertyInfo, out ThemePropertyValue)` | Typed read using the database |
| `void Dispose()` | Closes `HTHEME` |

### `ThemeDatabase` and models

| Member | Description |
| --- | --- |
| `IList<ThemeClassInfo> Classes` | 197 class/subclass entries |
| `IList<ThemePropertyInfo> Properties` | 227 `TMT_*` properties |
| `bool TryGetProperty(int id, out ThemePropertyInfo)` | Look up by numeric id |

Models:

- `ThemeClassInfo`: `Name`, `SubClass`, `DisplayName`, `Parts`
- `ThemePartInfo`: `Id`, `Name`, `DisplayName`, `States`
- `ThemeStateInfo`: `Id`, `Name`, `DisplayName`
- `ThemePropertyInfo`: `Type`, `Id`, `Name`, `DisplayName`

All collections are read-only.

### Enumerations

| Enum | Values |
| --- | --- |
| `ThemePropertyType` | `Enum`, `String`, `Int`, `Bool`, `Color`, `Margins`, `Filename`, `Size`, `Position`, `Rect`, `Font`, `IntList`, `DiskStream`, `Stream` |
| `ThemePropertyOrigin` | `NotDefined=0`, `State=1`, `Part=2`, `Class=3`, `Global=4` |
| `ThemeSize` | `Min=0`, `True=1`, `Draw=2` |
| `ThemeBitmapKind` | `Background`, `Glyph` |
| `ThemeTextFormat` | `Left`, `Center`, `Right`, `VerticalCenter`, `WordBreak`, `SingleLine`, `NoPrefix` |

### `ThemePropertyValue`

| Member | Valid for |
| --- | --- |
| `Type` | Always |
| `IntValue`, `EnumSymbol` | `Enum`, `Int` |
| `TextValue` | `String`, `Filename` |
| `BoolValue` | `Bool` |
| `ColorValue` | `Color` |
| `MarginsValue` | `Margins` |
| `RectValue` / `PointValue` / `SizeValue` | `Rect` / `Position` / `Size` |
| `FontValue` | `Font` |
| `IntListValue` | `IntList` |
| `StreamLength` | `Stream`, `DiskStream` |

`ToString()` is a culture-independent debug representation; the GUI uses its own
localized formatter.

### `ThemeRenderer` (static)

| Member | Description |
| --- | --- |
| `TryDrawBackground(Graphics, ...)` | Draw into GDI+ |
| `TryDrawBackground(IntPtr hdc, ...)` | Draw into an existing HDC |
| `TryDrawText(Graphics/IntPtr, ...)` | Draw themed text |
| `TryDrawParentBackground(hwnd, Graphics, rect)` | Parent background for transparent parts |
| `IsBackgroundPartiallyTransparent(...)` | Whether the parent background is needed |
| `Bitmap GetBitmap(VisualStyle, part, state, ThemeBitmapKind)` | Managed copy; caller disposes |
| `TryGetPartSize(...)` | Part size |
| `TryGetStreamLength(...)` | In-memory stream length |

---

## 6. Data conventions

- Part/state ids are the SDK `xxx_P_*` / `xxx_S_*` values.
- Property ids are the numeric `TMT_*` values.
- Colours arrive as `COLORREF` (`0x00BBGGRR`) and are converted to `Color`.
- Sizes, positions and margins are pixels at the process DPI.
- Font height is `LOGFONT.lfHeight` (negative means character height).
- `TryGetStream` returns only the length; the data stays in the theme module.
- `ThemePropertyType.Size` is only valid for the `TS_MIN`/`TS_TRUE`/`TS_DRAW`
  pseudo ids (0–2). `TMT_SIZE` (207) is not a valid `THEMESIZE`.

---

## 7. Drawing and WPF

### WinForms

```csharp
protected override void OnPaint(PaintEventArgs e)
{
    if (_style != null)
        ThemeRenderer.TryDrawBackground(e.Graphics, _style, 1, 1, ClientRectangle);
}
```

Reuse one `VisualStyleContext`; do not create a context inside `OnPaint`.

### WPF

WPF has no HDC, so render through a DIB section:

1. `CreateDIBSection` (32bpp, top-down);
2. `CreateCompatibleDC` + `SelectObject`;
3. `Graphics.FromHdc` to fill the background;
4. `ThemeRenderer.TryDrawBackground(hdc, ...)`;
5. copy the bits into `byte[]`;
6. `BitmapSource.Create(..., PixelFormats.Bgr32, ...)` and `Freeze()`;
7. restore/delete the DIB and DC in `finally`.

See [`src/ThemeExplorer.Wpf/ThemeRenderService.cs`](../src/ThemeExplorer.Wpf/ThemeRenderService.cs)
and the [WPF memory notes](WPF.zh-CN.md).

---

## 8. Memory and ownership

The library never caches handles or images and destroys every native object it
creates.

| Resource | Released by | Notes |
| --- | --- | --- |
| Hidden HWND | `VisualStyleContext.Dispose` | One per context |
| `HTHEME` | `VisualStyle.Dispose` (SafeHandle) | Do not rely on the finalizer |
| GDI+ bitmap | Caller | A copy; independent of the theme lifetime |
| DIB/DC/HBITMAP | `ThemeRenderService` `finally` | Per render |
| `BitmapSource` | GC | Frozen, no thread affinity |

Recommendations:

- Open one theme per selection and dispose it immediately.
- Do not enumerate all 227 properties in `OnPaint`; do it when the selection changes.
- On window close, clear image references and dispose the context.
- If you cache images, bound the cache and clear it when the theme changes.

---

## 9. Performance

- `OpenThemeData` plus the first property read has a fixed cost; open once per selection.
- Reading all properties costs 200+ native calls; keep it on the selection path, not per frame.
- Cache formatted strings if needed; `ThemePropertyValue` is small and safe to keep.
- Rendering nine previews is usually a few milliseconds; avoid rerendering the same
  part/state during scrolling or animation.
- Render once at device resolution for high DPI instead of rescaling every frame.

---

## 10. Compatibility and known differences

- .NET Framework 4.0, C# 6, AnyCPU (verified in a 64-bit process).
- Windows Vista or later. `GetThemeBitmap` / `GetThemeStream` are resolved
  dynamically and degrade to `false` when unavailable.
- Differences from the original C tool:
  - `TMT_SIZE` is no longer passed as a `THEMESIZE` value (see §6).
  - Class/part/state names and the database structure are preserved.
- The `PO_GLOBAL` behaviour of `GetThemePropertyOrigin` is an OS quirk, not a
  library bug.

---

## Full example

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

This enumerates every state and property and is for demonstration only.
