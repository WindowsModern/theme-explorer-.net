using System;
using System.Drawing;
using System.Globalization;
using System.Text;
using ThemeExplorer.Core;

namespace ThemeExplorer.Ui
{
    /// <summary>
    /// Turns typed theme values into localised display strings.
    /// <para xml:lang="zh-CN">把强类型的主题值转换为本地化显示字符串。</para>
    /// </summary>
    public static class PropertyValueFormatter
    {
        public static string GetTypeName(ThemePropertyType type)
        {
            switch (type)
            {
                case ThemePropertyType.Enum: return Strings.TypeEnum;
                case ThemePropertyType.String: return Strings.TypeString;
                case ThemePropertyType.Int: return Strings.TypeInt;
                case ThemePropertyType.Bool: return Strings.TypeBool;
                case ThemePropertyType.Color: return Strings.TypeColor;
                case ThemePropertyType.Margins: return Strings.TypeMargins;
                case ThemePropertyType.Filename: return Strings.TypeFilename;
                case ThemePropertyType.Size: return Strings.TypeSize;
                case ThemePropertyType.Position: return Strings.TypePosition;
                case ThemePropertyType.Rect: return Strings.TypeRect;
                case ThemePropertyType.Font: return Strings.TypeFont;
                case ThemePropertyType.IntList: return Strings.TypeIntList;
                case ThemePropertyType.DiskStream: return Strings.TypeDiskStream;
                case ThemePropertyType.Stream: return Strings.TypeStream;
                default: return type.ToString();
            }
        }

        public static string GetOriginName(ThemePropertyOrigin origin)
        {
            switch (origin)
            {
                case ThemePropertyOrigin.State: return Strings.OriginState;
                case ThemePropertyOrigin.Part: return Strings.OriginPart;
                case ThemePropertyOrigin.Class: return Strings.OriginClass;
                case ThemePropertyOrigin.Global: return Strings.OriginGlobal;
                default: return Strings.OriginUnknown;
            }
        }

        public static string Format(ThemePropertyValue value)
        {
            if (value == null)
                return string.Empty;

            CultureInfo culture = CultureInfo.CurrentCulture;
            switch (value.Type)
            {
                case ThemePropertyType.Enum:
                    return value.EnumSymbol == null
                        ? value.IntValue.ToString(culture)
                        : string.Format(culture, "{0} ({1})", value.IntValue, value.EnumSymbol);

                case ThemePropertyType.Int:
                    return string.Format(culture, Strings.FormatInt, value.IntValue, value.IntValue & 0xFFFFFFFFL);

                case ThemePropertyType.Bool:
                    return value.BoolValue ? Strings.ValueTrue : Strings.ValueFalse;

                case ThemePropertyType.Color:
                {
                    Color color = value.ColorValue;
                    return string.Format(culture, Strings.FormatColor, color.R, color.G, color.B,
                        ((uint)color.ToArgb()) & 0xFFFFFFu);
                }

                case ThemePropertyType.Margins:
                {
                    ThemeMargins margins = value.MarginsValue;
                    return string.Format(culture, Strings.FormatMargins,
                        margins.Left, margins.Right, margins.Top, margins.Bottom);
                }

                case ThemePropertyType.Size:
                    return string.Format(culture, Strings.FormatSize, value.SizeValue.Width, value.SizeValue.Height);

                case ThemePropertyType.Position:
                    return string.Format(culture, Strings.FormatPosition, value.PointValue.X, value.PointValue.Y);

                case ThemePropertyType.Rect:
                {
                    Rectangle rect = value.RectValue;
                    return string.Format(culture, Strings.FormatRect,
                        rect.Left, rect.Top, rect.Right, rect.Bottom, rect.Width, rect.Height);
                }

                case ThemePropertyType.Font:
                    return FormatFont(value.FontValue);

                case ThemePropertyType.IntList:
                {
                    if (value.IntListValue == null || value.IntListValue.Length == 0)
                        return string.Empty;

                    var builder = new StringBuilder();
                    for (int i = 0; i < value.IntListValue.Length; i++)
                    {
                        if (i > 0)
                            builder.Append(Strings.IntListSeparator);
                        builder.Append(value.IntListValue[i].ToString(culture));
                    }
                    return builder.ToString();
                }

                case ThemePropertyType.Stream:
                    return string.Format(culture, Strings.FormatStream, value.StreamLength);

                case ThemePropertyType.DiskStream:
                    return string.Format(culture, Strings.FormatDiskStream, value.StreamLength);

                case ThemePropertyType.String:
                case ThemePropertyType.Filename:
                    return value.TextValue ?? string.Empty;

                default:
                    return value.ToString();
            }
        }

        private static string FormatFont(ThemeFontInfo font)
        {
            if (font == null)
                return string.Empty;

            var builder = new StringBuilder();
            builder.AppendFormat(CultureInfo.CurrentCulture, "{0},{1} {2}",
                font.FaceName, font.Height, GetWeightName(font.Weight));

            if (font.Italic)
                builder.Append(' ').Append(Strings.FontItalic);
            if (font.Underline)
                builder.Append(' ').Append(Strings.FontUnderline);

            return builder.ToString();
        }

        private static string GetWeightName(int weight)
        {
            switch (((weight + 50) / 100) * 100)
            {
                case 100: return Strings.FontWeightThin;
                case 200: return Strings.FontWeightExtraLight;
                case 300: return Strings.FontWeightLight;
                case 400: return Strings.FontWeightRegular;
                case 500: return Strings.FontWeightMedium;
                case 600: return Strings.FontWeightSemibold;
                case 700: return Strings.FontWeightBold;
                case 800: return Strings.FontWeightExtraBold;
                case 900: return Strings.FontWeightHeavy;
                default: return Strings.FontWeightUnknown;
            }
        }
    }
}
