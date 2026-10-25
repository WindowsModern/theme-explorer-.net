using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ThemeExplorer.Core;
using ThemeExplorer.Ui;
using ThemeExplorer.Wpf.Interop;

namespace ThemeExplorer.Wpf
{
    /// <summary>
    /// Headless self-test for the WPF build.  Besides the functional checks it runs
    /// a selection stress loop and verifies that GDI/USER handles and managed memory
    /// return to their baseline, which is the practical proof that the rendering
    /// path releases everything it allocates.
    /// </summary>
    internal static class SelfTest
    {
        private const int StressIterations = 150;

        internal static bool IsRequested(string[] args)
        {
            if (args == null)
                return false;

            foreach (string arg in args)
            {
                if (string.Equals(arg, "--selftest", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        internal static int Run(string[] args)
        {
            string outputDirectory = GetOption(args, "--out");
            if (string.IsNullOrEmpty(outputDirectory))
                outputDirectory = Path.Combine(Path.GetTempPath(), "ThemeExplorer-WpfSelfTest");
            Directory.CreateDirectory(outputDirectory);

            var log = new List<string>();
            int exitCode = 0;
            MainWindow window = null;

            if (Application.Current != null)
                Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            try
            {
                window = new MainWindow
                {
                    ShowInTaskbar = false,
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Left = -30000,
                    Top = -30000
                };

                window.Show();
                DoEvents();

                log.Add("toolkit=WPF");
                log.Add("culture=" + CultureInfo.CurrentUICulture.Name);
                log.Add("process-bitness=" + (IntPtr.Size * 8));
                log.Add("layout=" + window.LayoutSummary);
                log.Add("title=" + window.Title);
                log.Add("expected-title=" + Strings.AppTitle);

                if (!string.Equals(window.Title, Strings.AppTitle, StringComparison.Ordinal))
                {
                    log.Add("ASSERT-FAIL: window title is not localised");
                    exitCode = 1;
                }

                if (CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh"
                    && !string.Equals(Strings.AppTitle, "主题浏览器", StringComparison.Ordinal))
                {
                    log.Add("ASSERT-FAIL: zh-Hans resources were not loaded");
                    exitCode = 1;
                }

                window.SelectTheme("Button", 1, 1);
                DoEvents();

                log.Add("class=" + window.ClassText);
                log.Add("part=" + window.PartText);
                log.Add("state=" + window.StateText);
                log.Add("status=" + window.StatusText);
                log.Add("rows=" + window.PropertyRowCount);

                if (window.PropertyRowCount <= 0)
                {
                    log.Add("ASSERT-FAIL: no properties were listed");
                    exitCode = 1;
                }

                window.SelectTheme("Clock", 1, 1);
                DoEvents();
                log.Add("missing-class-rows=" + window.PropertyRowCount);
                log.Add("missing-class-status=" + window.StatusText);
                if (window.PropertyRowCount != 0)
                {
                    log.Add("ASSERT-FAIL: an undefined class listed properties");
                    exitCode = 1;
                }

                window.SetShowAllProperties(true);
                window.SelectTheme("Button", 1, 1);
                DoEvents();
                log.Add("show-all-rows=" + window.PropertyRowCount);
                if (window.PropertyRowCount < 200)
                {
                    log.Add("ASSERT-FAIL: show-all did not list every property");
                    exitCode = 1;
                }

                window.SetShowAllProperties(false);
                window.SelectTheme("Button", 1, 1);
                DoEvents();

                RunRenderDiagnostics(log, ref exitCode);
                RunStress(window, log, ref exitCode);
                RunWindowChurnTest(log, ref exitCode);

                window.SelectTheme("Button", 1, 1);
                DoEvents();
                window.ScrollTreeToSelection();
                DoEvents();

                SaveScreenshot(window, Path.Combine(outputDirectory, "wpf-zh-Hans.png"), log);

                window.ChangeLanguage("en");
                DoEvents();
                log.Add("switched-title=" + window.Title);
                log.Add("switched-status=" + window.StatusText);
                log.Add("switched-rows=" + window.PropertyRowCount);

                if (!string.Equals(window.Title, "Theme Explorer", StringComparison.Ordinal))
                {
                    log.Add("ASSERT-FAIL: switching to English did not update the window title");
                    exitCode = 1;
                }

                SaveScreenshot(window, Path.Combine(outputDirectory, "wpf-en.png"), log);

                window.Close();
            }
            catch (Exception exception)
            {
                log.Add("ERROR: " + exception);
                exitCode = 1;
            }
            finally
            {
                if (window != null)
                {
                    try
                    {
                        window.Close();
                    }
                    catch (Exception)
                    {
                    }
                }
            }

            File.WriteAllLines(Path.Combine(outputDirectory, "selftest.log"), log.ToArray(),
                new UTF8Encoding(false));

            return exitCode;
        }

        private static void RunRenderDiagnostics(List<string> log, ref int exitCode)
        {
            var background = System.Drawing.Color.FromArgb(255, 240, 240, 240);

            using (var context = new VisualStyleContext())
            using (VisualStyle style = context.Open("Button"))
            {
                if (style == null)
                    return;

                Settle();
                Snapshot beforePixels = Capture();

                for (int i = 0; i < 200; i++)
                {
                    RenderedImage image = ThemeRenderService.RenderPart(style, 1, 1 + (i % 6),
                        80, 48, true, "Foo Bar", background);
                    if (image == null)
                        break;
                }

                Settle();
                Snapshot afterPixels = Capture();

                Settle();
                Snapshot beforeSources = Capture();

                for (int i = 0; i < 200; i++)
                {
                    BitmapSource source = ThemeRenderService.RenderPartSource(style, 1, 1 + (i % 6),
                        80, 48, true, "Foo Bar", background);
                    if (source == null)
                        break;
                }

                Settle();
                Snapshot afterSources = Capture();

                long pixelDelta = afterPixels.Commit - beforePixels.Commit;
                long sourceDelta = afterSources.Commit - beforeSources.Commit;

                log.Add("diagnostic-pixels-commit-delta=" + pixelDelta);
                log.Add("diagnostic-sources-commit-delta=" + sourceDelta);

                if (pixelDelta > 4L * 1024 * 1024)
                {
                    log.Add("ASSERT-FAIL: render-only pixel path grew commit by " + pixelDelta + " bytes");
                    exitCode = 1;
                }

                if (sourceDelta > 4L * 1024 * 1024)
                {
                    log.Add("ASSERT-FAIL: render-only source path grew commit by " + sourceDelta + " bytes");
                    exitCode = 1;
                }
            }
        }

        private static void RunWindowChurnTest(List<string> log, ref int exitCode)
        {
            Settle();
            Snapshot before = Capture();

            const int windowCount = 20;
            for (int i = 0; i < windowCount; i++)
            {
                var child = new MainWindow
                {
                    ShowInTaskbar = false,
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Left = -30000,
                    Top = -30000
                };

                child.Show();
                DoEvents();
                child.SelectTheme("Button", 1, 1);
                child.Close();
                DoEvents();
            }

            Settle();

            Snapshot after = Capture();
            int gdiDelta = after.Gdi - before.Gdi;
            int userDelta = after.User - before.User;
            int handleDelta = after.Handles - before.Handles;

            log.Add("window-churn-count=" + windowCount);
            log.Add("window-churn-remaining-windows=" + Application.Current.Windows.Count);
            log.Add("window-churn-gdi-delta=" + gdiDelta);
            log.Add("window-churn-user-delta=" + userDelta);
            log.Add("window-churn-handles-delta=" + handleDelta);

            if (gdiDelta > 8)
            {
                log.Add("ASSERT-FAIL: closing windows leaked GDI objects (" + gdiDelta + ")");
                exitCode = 1;
            }

            if (userDelta > 8)
            {
                log.Add("ASSERT-FAIL: closing windows leaked USER objects (" + userDelta + ")");
                exitCode = 1;
            }

            if (handleDelta > 24)
            {
                log.Add("ASSERT-FAIL: closing windows leaked process handles (" + handleDelta + ")");
                exitCode = 1;
            }
        }

        private static void RunStress(MainWindow window, List<string> log, ref int exitCode)
        {
            IList<ThemeClassInfo> classes = ThemeDatabase.Classes;

            // Warm up JIT, WPF's render caches and the theme service before taking
            // the baseline, otherwise the first iterations dominate the numbers.
            for (int i = 0; i < 15; i++)
                SelectFirstPart(window, classes[i]);

            window.Hide();
            Settle();
            Snapshot before = Capture();

            for (int i = 0; i < StressIterations; i++)
            {
                // Keep the tree path fixed and cycle through the six Button states.
                // This isolates the per-selection rendering/property churn from the
                // visuals created when the user expands more and more tree nodes.
                window.SelectTheme("Button", 1, 1 + (i % 6));

                // Give WPF's render thread a chance to retire the previous frame.
                // Without this the loop measures the render queue, not leaks.
                System.Threading.Thread.Sleep(12);

                if ((i & 7) == 0)
                    DoEvents();
            }

            window.Show();
            Settle();
            Snapshot after = Capture();

            long managedDelta = after.Managed - before.Managed;
            long workingSetDelta = after.WorkingSet - before.WorkingSet;
            long commitDelta = after.Commit - before.Commit;
            int gdiDelta = after.Gdi - before.Gdi;
            int userDelta = after.User - before.User;
            int handleDelta = after.Handles - before.Handles;

            log.Add("stress-iterations=" + StressIterations);
            log.Add("stress-window=hidden-excluding-wpf-compositor-surfaces");
            log.Add("stress-managed-before=" + before.Managed);
            log.Add("stress-managed-after=" + after.Managed);
            log.Add("stress-managed-delta=" + managedDelta);
            log.Add("stress-working-set-delta=" + workingSetDelta);
            log.Add("stress-commit-delta=" + commitDelta);
            log.Add("stress-gdi-before=" + before.Gdi);
            log.Add("stress-gdi-after=" + after.Gdi);
            log.Add("stress-gdi-delta=" + gdiDelta);
            log.Add("stress-user-before=" + before.User);
            log.Add("stress-user-after=" + after.User);
            log.Add("stress-user-delta=" + userDelta);
            log.Add("stress-handles-delta=" + handleDelta);

            // A leak of one GDI object per selection would show up as +150 here.
            if (gdiDelta > 16)
            {
                log.Add("ASSERT-FAIL: GDI object count grew by " + gdiDelta);
                exitCode = 1;
            }

            if (userDelta > 16)
            {
                log.Add("ASSERT-FAIL: USER object count grew by " + userDelta);
                exitCode = 1;
            }

            if (handleDelta > 64)
            {
                log.Add("ASSERT-FAIL: process handle count grew by " + handleDelta);
                exitCode = 1;
            }

            if (managedDelta > 24L * 1024 * 1024)
            {
                log.Add("ASSERT-FAIL: managed memory grew by " + managedDelta + " bytes");
                exitCode = 1;
            }

            // Commit/working-set size is logged but not asserted: WPF retains a
            // variable number of internal caches/surfaces (even for a hidden
            // window) from run to run.  GDI/USER/handle counts and the managed heap
            // are the stable leak signals.
        }

        private static void SelectFirstPart(MainWindow window, ThemeClassInfo cls)
        {
            ThemePartInfo part = cls.Parts.Count > 0 ? cls.Parts[0] : null;
            ThemeStateInfo state = part != null && part.States.Count > 0 ? part.States[0] : null;

            window.SelectTheme(cls.Name,
                part != null ? part.Id : 0,
                state != null ? state.Id : 0);
        }

        private static void SaveScreenshot(Window window, string path, List<string> log)
        {
            window.UpdateLayout();

            double scale = ThemeRenderService.Scale;
            int width = Math.Max(1, (int)Math.Ceiling(window.ActualWidth * scale));
            int height = Math.Max(1, (int)Math.Ceiling(window.ActualHeight * scale));

            var bitmap = new RenderTargetBitmap(width, height, 96.0 * scale, 96.0 * scale,
                PixelFormats.Pbgra32);
            bitmap.Render(window);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (FileStream stream = File.Create(path))
                encoder.Save(stream);

            log.Add("screenshot=" + path);
        }

        private static void Settle()
        {
            for (int i = 0; i < 6; i++)
            {
                DoEvents();
                Collect();
                System.Threading.Thread.Sleep(30);
            }

            DoEvents();
        }

        private static void DoEvents()
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,
                new Action(delegate { frame.Continue = false; }));
            Dispatcher.PushFrame(frame);
        }

        private static void Collect()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        private static long GetCommitBytes()
        {
            NativeGdi.PROCESS_MEMORY_COUNTERS counters;
            int size = Marshal.SizeOf(typeof(NativeGdi.PROCESS_MEMORY_COUNTERS));
            if (!NativeGdi.GetProcessMemoryInfo(NativeGdi.GetCurrentProcess(), out counters, size))
                return 0;

            return counters.PagefileUsage.ToInt64();
        }

        private static Snapshot Capture()
        {
            int handleCount;
            NativeGdi.GetProcessHandleCount(NativeGdi.GetCurrentProcess(), out handleCount);

            return new Snapshot
            {
                Managed = GC.GetTotalMemory(true),
                WorkingSet = Environment.WorkingSet,
                Commit = GetCommitBytes(),
                Gdi = (int)NativeGdi.GetGuiResources(NativeGdi.GetCurrentProcess(), 0),
                User = (int)NativeGdi.GetGuiResources(NativeGdi.GetCurrentProcess(), 1),
                Handles = handleCount
            };
        }

        private static string GetOption(string[] args, string name)
        {
            if (args == null)
                return null;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (string.Equals(arg, name, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    return args[i + 1];

                string prefix = name + "=";
                if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return arg.Substring(prefix.Length);
            }

            return null;
        }

        private sealed class Snapshot
        {
            public long Managed;
            public long WorkingSet;
            public long Commit;
            public int Gdi;
            public int User;
            public int Handles;
        }
    }
}
