<#
.SYNOPSIS
    Adds Simplified Chinese <para> blocks to the XML doc comments of the libraries.

.DESCRIPTION
    The English comments in the .cs files stay the source of truth.  This script
    appends a Chinese translation to every <summary> and <remarks> block, using
    the table below.  It is idempotent: existing Chinese paragraphs are replaced.

    If a summary/remarks has no translation the script fails, so a new public
    member cannot silently ship with English-only documentation.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\tools\Update-BilingualDocs.ps1
#>
[CmdletBinding()]
param(
    [string[]] $Directories = @(
        (Join-Path $PSScriptRoot '..\src\ThemeExplorer.Core'),
        (Join-Path $PSScriptRoot '..\src\ThemeExplorer.Ui')
    )
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------------------
# English -> Simplified Chinese translations.
# ---------------------------------------------------------------------------
$translations = @(
    @{ En = 'Resolves the optional UXTHEME.DLL entry points at run time. They exist on Windows Vista and later but not on Windows XP, and the original tool was careful to keep running there.'; Zh = '在运行时解析 UXTHEME.DLL 的可选入口点。这些入口点存在于 Windows Vista 及更高版本，Windows XP 上没有；原版工具为此做了兼容处理。' }

    @{ En = 'Kind of data stored in a visual style property (the <c>TMT_*</c> types defined by the Windows theming API).'; Zh = '视觉样式属性中存储的数据类型（Windows 主题 API 定义的 <c>TMT_*</c> 类型）。' }
    @{ En = 'A value from one of the documented enumerations (for example <c>BT_BORDERFILL</c>).'; Zh = '文档中定义的某个枚举值（例如 <c>BT_BORDERFILL</c>）。' }
    @{ En = 'A Unicode string.'; Zh = 'Unicode 字符串。' }
    @{ En = 'A signed integer.'; Zh = '有符号整数。' }
    @{ En = 'A boolean value.'; Zh = '布尔值。' }
    @{ En = 'A <c>COLORREF</c> colour.'; Zh = '<c>COLORREF</c> 颜色。' }
    @{ En = 'Four margin widths.'; Zh = '四个边距宽度。' }
    @{ En = 'A file name, usually relative to the theme file.'; Zh = '文件名，通常相对于主题文件。' }
    @{ En = 'A width/height pair.'; Zh = '宽/高对。' }
    @{ En = 'An x/y position.'; Zh = 'x/y 坐标。' }
    @{ En = 'A rectangle.'; Zh = '矩形。' }
    @{ En = 'A <c>LOGFONT</c> description.'; Zh = '<c>LOGFONT</c> 字体描述。' }
    @{ En = 'A list of integers.'; Zh = '整数列表。' }
    @{ En = 'A stream stored in the theme file on disk (Windows Vista and later).'; Zh = '存储在磁盘主题文件中的流（Windows Vista 及更高版本）。' }
    @{ En = 'An in-memory stream (Windows Vista and later).'; Zh = '内存中的流（Windows Vista 及更高版本）。' }
    @{ En = 'Where a property value was found while resolving it (the <c>PROPERTYORIGIN</c> enumeration of the Windows theming API).'; Zh = '解析属性值时该值的来源（Windows 主题 API 的 <c>PROPERTYORIGIN</c> 枚举）。' }
    @{ En = 'The property is not defined for the requested part and state.'; Zh = '该属性未在所请求的部件和状态上定义。' }
    @{ En = 'Defined for the exact state.'; Zh = '在精确匹配的状态上定义。' }
    @{ En = 'Inherited from the part defaults.'; Zh = '继承自部件默认值。' }
    @{ En = 'Inherited from the class defaults.'; Zh = '继承自类默认值。' }
    @{ En = 'Inherited from the global (theme-wide) defaults.'; Zh = '继承自全局（主题范围）默认值。' }
    @{ En = 'The <c>THEMESIZE</c> enumeration used by <c>GetThemePartSize</c>.'; Zh = '<c>GetThemePartSize</c> 使用的 <c>THEMESIZE</c> 枚举。' }
    @{ En = 'Smallest size the part can be drawn at.'; Zh = '部件可绘制的最小尺寸。' }
    @{ En = 'Native size of the part.'; Zh = '部件的原始尺寸。' }
    @{ En = 'Size used when the part is drawn.'; Zh = '绘制部件时使用的尺寸。' }
    @{ En = 'Selects which bitmap of a theme part is requested.'; Zh = '选择要获取主题部件的哪个位图。' }
    @{ En = 'The part background bitmap (<c>TMT_DIBDATA</c>).'; Zh = '部件背景位图（<c>TMT_DIBDATA</c>）。' }
    @{ En = 'The glyph bitmap (<c>TMT_GLYPHDIBDATA</c>).'; Zh = '字形位图（<c>TMT_GLYPHDIBDATA</c>）。' }
    @{ En = 'Text formatting flags for <see cref="ThemeRenderer"/>; the values match the Win32 <c>DT_*</c> flags understood by <c>DrawThemeText</c>.'; Zh = '用于 <see cref="ThemeRenderer"/> 的文本格式标志；取值与 <c>DrawThemeText</c> 理解的 Win32 <c>DT_*</c> 标志一致。' }
    @{ En = 'Left aligned text.'; Zh = '左对齐文本。' }
    @{ En = 'Text is centred horizontally.'; Zh = '文本水平居中。' }
    @{ En = 'Text is right aligned.'; Zh = '文本右对齐。' }
    @{ En = 'Text is centred vertically.'; Zh = '文本垂直居中。' }
    @{ En = 'Break words when wrapping.'; Zh = '换行时按单词断行。' }
    @{ En = 'Use a single line.'; Zh = '单行显示。' }
    @{ En = 'Do not clip the text.'; Zh = '不裁剪文本。' }
    @{ En = 'Flags for the optional <c>GetThemeBitmap</c> call.'; Zh = '可选 <c>GetThemeBitmap</c> 调用使用的标志。' }
    @{ En = 'Return the bitmap owned by the theme (do not delete it).'; Zh = '返回主题拥有的位图（不要删除它）。' }
    @{ En = 'Return a copy of the bitmap (the caller owns it).'; Zh = '返回位图的副本（由调用方负责释放）。' }

    @{ En = 'One state of a theme part (for example "Hot" or "Pressed").'; Zh = '主题部件的一个状态（例如 "Hot" 或 "Pressed"）。' }
    @{ En = 'Numeric state id used by the native API (the <c>xxx_S_*</c> constants).'; Zh = '本机 API 使用的数字状态 id（<c>xxx_S_*</c> 常量）。' }
    @{ En = 'Raw state constant name from the Windows SDK headers.'; Zh = 'Windows SDK 头文件中的原始状态常量名。' }
    @{ En = 'Short human readable name.'; Zh = '简短的可读名称。' }
    @{ En = 'One part of a theme class (for example the "PushButton" part of "Button").'; Zh = '主题类的一个部件（例如 "Button" 的 "PushButton" 部件）。' }
    @{ En = 'Numeric part id used by the native API (the <c>xxx_P_*</c> constants).'; Zh = '本机 API 使用的数字部件 id（<c>xxx_P_*</c> 常量）。' }
    @{ En = 'Raw part constant name from the Windows SDK headers.'; Zh = 'Windows SDK 头文件中的原始部件常量名。' }
    @{ En = 'States defined for this part. The collection is never <c>null</c>.'; Zh = '此部件定义的状态。集合永远不会是 <c>null</c>。' }
    @{ En = 'A visual style class, optionally restricted to a window subclass (for example "Toolbar" with the "ExplorerBar" subclass).'; Zh = '视觉样式类，可选地限定到某个窗口子类（例如 "Toolbar" 的 "ExplorerBar" 子类）。' }
    @{ En = 'Class name passed to <c>OpenThemeData</c>.'; Zh = '传给 <c>OpenThemeData</c> 的类名。' }
    @{ En = 'Window subclass passed to <c>SetWindowTheme</c>, or <c>null</c>.'; Zh = '传给 <c>SetWindowTheme</c> 的窗口子类，或 <c>null</c>。' }
    @{ En = 'Name shown in the class tree.'; Zh = '在类树中显示的名称。' }
    @{ En = 'Parts known for this class. The collection is never <c>null</c>.'; Zh = '此类已知的部件。集合永远不会是 <c>null</c>。' }
    @{ En = 'A <c>TMT_*</c> property that can be queried for any part/state pair.'; Zh = '可按任意部件/状态查询的 <c>TMT_*</c> 属性。' }
    @{ En = 'Data type of the property.'; Zh = '属性的数据类型。' }
    @{ En = 'Numeric property id (the value of the <c>TMT_*</c> constant).'; Zh = '数字属性 id（<c>TMT_*</c> 常量的值）。' }
    @{ En = 'The <c>TMT_*</c> constant name.'; Zh = '<c>TMT_*</c> 常量名。' }

    @{ En = 'Raw P/Invoke declarations for UXTHEME.DLL and the few Win32 functions the wrapper needs. Everything here is an implementation detail of the <see cref="ThemeExplorer.Core"/> assembly.'; Zh = 'UXTHEME.DLL 及封装所需少量 Win32 函数的原始 P/Invoke 声明。这里的全部内容都属于 <see cref="ThemeExplorer.Core"/> 程序集的实现细节。' }
    @{ En = 'Safe handle owning an <c>HTHEME</c> returned by <c>OpenThemeData</c>.'; Zh = '拥有 <c>OpenThemeData</c> 返回的 <c>HTHEME</c> 的安全句柄。' }

    @{ En = 'Thrown when a UXTHEME.DLL function reports a failure HRESULT.'; Zh = '当 UXTHEME.DLL 函数返回失败的 HRESULT 时抛出。' }
    @{ En = 'Creates an exception for the given operation and HRESULT.'; Zh = '为指定操作和 HRESULT 创建异常。' }
    @{ En = 'Creates an exception for the given operation, HRESULT and inner exception.'; Zh = '为指定操作、HRESULT 和内部异常创建异常。' }
    @{ En = 'Name of the native API that failed.'; Zh = '失败的本机 API 名称。' }
    @{ En = 'The HRESULT returned by the native API.'; Zh = '本机 API 返回的 HRESULT。' }
    @{ En = 'The COM exception corresponding to <see cref="NativeHResult"/>, if any.'; Zh = '与 <see cref="NativeHResult"/> 对应的 COM 异常（如果有）。' }

    @{ En = 'Symbolic names for the values of <c>TMT_ENUM</c> properties.'; Zh = '<c>TMT_ENUM</c> 属性取值的符号名称。' }
    @{ En = 'Reads a <see cref="ThemePropertyValue"/> for a given property definition.'; Zh = '按给定属性定义读取 <see cref="ThemePropertyValue"/>。' }

    @{ En = 'A value read from a theme property. <see cref="Type"/> indicates which of the typed values is meaningful.'; Zh = '从主题属性读取的值。<see cref="Type"/> 指示哪个强类型字段有效。' }
    @{ En = 'Data type of the value.'; Zh = '值的数据类型。' }
    @{ En = 'Integer value (also used for enumeration values).'; Zh = '整数值（也用于枚举值）。' }
    @{ En = 'Symbolic name of an enumeration value, when known.'; Zh = '枚举值的符号名称（如果已知）。' }
    @{ En = 'String value (also used for file names).'; Zh = '字符串值（也用于文件名）。' }
    @{ En = 'Boolean value.'; Zh = '布尔值。' }
    @{ En = 'Colour value.'; Zh = '颜色值。' }
    @{ En = 'Margin value.'; Zh = '边距值。' }
    @{ En = 'Rectangle value.'; Zh = '矩形值。' }
    @{ En = 'Position value.'; Zh = '位置值。' }
    @{ En = 'Size value.'; Zh = '尺寸值。' }
    @{ En = 'Font value.'; Zh = '字体值。' }
    @{ En = 'Integer list value.'; Zh = '整数列表值。' }
    @{ En = 'Length, in bytes, of a stream value.'; Zh = '流值的长度（字节）。' }
    @{ En = 'Returns a culture-independent textual representation, mainly useful for debugging. The GUI uses its own localised formatter.'; Zh = '返回与区域文化无关的文本表示，主要用于调试。GUI 使用自己的本地化格式化器。' }

    @{ En = 'Drawing helpers built on top of <see cref="VisualStyle"/>.'; Zh = '构建在 <see cref="VisualStyle"/> 之上的绘制辅助方法。' }
    @{ En = 'The <c>Try*</c> methods return <c>false</c> (instead of throwing) when the current theme does not support the requested part; this keeps custom painting code simple.'; Zh = '当当前主题不支持所请求的部件时，<c>Try*</c> 方法返回 <c>false</c> 而不是抛出异常；这样自定义绘制代码可以保持简单。' }
    @{ En = 'Draws the background of a part/state pair into <paramref name="bounds"/>.'; Zh = '将部件/状态的背景绘制到 <paramref name="bounds"/>。' }
    @{ En = 'Draws the background of a part/state pair into the given device context. This overload is useful when the caller already owns an HDC, for example when rendering into a DIB section for WPF.'; Zh = '将部件/状态的背景绘制到给定的设备上下文。当调用方已经拥有 HDC（例如为 WPF 渲染到 DIB 区域）时，此重载很有用。' }
    @{ En = 'Draws the themed text of a part/state pair into <paramref name="bounds"/>.'; Zh = '将部件/状态的主题文字绘制到 <paramref name="bounds"/>。' }
    @{ En = 'Draws the themed text of a part/state pair into the given device context.'; Zh = '将部件/状态的主题文字绘制到给定的设备上下文。' }
    @{ En = 'Draws the parent window background; used for parts that are partially transparent.'; Zh = '绘制父窗口背景；用于部分透明的部件。' }
    @{ En = 'Returns whether the background of a part/state pair is partially transparent.'; Zh = '返回部件/状态的背景是否部分透明。' }
    @{ En = 'Returns a managed copy of the background or glyph bitmap of a part, or <c>null</c> when the theme does not provide one.'; Zh = '返回部件背景或字形位图的托管副本；主题未提供时返回 <c>null</c>。' }
    @{ En = 'Queries the size of a part/state pair.'; Zh = '查询部件/状态的尺寸。' }
    @{ En = 'Queries the length of an in-memory stream property.'; Zh = '查询内存流属性的长度。' }

    @{ En = 'Process-wide information about the visual styles subsystem.'; Zh = '视觉样式子系统的进程级信息。' }
    @{ En = 'Whether visual styles are active for this session and enabled for the current process.'; Zh = '当前会话是否启用了视觉样式，并且当前进程是否启用了主题。' }
    @{ En = 'Whether a visual style is active in the current session (this is also true for processes that did not opt in to common controls v6).'; Zh = '当前会话是否有视觉样式处于活动状态（对于未启用 comctl32 v6 的进程也为 true）。' }
    @{ En = 'Whether the current process opted in to themed common controls.'; Zh = '当前进程是否启用了主题化公共控件。' }
    @{ En = 'Returns the theme file, colour scheme and size currently in use.'; Zh = '返回当前使用的主题文件、配色方案和尺寸名称。' }

    @{ En = 'The four margin widths of a theme part.'; Zh = '主题部件的四个边距宽度。' }
    @{ En = 'Creates a margin value.'; Zh = '创建边距值。' }
    @{ En = 'Left margin, in pixels.'; Zh = '左边距（像素）。' }
    @{ En = 'Right margin, in pixels.'; Zh = '右边距（像素）。' }
    @{ En = 'Top margin, in pixels.'; Zh = '上边距（像素）。' }
    @{ En = 'Bottom margin, in pixels.'; Zh = '下边距（像素）。' }
    @{ En = 'A font as reported by <c>GetThemeFont</c>.'; Zh = '由 <c>GetThemeFont</c> 返回的字体。' }
    @{ En = 'Typeface name, for example "Segoe UI".'; Zh = '字体名称，例如 "Segoe UI"。' }
    @{ En = 'Character height, in logical units.'; Zh = '字符高度（逻辑单位）。' }
    @{ En = 'Weight (400 is normal, 700 is bold).'; Zh = '字重（400 为常规，700 为粗体）。' }
    @{ En = 'Whether the font is italic.'; Zh = '字体是否斜体。' }
    @{ En = 'Whether the font is underlined.'; Zh = '字体是否带下划线。' }
    @{ En = 'Creates a <see cref="Font"/> that approximates this theme font.'; Zh = '创建接近此主题字体的 <see cref="Font"/>。' }
    @{ En = 'Information about the visual style file currently in use.'; Zh = '当前使用的视觉样式文件信息。' }
    @{ En = 'Full path of the <c>.msstyles</c> file, or an empty string.'; Zh = '<c>.msstyles</c> 文件的完整路径，或空字符串。' }
    @{ En = 'Colour scheme name (for example "NormalColor").'; Zh = '配色方案名称（例如 "NormalColor"）。' }
    @{ En = 'Size name (for example "NormalSize").'; Zh = '尺寸名称（例如 "NormalSize"）。' }
    @{ En = 'File name without the directory, for display purposes.'; Zh = '不含目录的文件名，用于显示。' }
    @{ En = 'Read-only access to the visual style database (classes, parts, properties) that ships with the library.'; Zh = '对随库发布的视觉样式数据库（类、部件、属性）的只读访问。' }
    @{ En = 'All known visual style classes, ordered by name. Window subclasses are listed as separate entries; see <see cref="ThemeClassInfo.SubClass"/>.'; Zh = '所有已知的视觉样式类，按名称排序。窗口子类会作为单独条目列出；参见 <see cref="ThemeClassInfo.SubClass"/>。' }
    @{ En = 'All <c>TMT_*</c> properties that <see cref="VisualStyle"/> can read via its TryGetProperty overloads understands.'; Zh = '所有可通过 <see cref="VisualStyle"/> 的 TryGetProperty 重载读取的 <c>TMT_*</c> 属性。' }
    @{ En = 'Tries to resolve a numeric property id to its definition.'; Zh = '尝试把数字属性 id 解析为属性定义。' }

    @{ En = 'A handle to an open visual style (<c>HTHEME</c>) together with strongly typed accessors for its parts, states and properties.'; Zh = '已打开视觉样式（<c>HTHEME</c>）的句柄，以及针对其部件、状态和属性的强类型访问方法。' }
    @{ En = 'Instances are created by <see cref="VisualStyleContext.Open"/>. The object owns the native handle and must be disposed when it is no longer needed.'; Zh = '实例由 <see cref="VisualStyleContext.Open"/> 创建。该对象拥有本机句柄，不再需要时必须释放。' }
    @{ En = '<c>TMT_DIBDATA</c>: the background bitmap of a part.'; Zh = '<c>TMT_DIBDATA</c>：部件的背景位图。' }
    @{ En = '<c>TMT_GLYPHDIBDATA</c>: the glyph bitmap of a part.'; Zh = '<c>TMT_GLYPHDIBDATA</c>：部件的字形位图。' }
    @{ En = 'Whether the underlying native handle is still usable.'; Zh = '底层本机句柄是否仍可用。' }
    @{ En = 'Returns whether the given part/state pair is defined by the style.'; Zh = '返回样式是否定义了给定的部件/状态。' }
    @{ En = 'Tries to read an integer property.'; Zh = '尝试读取整数属性。' }
    @{ En = 'Tries to read a boolean property.'; Zh = '尝试读取布尔属性。' }
    @{ En = 'Tries to read a colour property.'; Zh = '尝试读取颜色属性。' }
    @{ En = 'Tries to read a string property.'; Zh = '尝试读取字符串属性。' }
    @{ En = 'Tries to read a file name property.'; Zh = '尝试读取文件名属性。' }
    @{ En = 'Tries to read a margins property.'; Zh = '尝试读取边距属性。' }
    @{ En = 'Tries to read a rectangle property.'; Zh = '尝试读取矩形属性。' }
    @{ En = 'Tries to read a position property.'; Zh = '尝试读取位置属性。' }
    @{ En = 'Tries to read a size property.'; Zh = '尝试读取尺寸属性。' }
    @{ En = 'Tries to read a font property. Returns a description rather than a <see cref="Font"/> so that no GDI object has to be owned by the caller.'; Zh = '尝试读取字体属性。返回描述对象而不是 <see cref="Font"/>，这样调用方无需拥有 GDI 对象。' }
    @{ En = 'Tries to read an integer list property.'; Zh = '尝试读取整数列表属性。' }
    @{ En = 'Tries to read an in-memory stream property (Windows Vista and later).'; Zh = '尝试读取内存流属性（Windows Vista 及更高版本）。' }
    @{ En = 'Tries to read a stream stored inside the current theme file (Windows Vista and later).'; Zh = '尝试读取当前主题文件中存储的流（Windows Vista 及更高版本）。' }
    @{ En = 'Returns where the value of a property comes from (state, part, class or global defaults).'; Zh = '返回属性值的来源（状态、部件、类或全局默认值）。' }
    @{ En = 'Tries to read the value of a property defined by <see cref="ThemeDatabase"/>.'; Zh = '尝试读取由 <see cref="ThemeDatabase"/> 定义的属性值。' }
    @{ En = 'Releases the native theme handle.'; Zh = '释放本机主题句柄。' }
    @{ En = 'Opens a visual style for the given class without creating a helper window. Returns <c>null</c> when the class is not defined by the current visual style.'; Zh = '在不创建辅助窗口的情况下为指定类打开视觉样式。当前视觉样式未定义该类时返回 <c>null</c>。' }

    @{ En = 'Owns the hidden helper window that UXTHEME needs in order to apply a window subclass before opening a theme. Create one context per UI thread and reuse it for all <see cref="VisualStyle"/> instances.'; Zh = '拥有隐藏辅助窗口，UXTHEME 需要它来在打开主题前应用窗口子类。每个 UI 线程创建一个上下文，并对所有 <see cref="VisualStyle"/> 实例复用它。' }
    @{ En = 'The context is not thread safe and is meant to be used on the UI thread.'; Zh = '上下文不是线程安全的，应在 UI 线程上使用。' }
    @{ En = 'Creates the context and the hidden helper window.'; Zh = '创建上下文和隐藏的辅助窗口。' }
    @{ En = 'Opens a visual style for the given class. Returns <c>null</c> when the class is not themed by the current visual style.'; Zh = '为给定类打开视觉样式。当当前视觉样式未定义该类时返回 <c>null</c>。' }
    @{ En = 'Destroys the hidden helper window.'; Zh = '销毁隐藏的辅助窗口。' }

    @{ En = 'Turns typed theme values into localised display strings.'; Zh = '把强类型的主题值转换为本地化显示字符串。' }
    @{ En = 'Keeps track of the UI language. English is the neutral resource language; Simplified Chinese lives in the zh-Hans satellite assembly.'; Zh = '跟踪界面语言。英语是中性资源语言；简体中文位于 zh-Hans 卫星程序集。' }
    @{ En = 'Applies the language selected on the command line (<c>--lang=en</c> or <c>--lang=zh-Hans</c>), otherwise follows the operating system. All Chinese locales map to zh-Hans because that is the only translation we ship.'; Zh = '应用命令行选择的语言（<c>--lang=en</c> 或 <c>--lang=zh-Hans</c>），否则跟随操作系统。所有中文区域都映射到 zh-Hans，因为这是我们唯一提供的翻译。' }

    @{ En = 'Returns whether two margin values are equal.'; Zh = '返回两个边距值是否相等。' }
    @{ En = 'Returns whether this value equals another object.'; Zh = '返回此值是否等于另一个对象。' }
    @{ En = 'Returns a hash code for the margin value.'; Zh = '返回边距值的哈希代码。' }
    @{ En = 'Returns a textual representation of the margins.'; Zh = '返回边距的文本表示。' }
    @{ En = 'Returns a textual representation of the font.'; Zh = '返回字体的文本表示。' }
)

$map = @{}
foreach ($translation in $translations) {
    $map[$translation.En] = $translation.Zh
}

$pattern = '(?m)^(?<indent>[ \t]*)///[ \t]*<(?<tag>summary|remarks)>(?<body>.*?)</\k<tag>>'
$options = [Text.RegularExpressions.RegexOptions]::Singleline

$evaluator = [Text.RegularExpressions.MatchEvaluator] {
    param([Text.RegularExpressions.Match] $match)

    $body = $match.Groups['body'].Value
    $body = [regex]::Replace($body, '<para xml:lang="zh-CN">.*?</para>', '')
    $body = [regex]::Replace($body, '(?m)^\s*///[ \t]?', '')
    $body = ($body -replace '\s+', ' ').Trim()

    $chinese = $map[$body]
    if ([string]::IsNullOrWhiteSpace($chinese)) {
        throw "Missing Simplified Chinese translation for: $body"
    }

    $indent = $match.Groups['indent'].Value
    $tag = $match.Groups['tag'].Value
    $newLine = "`n"

    return $indent + '/// <' + $tag + '>' + $newLine +
        $indent + '/// ' + $body + $newLine +
        $indent + '/// <para xml:lang="zh-CN">' + $chinese + '</para>' + $newLine +
        $indent + '/// </' + $tag + '>'
}

$changedFiles = 0
$changedBlocks = 0

foreach ($directory in $Directories) {
    if (-not (Test-Path $directory)) {
        throw "Directory not found: $directory"
    }

    $files = Get-ChildItem $directory -Recurse -Filter *.cs | Where-Object {
        $_.FullName -notmatch '\\obj\\|\\bin\\' -and
        $_.Name -notlike '*Generated*' -and
        $_.Name -ne 'Strings.cs'
    }

    foreach ($file in $files) {
        $text = [IO.File]::ReadAllText($file.FullName)
        $matchCount = [regex]::Matches($text, $pattern, $options).Count
        if ($matchCount -eq 0) {
            continue
        }

        $updated = [regex]::Replace($text, $pattern, $evaluator, $options)

        if ($updated -ne $text) {
            [IO.File]::WriteAllText($file.FullName, $updated, (New-Object Text.UTF8Encoding($false)))
            $changedFiles++
            $changedBlocks += $matchCount
            Write-Host ("Updated {0} ({1} blocks)" -f $file.FullName, $matchCount)
        }
    }
}

Write-Host ("Done. Files: {0}, blocks: {1}" -f $changedFiles, $changedBlocks)
