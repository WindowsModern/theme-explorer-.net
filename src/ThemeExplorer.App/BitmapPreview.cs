using System;
using System.Drawing;
using System.Windows.Forms;

namespace ThemeExplorer.App
{
    /// <summary>
    /// Shows the bitmap embedded in a theme part, or a localised placeholder when
    /// the part has none.
    /// </summary>
    internal sealed class BitmapPreview : Control
    {
        private Image _image;
        private string _emptyText;

        internal BitmapPreview()
        {
            SetStyle(ControlStyles.UserPaint
                | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw, true);
            BackColor = SystemColors.Control;
        }

        internal Image Image
        {
            get { return _image; }
            set
            {
                if (ReferenceEquals(_image, value))
                    return;

                if (_image != null)
                    _image.Dispose();
                _image = value;
                Invalidate();
            }
        }

        internal string EmptyText
        {
            get { return _emptyText; }
            set
            {
                _emptyText = value;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics graphics = e.Graphics;
            graphics.Clear(BackColor);

            Rectangle bounds = ClientRectangle;
            ControlPaint.DrawBorder(graphics, bounds, SystemColors.ControlDark, ButtonBorderStyle.Solid);

            if (_image != null)
            {
                int width = _image.Width;
                int height = _image.Height;
                if (width > bounds.Width - 4 || height > bounds.Height - 4)
                {
                    float scale = Math.Min((bounds.Width - 4f) / width, (bounds.Height - 4f) / height);
                    width = Math.Max(1, (int)(width * scale));
                    height = Math.Max(1, (int)(height * scale));
                }

                int x = bounds.Left + (bounds.Width - width) / 2;
                int y = bounds.Top + (bounds.Height - height) / 2;
                graphics.DrawImage(_image, new Rectangle(x, y, width, height));
            }
            else if (!string.IsNullOrEmpty(_emptyText))
            {
                TextRenderer.DrawText(graphics, _emptyText, Font, bounds, SystemColors.GrayText,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _image != null)
            {
                _image.Dispose();
                _image = null;
            }

            base.Dispose(disposing);
        }
    }
}
