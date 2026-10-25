# Theme Explorer (.NET)

A C# port of [mity/theme-explorer](https://github.com/mity/theme-explorer): a small tool for
exploring the visual styles (themes) provided by `UXTHEME.DLL`, plus a reusable class library
wrapping that API. The repository contains both a WinForms and a WPF front end.

*Chinese version: [README.zh-CN.md](README.zh-CN.md)*

![WinForms UI, Simplified Chinese](docs/screenshots/zh-Hans.png)

![WPF UI, Simplified Chinese](docs/screenshots/wpf-zh-Hans.png)

## Highlights

- **.NET Framework 4.0**, **C# 6.0**, Visual Studio 2015 solution, **AnyCPU**
  (verified running as a 64-bit process).
- **ThemeExplorer.Core** — a documented wrapper around UXTHEME: `SafeHandle`-based lifetime,
  HRESULT checks, strongly typed values, HDC and `Graphics` drawing overloads, and dynamic
  resolution of `GetThemeBitmap` / `GetThemeStream`.
  API reference: [English](docs/API.md) · [中文](docs/API.zh-CN.md).
- **ThemeExplorer.Ui** — shared localization resources, culture handling and property-value
  formatting, so both front ends stay in sync.
- **ThemeExplorer** (WinForms) and **ThemeExplorer.Wpf** — two GUIs with the original layout:
  class/part/state tree, nine part previews, DIB/glyph bitmaps, and a property list that shows
  where each value comes from (state, part, class or global defaults).
- **English + Simplified Chinese** via satellite resources. The language follows the OS by
  default and can be switched at runtime from *View → Language* or with `--lang=zh-Hans`.
  Common class/part/state names are localized in the tree; technical names without a reliable
  translation remain in English.
- **Live theme updates**: the WinForms browser refreshes the current selection when the system
  theme or colorization changes. *View → Automatically update on theme change* is enabled by
  default and can be turned off without changing the selected tree node.
- **WPF memory discipline**: no `HwndHost` per preview, theme handles closed immediately,
  every DIB/DC destroyed in `finally`, no static image caches. The self-test opens/closes
  20 windows and 150 selections and checks GDI/USER/handle counts; see
  [WPF memory notes](docs/WPF.zh-CN.md).
- The database (44 classes, 153 window subclasses, 335 parts, 670 states, 227 `TMT_*`
  properties) is generated from the original `db.c`, so nothing was retyped by hand.

## Related: Win32 themes for WPF

[PresentationFramework.Theme](../PresentationFramework.Theme/README.md) is a companion library that
paints WPF controls with the current Win32 visual style (Button, CheckBox, RadioButton, TextBox) and
shares this `ThemeExplorer.Core` interop layer.
## Requirements

- Windows Vista or later with visual styles enabled (developed and tested on Windows 10/11).
  The binaries target .NET Framework 4.0.
- To build: Visual Studio 2015, or a newer MSBuild plus the .NET Framework 4.0 targeting pack.

## Build

With Visual Studio 2015: open `ThemeExplorer.sln`, choose `Release | Any CPU`, build.

From a command line:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1 -Configuration Release -SelfTest
```

or directly with MSBuild:

```powershell
msbuild ThemeExplorer.sln /p:Configuration=Release /p:Platform="Any CPU"
```

Output:

```
src\ThemeExplorer.App\bin\Release\ThemeExplorer.exe          (WinForms)
src\ThemeExplorer.Wpf\bin\Release\ThemeExplorer.Wpf.exe      (WPF)
...\ThemeExplorer.Core.dll, ThemeExplorer.Ui.dll
...\zh-Hans\ThemeExplorer.Ui.resources.dll
```

## Run

```text
ThemeExplorer.exe     [--lang=en|zh-Hans] [--selftest --out=<directory>]
ThemeExplorer.Wpf.exe [--lang=en|zh-Hans] [--selftest --out=<directory>]
```

- Without `--lang`, the UI follows the operating system language. All Chinese locales map to
  Simplified Chinese (`zh-Hans`); any other language falls back to English.
- `--selftest` runs headless checks (window creation, theme opening, property enumeration,
  language switching) and writes a log plus screenshots to the given directory. The WPF test
  additionally measures handles and memory. `build.ps1` runs both.

## Using the class library

```csharp
using ThemeExplorer.Core;

