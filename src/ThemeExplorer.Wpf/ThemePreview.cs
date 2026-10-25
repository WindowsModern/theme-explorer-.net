using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ThemeExplorer.Wpf
{
    /// <summary>
    /// Displays one pre-rendered theme part.  The control only holds a reference
    /// to a frozen <see cref="BitmapSource"/>; replacing or clearing it releases
    /// the old image to the garbage collector.
    /// </summary>
    internal sealed class ThemePreview : FrameworkElement
    {
        private BitmapSource _image;

        internal ThemePreview()
        {
            SnapsToDevicePixels = true;
            UseLayoutRounding = true;
            IsHitTestVisible = false;
        }

        internal void SetImage(BitmapSource image)
        {
            _image = image;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            if (_image == null)
                return;

            drawingContext.DrawImage(_image, new Rect(0, 0, ActualWidth, ActualHeight));
        }
    }
}
