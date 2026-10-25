using System;
using System.Drawing;
using System.Windows.Forms;
using ThemeExplorer.Core;
using ThemeExplorer.Ui;

namespace ThemeExplorer.App
{
    /// <summary>
    /// Small owner-drawn control that previews one theme part/state pair, optionally
    /// with themed text.  This replaces the custom window class of the original C
    /// implementation.
    /// </summary>
    internal sealed class ThemePreviewControl : Control
    {
        private readonly bool _drawText;
        private VisualStyle _style;
        private int _part;
        private int _state;

        internal ThemePreviewControl(bool drawText)
        {
            _drawText = drawText;
            SetStyle(ControlStyles.UserPaint
                | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw, true);
            BackColor = SystemColors.Control;
        }

        internal void UpdateStyle(VisualStyle style, int part, int state)
        {
            _style = style;
            _part = part;
            _state = state;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics graphics = e.Graphics;
            graphics.Clear(BackColor);

            if (_style == null || _style.IsDisposed)
                return;

            if (ThemeRenderer.IsBackgroundPartiallyTransparent(_style, _part, _state))
                ThemeRenderer.TryDrawParentBackground(Handle, graphics, ClientRectangle);

            if (!ThemeRenderer.TryDrawBackground(graphics, _style, _part, _state, ClientRectangle))
                graphics.Clear(BackColor);

            if (_drawText)
            {
                ThemeRenderer.TryDrawText(graphics, _style, _part, _state, Strings.SampleText,
                    ClientRectangle,
                    ThemeTextFormat.Center | ThemeTextFormat.VerticalCenter | ThemeTextFormat.SingleLine);
            }
        }
    }
}
