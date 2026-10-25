using System;
using System.Drawing;
using ThemeExplorer.Ui;

namespace ThemeExplorer.App
{
    /// <summary>
    /// Chooses a UI font that renders the current language well.
    /// </summary>
    internal static class UiFont
    {
        private static Font _font;

        internal static Font Get()
        {
            if (_font != null)
                return _font;

            string face = UiCulture.IsChinese ? "Microsoft YaHei UI" : "Segoe UI";
            try
            {
                Font candidate = new Font(face, 9f, FontStyle.Regular, GraphicsUnit.Point);
                if (string.Equals(candidate.Name, face, StringComparison.OrdinalIgnoreCase))
                {
                    _font = candidate;
                    return _font;
                }
                candidate.Dispose();
            }
            catch (ArgumentException)
            {
                // Fall through to the generic font.
            }

            _font = new Font(FontFamily.GenericSansSerif, 9f, FontStyle.Regular, GraphicsUnit.Point);
            return _font;
        }
    }
}
