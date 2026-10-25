using System;
using System.Windows;
using ThemeExplorer.Ui;

namespace ThemeExplorer.Wpf
{
    /// <summary>
    /// WPF application entry point.  There is no XAML: the whole UI is built in
    /// code so that localization can switch at run time without reloading a BAML
    /// resource dictionary.
    /// </summary>
    public sealed class App : Application
    {
        [STAThread]
        public static int Main(string[] args)
        {
            UiCulture.Initialize(args);

            var app = new App();
            return app.Run();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            if (SelfTest.IsRequested(e.Args))
            {
                int exitCode = SelfTest.Run(e.Args);
                Shutdown(exitCode);
                return;
            }

            var window = new MainWindow();
            MainWindow = window;
            window.Show();
        }
    }
}
