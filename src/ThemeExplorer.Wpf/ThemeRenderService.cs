using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ThemeExplorer.Core;
using ThemeExplorer.Wpf.Interop;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingColor = System.Drawing.Color;
using DrawingRectangle = System.Drawing.Rectangle;
using DrawingBitmapData = System.Drawing.Imaging.BitmapData;
using DrawingImageLockMode = System.Drawing.Imaging.ImageLockMode;
using DrawingPixelFormat = System.Drawing.Imaging.PixelFormat;

namespace ThemeExplorer.Wpf
{
    /// <summary>
    /// Pixels of a rendered theme part.  The raw buffer is wrapped into a frozen
    /// <see cref="BitmapSource"/> by <c>RenderPartSource</c> (or consumed directly by
    /// diagnostics and tests).
    /// </summary>
    internal sealed class RenderedImage
    {
        internal RenderedImage(byte[] pixels, int width, int height, double dpi)
        {
            Pixels = pixels;
            Width = width;
            Height = height;
            Dpi = dpi;
        }

        internal byte[] Pixels { get; }

        internal int Width { get; }

        internal int Height { get; }

        internal double Dpi { get; }
    }

    /// <summary>
    /// Renders theme parts into pixel buffers or frozen <see cref="BitmapSource"/>
    /// instances.
    /// </summary>
    /// <remarks>
    /// Every call creates a DIB section, a memory DC and a GDI+ wrapper, draws the
    /// theme, copies the pixels into a managed array and destroys all native
    /// objects in a <c>finally</c> block.  No native handle and no image cache is
    /// kept, so dropping the returned buffer is enough to make the pixels
    /// collectable.
    /// </remarks>
    internal static class ThemeRenderService
    {
        private static readonly double DpiScale = ComputeDpiScale();

        internal static double Scale
        {
            get { return DpiScale; }
        }

        internal static RenderedImage RenderPart(VisualStyle style, int part, int state,
            double dipWidth, double dipHeight, bool drawText, string text, DrawingColor background)
        {
            if (style == null || style.IsDisposed || dipWidth <= 0 || dipHeight <= 0)
                return null;

            int pixelWidth = Math.Max(1, (int)Math.Ceiling(dipWidth * DpiScale));
            int pixelHeight = Math.Max(1, (int)Math.Ceiling(dipHeight * DpiScale));

            return Render(pixelWidth, pixelHeight, background, delegate(IntPtr hdc)
            {
                var bounds = new DrawingRectangle(0, 0, pixelWidth, pixelHeight);
                ThemeRenderer.TryDrawBackground(hdc, style, part, state, bounds);

                if (drawText)
                {
                    ThemeRenderer.TryDrawText(hdc, style, part, state, text, bounds,
                        ThemeTextFormat.Center | ThemeTextFormat.VerticalCenter | ThemeTextFormat.SingleLine);
                }
            });
        }

        /// <summary>
        /// Convenience wrapper that turns a rendered pixel buffer into a frozen
        /// <see cref="BitmapSource"/> suitable for WPF image elements.
        /// </summary>
        internal static BitmapSource RenderPartSource(VisualStyle style, int part, int state,
            double dipWidth, double dipHeight, bool drawText, string text, DrawingColor background)
        {
            RenderedImage rendered = RenderPart(style, part, state, dipWidth, dipHeight,
                drawText, text, background);
            if (rendered == null)
                return null;

            BitmapSource source = BitmapSource.Create(rendered.Width, rendered.Height,
                rendered.Dpi, rendered.Dpi, PixelFormats.Bgr32, null,
                rendered.Pixels, rendered.Width * 4);
            source.Freeze();
            return source;
        }

        internal static BitmapSource RenderBitmap(VisualStyle style, int part, int state, ThemeBitmapKind kind)
        {
            if (style == null || style.IsDisposed)
                return null;

            using (DrawingBitmap bitmap = ThemeRenderer.GetBitmap(style, part, state, kind))
            {
                if (bitmap == null)
                    return null;

                return ConvertBitmap(bitmap);
            }
        }

        private static RenderedImage Render(int width, int height, DrawingColor background, Action<IntPtr> draw)
        {
            var info = new NativeGdi.BITMAPINFO();
            info.bmiHeader.biSize = Marshal.SizeOf(typeof(NativeGdi.BITMAPINFOHEADER));
            info.bmiHeader.biWidth = width;
            info.bmiHeader.biHeight = -height;      // top-down DIB
            info.bmiHeader.biPlanes = 1;
            info.bmiHeader.biBitCount = 32;
            info.bmiHeader.biCompression = 0;       // BI_RGB

            IntPtr dc = NativeGdi.CreateCompatibleDC(IntPtr.Zero);
            if (dc == IntPtr.Zero)
                return null;

            IntPtr bits;
            IntPtr dib = NativeGdi.CreateDIBSection(dc, ref info, NativeGdi.DIB_RGB_COLORS,
                out bits, IntPtr.Zero, 0);
            if (dib == IntPtr.Zero)
            {
                NativeGdi.DeleteDC(dc);
                return null;
            }

            IntPtr previous = NativeGdi.SelectObject(dc, dib);
            try
            {
                using (Graphics graphics = Graphics.FromHdc(dc))
                {
                    // A theme part can be partially transparent.  The previews live
                    // on a solid surface, so compositing against that colour is the
                    // same thing DrawThemeParentBackground would do.
                    graphics.Clear(background);
                }

                draw(dc);

                int stride = width * 4;
                var pixels = new byte[stride * height];
                Marshal.Copy(bits, pixels, 0, pixels.Length);

                return new RenderedImage(pixels, width, height, 96.0 * DpiScale);
            }
            finally
            {
                NativeGdi.SelectObject(dc, previous);
                NativeGdi.DeleteObject(dib);
                NativeGdi.DeleteDC(dc);
            }
        }

        private static BitmapSource ConvertBitmap(DrawingBitmap bitmap)
        {
            int width = bitmap.Width;
            int height = bitmap.Height;
            DrawingBitmapData data = bitmap.LockBits(new DrawingRectangle(0, 0, width, height),
                DrawingImageLockMode.ReadOnly, DrawingPixelFormat.Format32bppArgb);
            try
            {
                int stride = Math.Abs(data.Stride);
                var pixels = new byte[stride * height];

                if (data.Stride == stride)
                {
                    Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
                }
                else
                {
                    // Bottom-up bitmap: copy row by row into the top-down buffer.
                    for (int y = 0; y < height; y++)
                    {
                        IntPtr row = new IntPtr(data.Scan0.ToInt64() + (long)(height - 1 - y) * stride);
                        Marshal.Copy(row, pixels, y * stride, stride);
                    }
                }

                BitmapSource source = BitmapSource.Create(width, height, 96.0, 96.0,
                    PixelFormats.Bgra32, null, pixels, stride);
                source.Freeze();
                return source;
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }

        private static double ComputeDpiScale()
        {
            try
            {
                using (Graphics graphics = Graphics.FromHwnd(IntPtr.Zero))
                {
                    double scale = graphics.DpiX / 96.0;
                    return scale <= 0 ? 1.0 : scale;
                }
            }
            catch (ArgumentException)
            {
                return 1.0;
            }
        }
    }
}