// One context per UI thread; it owns a hidden helper window used for subclasses.
using (var context = new VisualStyleContext())
using (VisualStyle style = context.Open("Button"))   // returns null if not themed
{
    if (style == null)
        return;

    ThemePropertyInfo property;
    ThemePropertyValue value;
    if (ThemeDatabase.TryGetProperty(3802, out property)   // TMT_FILLCOLOR
        && style.TryGetProperty(1, 1, property, out value))
    {
        Console.WriteLine("{0} = {1} ({2})", property.Name, value,
            style.GetPropertyOrigin(1, 1, property.Id));
    }

    using (Bitmap bitmap = ThemeRenderer.GetBitmap(style, 1, 1, ThemeBitmapKind.Background))
    {
        // bitmap is a managed copy; the theme may be disposed afterwards.
    }
}
```

Key API:

| Type | Purpose |
| --- | --- |
| `VisualStyleContext` | Owns the hidden window; `Open(className, subClass)` returns a `VisualStyle`. |
| `VisualStyle` | Typed `TryGetInt/Bool/Color/String/Margins/Rect/Position/PartSize/Font/IntList/Stream`, `GetPropertyOrigin`, `IsPartDefined`. |
| `ThemeDatabase` | Read-only class/part/state/property catalogue. |
| `ThemeRenderer` | `TryDrawBackground` / `TryDrawText` (Graphics or HDC), `GetBitmap`, `IsBackgroundPartiallyTransparent`. |
| `ThemeSystem` | `IsVisualStylesEnabled`, `IsAppThemed`, `GetCurrentTheme()`. |
| `ThemePropertyValue` | Typed value with `Type`, `IntValue`, `ColorValue`, `MarginsValue`, ... |

All `Try*` methods return `false` instead of throwing when the current theme does not define the
requested part/state/property, which is the normal case while browsing a theme. Full details:
[API reference](docs/API.md).

## Memory and lifetime

- `VisualStyleContext` and `VisualStyle` are `IDisposable`; open a theme per selection and close
  it when the images and property rows have been produced.
- `GetBitmap` returns a managed copy; dispose it after converting to WPF/WinForms.
- The library keeps no static handle or image cache.
- WPF never creates a child HWND per preview; its GDI bridge destroys the DIB/DC in `finally`.
- The WPF self-test (150 selections, 20 window open/close cycles) reports 0 GDI growth,
  USER/handle counts within ±2 and ~4 KB managed-heap growth; the render-only path stays flat.
  Commit/working-set size can fluctuate because of WPF internal caches and is logged, not asserted.
  Details and methodology: [WPF memory notes](docs/WPF.zh-CN.md).

## Differences from the original C implementation

- On Windows 10/11 `GetThemePropertyOrigin` reports `PO_GLOBAL` even for property ids that do not
  exist, so the GUIs hide valueless rows by default. *View → Show all properties* restores the
  raw (unfiltered) 227-row behaviour of the original.
- `TMT_SIZE` is only queried for the `TS_MIN`/`TS_TRUE`/`TS_DRAW` pseudo ids (0–2), which are the
  only values that are valid `THEMESIZE` arguments. The original passed the property id (207)
  straight through.
- DPI-aware layout, resizable property columns, localized type/origin names, and a placeholder in
  the bitmap panels when a part has no DIB.
- No binaries are downloaded from `mctrl.org` (see issue #5 in the original repository); build
  from source or use the GitHub release of the original project.

## Repository layout

```
ThemeExplorer.sln
src/ThemeExplorer.Core/          Interop + models + renderer (class library)
src/ThemeExplorer.Ui/            Shared strings, culture and value formatting
src/ThemeExplorer.App/           WinForms GUI
src/ThemeExplorer.Wpf/           WPF GUI + DIB renderer + memory self-test
tools/Generate-ThemeData.ps1     Regenerates ThemeData.Generated.cs from the original db.c
                                  (expects the original repository next to this one; use -Source otherwise)
tools/Generate-Strings.ps1       Generates Strings.resx / Strings.zh-Hans.resx / Strings.cs
tools/Update-BilingualDocs.ps1   Adds/refreshes the Chinese <para> in XML doc comments
docs/API.md, docs/API.zh-CN.md   Class library reference
docs/WPF.zh-CN.md                WPF rendering and memory verification
docs/screenshots/                Screenshots used by the READMEs
build.ps1                        Command-line build + both self-tests
```

## Localization

UI strings live in `ThemeExplorer.Ui/Strings.resx` (English, neutral) and `Strings.zh-Hans.resx`;
both are generated from one table in `tools/Generate-Strings.ps1` so the two files cannot drift
apart. Theme class/part/state names and `TMT_*` property names stay in English because they are
the identifiers used by the Windows API and MSDN.

To add a language, add the entries to the table, run the generator, and extend the language menu
in both front ends.

The XML documentation comments of `ThemeExplorer.Core` and `ThemeExplorer.Ui` are bilingual:
each English `<summary>`/`<remarks>` is followed by a `<para xml:lang="zh-CN">` Chinese
translation, so both appear in IntelliSense and in the generated `.xml` file. After editing an
English comment, run `tools/Update-BilingualDocs.ps1`; it fails if a translation is missing.

## License

MIT — see [LICENSE.md](LICENSE.md). Original Theme Explorer © 2012 Martin Mitas.
