using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ThemeExplorer.Wpf
{
    /// <summary>
    /// Shows the background or glyph bitmap of a theme part, with a localised
    /// placeholder when the part does not provide one.
    /// </summary>
    internal sealed class ThemeBitmapView : Border
    {
        private readonly Image _image;
        private readonly TextBlock _placeholder;

        internal ThemeBitmapView()
        {
            Width = 80;
            Height = 80;
            BorderBrush = SystemColors.ControlDarkBrush;
            BorderThickness = new Thickness(1);
            Background = SystemColors.ControlBrush;

            _image = new Image
            {
                Stretch = Stretch.Uniform,
                StretchDirection = StretchDirection.DownOnly,
                Margin = new Thickness(2)
            };

            _placeholder = new TextBlock
            {
                Foreground = SystemColors.GrayTextBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11
            };

            var grid = new Grid();
            grid.Children.Add(_image);
            grid.Children.Add(_placeholder);
            Child = grid;
        }

        internal void SetImage(BitmapSource source)
        {
            _image.Source = source;
            _image.Visibility = source == null ? Visibility.Collapsed : Visibility.Visible;
            _placeholder.Visibility = source == null ? Visibility.Visible : Visibility.Collapsed;
        }

        internal void SetEmptyText(string text)
        {
            _placeholder.Text = text;
        }
    }
}
