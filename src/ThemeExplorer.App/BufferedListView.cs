using System.Windows.Forms;

namespace ThemeExplorer.App
{
    /// <summary>
    /// ListView with double buffering enabled; the properties list changes often
    /// and flickers badly without it.
    /// </summary>
    internal sealed class BufferedListView : ListView
    {
        internal BufferedListView()
        {
            DoubleBuffered = true;
        }
    }
}
