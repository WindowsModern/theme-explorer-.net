using System;
using System.IO;
using System.Windows.Forms;
using ThemeExplorer.Ui;

namespace ThemeExplorer.App
{
    internal static class Program
    {
        private static readonly string ErrorLogPath =
            Path.Combine(Path.GetTempPath(), "ThemeExplorer-error.log");

        [STAThread]
        private static void Main(string[] args)
        {
            UiCulture.Initialize(args);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += OnThreadException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            if (SelfTest.IsRequested(args))
            {
                Environment.Exit(SelfTest.Run(args));
                return;
            }

            Application.Run(new MainForm());
        }

        private static void OnThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            ReportException(e.Exception);
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            ReportException(e.ExceptionObject as Exception);
        }

        private static void ReportException(Exception exception)
        {
            if (exception == null)
                return;

            try
            {
                File.AppendAllText(ErrorLogPath,
                    DateTime.Now.ToString("s") + " " + exception + Environment.NewLine);
            }
            catch (IOException)
            {
                // Logging must never hide the original failure.
            }
            catch (UnauthorizedAccessException)
            {
            }

            MessageBox.Show(string.Format(Strings.ErrorUnexpected, exception.Message),
                Strings.ErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
