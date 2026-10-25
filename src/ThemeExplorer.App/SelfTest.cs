using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;
using ThemeExplorer.Core;
using ThemeExplorer.Ui;

namespace ThemeExplorer.App
{
    /// <summary>
    /// Headless smoke test used by the build script.  It opens the main window,
    /// selects a known part, records the property table, renders two screenshots
    /// (Chinese and English) and writes a log.  This makes it possible to verify a
    /// GUI build on a machine without an interactive desktop session.
    /// </summary>
    internal static class SelfTest
    {
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
                outputDirectory = Path.Combine(Path.GetTempPath(), "ThemeExplorer-SelfTest");
            Directory.CreateDirectory(outputDirectory);

            var log = new List<string>();
            int exitCode = 0;
            MainForm form = null;

            try
            {
                form = new MainForm
                {
                    ShowInTaskbar = false,
                    StartPosition = FormStartPosition.Manual,
                    Location = new Point(-30000, -30000)
                };

                form.Show();
                Application.DoEvents();

                log.Add("culture=" + CultureInfo.CurrentUICulture.Name);
                log.Add("process-bitness=" + (IntPtr.Size * 8));
                log.Add("layout=" + form.LayoutSummary);
                log.Add("theme-active=" + ThemeSystem.IsThemeActive);
                log.Add("app-themed=" + ThemeSystem.IsAppThemed);
                log.Add("visual-styles=" + ThemeSystem.IsVisualStylesEnabled);
                log.Add("title=" + form.Text);
                log.Add("expected-title=" + Strings.AppTitle);
                log.Add("auto-update=" + form.AutoUpdateTheme);
                log.Add("auto-update-text=" + form.AutoUpdateThemeText);

                if (!form.AutoUpdateTheme)
                {
                    log.Add("ASSERT-FAIL: automatic theme updates are not enabled by default");
                    exitCode = 1;
                }

                if (!string.Equals(form.AutoUpdateThemeText, Strings.MenuViewAutoUpdate, StringComparison.Ordinal))
                {
                    log.Add("ASSERT-FAIL: automatic theme update menu text is not localized");
                    exitCode = 1;
                }

                if (!string.Equals(form.Text, Strings.AppTitle, StringComparison.Ordinal))
                {
                    log.Add("ASSERT-FAIL: window title is not localised");
                    exitCode = 1;
                }

                if (CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh"
                    && !string.Equals(Strings.AppTitle, "主题浏览器", StringComparison.Ordinal))
                {
                    log.Add("ASSERT-FAIL: zh-Hans satellite resources were not loaded");
                    exitCode = 1;
                }

                form.SelectTheme("Button", 1, 1);
                Application.DoEvents();

                string selectedBeforeRefresh = form.SelectedTreeNodeText;
                form.SimulateThemeChange();
                Application.DoEvents();
                log.Add("selected-before-refresh=" + selectedBeforeRefresh);
                log.Add("selected-after-refresh=" + form.SelectedTreeNodeText);

                if (!string.Equals(selectedBeforeRefresh, form.SelectedTreeNodeText, StringComparison.Ordinal))
                {
                    log.Add("ASSERT-FAIL: a theme refresh changed the selected tree node");
                    exitCode = 1;
                }

                form.SetAutoUpdateTheme(false);
                log.Add("auto-update-disabled=" + form.AutoUpdateTheme);
                if (form.AutoUpdateTheme)
                {
                    log.Add("ASSERT-FAIL: automatic theme updates could not be disabled");
                    exitCode = 1;
                }
                form.SetAutoUpdateTheme(true);
                Application.DoEvents();

                log.Add("class=" + form.ClassText);
                log.Add("part=" + form.PartText);
                log.Add("state=" + form.StateText);
                log.Add("status=" + form.StatusText);
                log.Add("rows=" + form.PropertyRowCount);

                if (form.PropertyRowCount <= 0)
                {
                    log.Add("ASSERT-FAIL: no properties were listed");
                    exitCode = 1;
                }

                foreach (string row in form.PropertyRows(6))
                    log.Add("row=" + row);

                using (var probeContext = new VisualStyleContext())
                using (var probeStyle = probeContext.Open("Button", null))
                {
                    log.Add("probe-partdefined=" + (probeStyle != null && probeStyle.IsPartDefined(1, 1)));

                    using (var probeBitmap = new Bitmap(140, 60))
                    using (Graphics probeGraphics = Graphics.FromImage(probeBitmap))
                    {
                        probeGraphics.Clear(Color.White);
                        bool drawn = probeStyle != null && ThemeRenderer.TryDrawBackground(
                            probeGraphics, probeStyle, 1, 1, new Rectangle(0, 0, 140, 60));
                        log.Add("probe-drawbackground=" + drawn);
                        probeBitmap.Save(Path.Combine(outputDirectory, "probe-background.png"), ImageFormat.Png);

                        bool textDrawn = probeStyle != null && ThemeRenderer.TryDrawText(
                            probeGraphics, probeStyle, 1, 1, "Foo Bar",
                            new Rectangle(0, 0, 140, 60),
                            ThemeTextFormat.Center | ThemeTextFormat.VerticalCenter | ThemeTextFormat.SingleLine);
                        log.Add("probe-drawtext=" + textDrawn);
                        probeBitmap.Save(Path.Combine(outputDirectory, "probe-text.png"), ImageFormat.Png);
                    }
                }

                // A class that the current theme does not define must not crash.
                form.SelectTheme("Clock", 1, 1);
                Application.DoEvents();
                log.Add("missing-class-rows=" + form.PropertyRowCount);
                log.Add("missing-class-status=" + form.StatusText);
                if (form.PropertyRowCount != 0)
                {
                    log.Add("ASSERT-FAIL: an undefined class listed properties");
                    exitCode = 1;
                }

                // "Show all properties" restores the raw (unfiltered) behaviour.
                form.SetShowAllProperties(true);
                form.SelectTheme("Button", 1, 1);
                Application.DoEvents();
                log.Add("show-all-rows=" + form.PropertyRowCount);
                if (form.PropertyRowCount < 200)
                {
                    log.Add("ASSERT-FAIL: show-all did not list every property");
                    exitCode = 1;
                }

                form.SetShowAllProperties(false);
                form.SelectTheme("Button", 1, 1);
                Application.DoEvents();

                form.ScrollTreeToSelection();
                Application.DoEvents();

                string chineseScreenshot = Path.Combine(outputDirectory, "window-zh-Hans.png");
                SaveScreenshot(form, chineseScreenshot);
                log.Add("screenshot=" + chineseScreenshot);

                form.ChangeLanguage("en");
                Application.DoEvents();

                log.Add("switched-title=" + form.Text);
                log.Add("switched-status=" + form.StatusText);
                log.Add("switched-rows=" + form.PropertyRowCount);

                if (!string.Equals(form.Text, "Theme Explorer", StringComparison.Ordinal))
                {
                    log.Add("ASSERT-FAIL: switching to English did not update the window title");
                    exitCode = 1;
                }

                form.ScrollTreeToSelection();
                Application.DoEvents();

                string englishScreenshot = Path.Combine(outputDirectory, "window-en.png");
                SaveScreenshot(form, englishScreenshot);
                log.Add("screenshot=" + englishScreenshot);

                form.Close();
            }
            catch (Exception exception)
            {
                log.Add("ERROR: " + exception);
                exitCode = 1;
            }
            finally
            {
                if (form != null)
                {
                    try
                    {
                        form.Dispose();
                    }
                    catch (Exception)
                    {
                        // Ignore cleanup failures in the test harness.
                    }
                }
            }

            File.WriteAllLines(Path.Combine(outputDirectory, "selftest.log"), log.ToArray(),
                new UTF8Encoding(false));

            return exitCode;
        }

        private static void SaveScreenshot(Form form, string path)
        {
            using (var bitmap = new Bitmap(form.Width, form.Height))
            {
                form.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
                bitmap.Save(path, ImageFormat.Png);
            }
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
    }
}
