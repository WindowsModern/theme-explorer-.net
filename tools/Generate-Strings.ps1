<#
.SYNOPSIS
    Generates the English and Simplified Chinese resources plus the strongly
    typed Strings accessor used by the GUI.

.DESCRIPTION
    Keeping the translations in one table avoids the classic problem of a key
    existing in one .resx file but not in the other.
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$uiDir = Join-Path $root 'src\ThemeExplorer.Ui'

# ---------------------------------------------------------------------------
# The single translation table.
# ---------------------------------------------------------------------------
$strings = @(
    @{ Name = 'AppTitle';               En = 'Theme Explorer';                                   Zh = '主题浏览器' }
    @{ Name = 'MenuFile';               En = '&File';                                            Zh = '文件(&F)' }
    @{ Name = 'MenuFileExit';           En = 'E&xit';                                            Zh = '退出(&X)' }
    @{ Name = 'MenuView';               En = '&View';                                            Zh = '视图(&V)' }
    @{ Name = 'MenuViewShowAll';        En = 'Show all properties';                              Zh = '显示所有属性' }
    @{ Name = 'MenuViewAutoUpdate';     En = 'Automatically update on theme change';            Zh = '主题更改时自动更新' }
    @{ Name = 'MenuViewLanguage';       En = '&Language';                                        Zh = '语言(&L)' }
    @{ Name = 'MenuLanguageEnglish';    En = 'English';                                          Zh = 'English' }
    @{ Name = 'MenuLanguageChinese';    En = '简体中文';                                          Zh = '简体中文' }
    @{ Name = 'MenuHelp';               En = '&Help';                                            Zh = '帮助(&H)' }
    @{ Name = 'MenuHelpAbout';          En = '&About Theme Explorer...';                         Zh = '关于主题浏览器(&A)...' }
    @{ Name = 'MenuEditCopySelected';   En = 'Copy selected';                                    Zh = '复制选中项' }
    @{ Name = 'MenuEditCopyAll';        En = 'Copy all';                                         Zh = '复制全部' }
    @{ Name = 'CopyEmpty';              En = 'No property values to copy.';                      Zh = '没有可复制的属性值。' }
    @{ Name = 'AboutTitle';             En = 'About Theme Explorer';                             Zh = '关于主题浏览器' }
    @{ Name = 'AboutText';              En = "Theme Explorer 1.0`n`nA tool for exploring visual styles (themes) on Windows, as provided by UXTHEME.DLL.`n`nBased on the original C implementation by Martin Mitas (mity/theme-explorer).`nC# port: .NET Framework 4.0 / C# 6, AnyCPU.`n`nReleased under the MIT License."; Zh = "主题浏览器 1.0`n`n用于浏览和检查 Windows 视觉样式（主题）的工具，数据来自 UXTHEME.DLL。`n`n基于 Martin Mitas 的原始 C 实现（mity/theme-explorer）。`nC# 移植版本：.NET Framework 4.0 / C# 6，AnyCPU。`n`n遵循 MIT 许可证发布。" }
    @{ Name = 'LabelClass';             En = 'Class:';                                           Zh = '类：' }
    @{ Name = 'LabelPart';              En = 'Part:';                                            Zh = '部件：' }
    @{ Name = 'LabelState';             En = 'State:';                                           Zh = '状态：' }
    @{ Name = 'ColumnType';             En = 'Type';                                             Zh = '类型' }
    @{ Name = 'ColumnId';               En = 'ID';                                               Zh = 'ID' }
    @{ Name = 'ColumnProperty';         En = 'Property';                                         Zh = '属性' }
    @{ Name = 'ColumnValue';            En = 'Value';                                            Zh = '值' }
    @{ Name = 'ColumnOrigin';           En = 'Origin';                                           Zh = '来源' }
    @{ Name = 'NoBitmap';               En = 'no bitmap';                                     Zh = '无位图' }
    @{ Name = 'StatusReady';            En = 'Ready';                                            Zh = '就绪' }
    @{ Name = 'StatusTheme';            En = 'Theme: {0}';                                       Zh = '主题：{0}' }
    @{ Name = 'StatusThemeInactive';    En = 'Visual styles are not enabled.';                    Zh = '未启用视觉样式。' }
    @{ Name = 'StatusNoTheme';          En = 'This class is not defined by the current visual style.'; Zh = '当前视觉样式未定义此类。' }
    @{ Name = 'TooltipBackgroundBitmap'; En = 'Background bitmap (TMT_DIBDATA)';                  Zh = '背景位图（TMT_DIBDATA）' }
    @{ Name = 'TooltipGlyphBitmap';     En = 'Glyph bitmap (TMT_GLYPHDIBDATA)';                   Zh = '字形位图（TMT_GLYPHDIBDATA）' }
    @{ Name = 'SampleText';             En = 'Foo Bar';                                          Zh = '示例文本' }
    @{ Name = 'ErrorTitle';             En = 'Theme Explorer';                                   Zh = '主题浏览器' }
    @{ Name = 'ErrorUnexpected';         En = "An unexpected error occurred.

{0}"; Zh = "发生意外错误。

{0}" }
    @{ Name = 'ErrorOpenTheme';         En = "Failed to open the visual style for '{0}'.`n`n{1}"; Zh = "无法为「{0}」打开视觉样式。`n`n{1}" }
    @{ Name = 'PartTitle';              En = '[{0}] {1}';                                        Zh = '[{0}] {1}' }
    @{ Name = 'StateTitle';             En = '[{0}] {1}';                                        Zh = '[{0}] {1}' }

    @{ Name = 'OriginState';            En = 'state';                                            Zh = '状态' }
    @{ Name = 'OriginPart';             En = 'part';                                             Zh = '部件' }
    @{ Name = 'OriginClass';            En = 'class';                                            Zh = '类' }
    @{ Name = 'OriginGlobal';           En = 'global';                                           Zh = '全局' }
    @{ Name = 'OriginUnknown';          En = '[unknown]';                                        Zh = '[未知]' }

    @{ Name = 'TypeEnum';               En = 'Enum';                                             Zh = '枚举' }
    @{ Name = 'TypeString';             En = 'String';                                           Zh = '字符串' }
    @{ Name = 'TypeInt';                En = 'Int';                                              Zh = '整数' }
    @{ Name = 'TypeBool';               En = 'Bool';                                             Zh = '布尔' }
    @{ Name = 'TypeColor';              En = 'Color';                                            Zh = '颜色' }
    @{ Name = 'TypeMargins';            En = 'Margins';                                          Zh = '边距' }
    @{ Name = 'TypeFilename';           En = 'Filename';                                         Zh = '文件名' }
    @{ Name = 'TypeSize';               En = 'Size';                                             Zh = '尺寸' }
    @{ Name = 'TypePosition';           En = 'Position';                                          Zh = '位置' }
    @{ Name = 'TypeRect';               En = 'Rect';                                             Zh = '矩形' }
    @{ Name = 'TypeFont';               En = 'Font';                                             Zh = '字体' }
    @{ Name = 'TypeIntList';            En = 'IntList';                                          Zh = '整数列表' }
    @{ Name = 'TypeDiskStream';         En = 'DiskStream';                                       Zh = '磁盘流' }
    @{ Name = 'TypeStream';             En = 'Stream';                                           Zh = '流' }

    @{ Name = 'ValueTrue';              En = 'True';                                             Zh = '是' }
    @{ Name = 'ValueFalse';             En = 'False';                                            Zh = '否' }

    @{ Name = 'FormatInt';              En = '{0} (0x{1:x})';                                    Zh = '{0} (0x{1:x})' }
    @{ Name = 'FormatColor';            En = '{0},{1},{2} (#{3:x6})';                            Zh = '{0},{1},{2} (#{3:x6})' }
    @{ Name = 'FormatMargins';          En = '{0},{1} (horz), {2},{3} (vert)';                   Zh = '{0},{1}（水平），{2},{3}（垂直）' }
    @{ Name = 'FormatSize';             En = '{0}x{1}';                                          Zh = '{0}×{1}' }
    @{ Name = 'FormatPosition';         En = '{0},{1}';                                          Zh = '{0},{1}' }
    @{ Name = 'FormatRect';             En = '{0},{1} - {2},{3} ({4}x{5})';                      Zh = '{0},{1} - {2},{3}（{4}×{5}）' }
    @{ Name = 'FormatStream';           En = '<stream {0} bytes>';                               Zh = '<流 {0} 字节>' }
    @{ Name = 'FormatDiskStream';       En = '<disk stream {0} bytes>';                          Zh = '<磁盘流 {0} 字节>' }
    @{ Name = 'IntListSeparator';       En = ', ';                                               Zh = '、' }

    @{ Name = 'FontWeightThin';         En = 'thin';                                             Zh = '极细' }
    @{ Name = 'FontWeightExtraLight';   En = 'extralight';                                       Zh = '超细' }
    @{ Name = 'FontWeightLight';        En = 'light';                                            Zh = '细体' }
    @{ Name = 'FontWeightRegular';      En = 'regular';                                          Zh = '常规' }
    @{ Name = 'FontWeightMedium';       En = 'medium';                                           Zh = '中等' }
    @{ Name = 'FontWeightSemibold';     En = 'semibold';                                         Zh = '半粗' }
    @{ Name = 'FontWeightBold';         En = 'bold';                                             Zh = '粗体' }
    @{ Name = 'FontWeightExtraBold';    En = 'extrabold';                                        Zh = '特粗' }
    @{ Name = 'FontWeightHeavy';        En = 'heavy';                                            Zh = '黑体' }
    @{ Name = 'FontWeightUnknown';      En = '??';                                               Zh = '??' }
    @{ Name = 'FontItalic';             En = 'italic';                                           Zh = '斜体' }
    @{ Name = 'FontUnderline';          En = 'underlined';                                       Zh = '下划线' }
)

$resHeaders = @(
    @{ Name = 'resmimetype'; Value = 'text/microsoft-resx' }
    @{ Name = 'version';     Value = '2.0' }
    @{ Name = 'reader';      Value = 'System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089' }
    @{ Name = 'writer';      Value = 'System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089' }
)

function Write-ResxFile {
    param([string] $Path, [string] $ValueProperty)

    $settings = New-Object System.Xml.XmlWriterSettings
    $settings.Indent = $true
    $settings.IndentChars = '  '
    $settings.Encoding = New-Object System.Text.UTF8Encoding($false)

    $writer = [System.Xml.XmlWriter]::Create($Path, $settings)
    try {
        $writer.WriteStartDocument()
        $writer.WriteStartElement('root')

        foreach ($header in $resHeaders) {
            $writer.WriteStartElement('resheader')
            $writer.WriteAttributeString('name', $header.Name)
            $writer.WriteElementString('value', $header.Value)
            $writer.WriteEndElement()
        }

        foreach ($entry in $strings) {
            $writer.WriteStartElement('data')
            $writer.WriteAttributeString('name', $entry.Name)
            $writer.WriteAttributeString('xml', 'space', 'http://www.w3.org/XML/1998/namespace', 'preserve')
            $writer.WriteElementString('value', $entry.$ValueProperty)
            $writer.WriteEndElement()
        }

        $writer.WriteEndElement()
        $writer.WriteEndDocument()
    }
    finally {
        $writer.Close()
    }
}

Write-ResxFile -Path (Join-Path $uiDir 'Strings.resx') -ValueProperty 'En'
Write-ResxFile -Path (Join-Path $uiDir 'Strings.zh-Hans.resx') -ValueProperty 'Zh'

# ---------------------------------------------------------------------------
# Strongly typed accessor.
# ---------------------------------------------------------------------------
$sb = New-Object System.Text.StringBuilder
[void] $sb.AppendLine('// <auto-generated>')
[void] $sb.AppendLine('//     Generated by tools/Generate-Strings.ps1. Do not edit by hand.')
[void] $sb.AppendLine('// </auto-generated>')
[void] $sb.AppendLine()
[void] $sb.AppendLine('using System.Globalization;')
[void] $sb.AppendLine('using System.Resources;')
[void] $sb.AppendLine()
[void] $sb.AppendLine('namespace ThemeExplorer.Ui')
[void] $sb.AppendLine('{')
[void] $sb.AppendLine('    /// <summary>')
[void] $sb.AppendLine('    /// Localised UI strings.  The resource manager honours <see cref="CultureInfo.CurrentUICulture"/>,')
[void] $sb.AppendLine('    /// so switching the UI language is just a matter of changing the current thread culture.')
[void] $sb.AppendLine('    /// <para xml:lang="zh-CN">本地化界面字符串。资源管理器遵循 <see cref="CultureInfo.CurrentUICulture"/>，因此切换界面语言只需修改当前线程的区域文化。</para>')
[void] $sb.AppendLine('    /// </summary>')
[void] $sb.AppendLine('    public static class Strings')
[void] $sb.AppendLine('    {')
[void] $sb.AppendLine('        private static readonly ResourceManager Resources =')
[void] $sb.AppendLine('            new ResourceManager("ThemeExplorer.Ui.Strings", typeof(Strings).Assembly);')
[void] $sb.AppendLine()
foreach ($entry in $strings) {
    [void] $sb.AppendLine(('        public static string {0} => Get("{0}");' -f $entry.Name))
}
[void] $sb.AppendLine()
[void] $sb.AppendLine('        private static string Get(string name)')
[void] $sb.AppendLine('        {')
[void] $sb.AppendLine('            return Resources.GetString(name, CultureInfo.CurrentUICulture) ?? name;')
[void] $sb.AppendLine('        }')
[void] $sb.AppendLine('    }')
[void] $sb.AppendLine('}')

[IO.File]::WriteAllText((Join-Path $uiDir 'Strings.cs'), $sb.ToString(), (New-Object System.Text.UTF8Encoding($true)))

Write-Host ("Generated {0} strings for English and zh-Hans." -f $strings.Count)
