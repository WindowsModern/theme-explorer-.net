using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ThemeExplorer.Core;
using ThemeExplorer.Ui;

namespace ThemeExplorer.Wpf
{
    internal sealed class PropertyRow
    {
        public string Type { get; set; }
        public string Id { get; set; }
        public string Property { get; set; }
        public string Value { get; set; }
        public string Origin { get; set; }
    }

    /// <summary>
    /// WPF port of the Theme Explorer window.  The layout mirrors the WinForms
    /// version; theme parts are rendered through GDI into frozen bitmaps, so no
    /// <c>HwndHost</c> or child HWND is created per preview.
    /// </summary>
    public sealed class MainWindow : Window
    {
        private static readonly System.Drawing.Color ControlBackground =
            System.Drawing.Color.FromArgb(255, SystemColors.ControlColor.R,
                SystemColors.ControlColor.G, SystemColors.ControlColor.B);

        private readonly VisualStyleContext _context;
        private readonly ThemePreview[] _previews = new ThemePreview[9];
        private readonly bool[] _previewDrawText = new bool[9];

        private TreeView _tree;
        private TextBlock _classCaption;
        private TextBlock _partCaption;
        private TextBlock _stateCaption;
        private TextBlock _classValue;
        private TextBlock _partValue;
        private TextBlock _stateValue;
        private ThemeBitmapView _backgroundView;
        private ThemeBitmapView _glyphView;
        private ListView _properties;
        private GridViewColumn[] _columns;
        private StatusBarItem _statusItem;
        private Menu _menu;
        private ContextMenu _propertyMenu;
        private MenuItem _copySelectedItem;
        private MenuItem _copyAllItem;
        private MenuItem _fileMenu;
        private MenuItem _fileExit;
        private MenuItem _viewMenu;
        private MenuItem _showAllItem;
        private MenuItem _languageMenu;
        private MenuItem _englishItem;
        private MenuItem _chineseItem;
        private MenuItem _helpMenu;
        private MenuItem _aboutItem;

        private ThemeClassInfo _currentClass;
        private ThemePartInfo _currentPart;
        private ThemeStateInfo _currentState;
        private bool _showAllProperties;
        private bool _hasTheme;
        private bool _updating;

        public MainWindow()
        {
            _context = new VisualStyleContext();

            BuildUi();
            TryLoadIcon();
            ApplyLocalization();
            PopulateTree();
            SelectInitialNode();
        }

        // ------------------------------------------------------------------
        // UI construction
        // ------------------------------------------------------------------

        private void BuildUi()
        {
            Title = Strings.AppTitle;
            Width = 980;
            Height = 700;
            MinWidth = 820;
            MinHeight = 560;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = SystemColors.ControlBrush;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            FontSize = 12;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);

            _menu = BuildMenu();

            _tree = new TreeView
            {
                BorderThickness = new Thickness(0, 0, 1, 0),
                BorderBrush = SystemColors.ControlDarkBrush,
                Padding = new Thickness(4),
                Background = Brushes.White
            };
            _tree.SelectedItemChanged += OnTreeSelected;
            VirtualizingStackPanel.SetIsVirtualizing(_tree, true);
            VirtualizingStackPanel.SetVirtualizationMode(_tree, VirtualizationMode.Recycling);

            var status = new StatusBar();
            _statusItem = new StatusBarItem { HorizontalAlignment = HorizontalAlignment.Left };
            status.Items.Add(_statusItem);

            var main = new Grid();
            main.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260), MinWidth = 180 });
            main.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
            main.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 480 });

            Grid.SetColumn(_tree, 0);
            main.Children.Add(_tree);

            var splitter = new GridSplitter
            {
                Width = 5,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Stretch,
                ResizeBehavior = GridResizeBehavior.PreviousAndNext,
                Background = SystemColors.ControlBrush
            };
            Grid.SetColumn(splitter, 1);
            main.Children.Add(splitter);

            var details = BuildDetails();
            Grid.SetColumn(details, 2);
            main.Children.Add(details);

            var dock = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(_menu, Dock.Top);
            dock.Children.Add(_menu);
            DockPanel.SetDock(status, Dock.Bottom);
            dock.Children.Add(status);
            dock.Children.Add(main);

            Content = dock;

            SizeChanged += delegate { AdjustColumnWidths(); };
        }

        private Menu BuildMenu()
        {
            var menu = new Menu();

            _fileMenu = new MenuItem();
            _fileExit = new MenuItem();
            _fileExit.Click += delegate { Close(); };
            _fileMenu.Items.Add(_fileExit);

            _viewMenu = new MenuItem();
            _showAllItem = new MenuItem { IsCheckable = true };
            _showAllItem.Click += OnShowAllClick;

            _languageMenu = new MenuItem();
            _englishItem = new MenuItem { IsCheckable = true };
            _chineseItem = new MenuItem { IsCheckable = true };
            _englishItem.Click += delegate { SetLanguage("en"); };
            _chineseItem.Click += delegate { SetLanguage("zh-Hans"); };
            _languageMenu.Items.Add(_englishItem);
            _languageMenu.Items.Add(_chineseItem);

            _viewMenu.Items.Add(_showAllItem);
            _viewMenu.Items.Add(new Separator());
            _viewMenu.Items.Add(_languageMenu);

            _helpMenu = new MenuItem();
            _aboutItem = new MenuItem();
            _aboutItem.Click += OnAboutClick;
            _helpMenu.Items.Add(_aboutItem);

            menu.Items.Add(_fileMenu);
            menu.Items.Add(_viewMenu);
            menu.Items.Add(_helpMenu);
            return menu;
        }

        private UIElement BuildDetails()
        {
            var grid = new Grid { Margin = new Thickness(8, 6, 8, 6) };
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(78) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(206) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var header = BuildHeader();
            Grid.SetRow(header, 0);
            grid.Children.Add(header);

            var previews = BuildPreviewGrid();
            Grid.SetRow(previews, 1);
            grid.Children.Add(previews);

            var list = BuildPropertyList();
            Grid.SetRow(list, 2);
            grid.Children.Add(list);

            return grid;
        }

        private UIElement BuildHeader()
        {
            var header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (int i = 0; i < 3; i++)
                header.RowDefinitions.Add(new RowDefinition { Height = new GridLength(22) });

            _classCaption = CreateCaption();
            _partCaption = CreateCaption();
            _stateCaption = CreateCaption();
            _classValue = CreateValue();
            _partValue = CreateValue();
            _stateValue = CreateValue();

            AddRow(header, 0, _classCaption, _classValue);
            AddRow(header, 1, _partCaption, _partValue);
            AddRow(header, 2, _stateCaption, _stateValue);

            return header;
        }

        private static TextBlock CreateCaption()
        {
            return new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
        }

        private static TextBlock CreateValue()
        {
            return new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Bold,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
        }

        private static void AddRow(Grid grid, int row, TextBlock caption, TextBlock value)
        {
            Grid.SetRow(caption, row);
            Grid.SetColumn(caption, 0);
            grid.Children.Add(caption);

            Grid.SetRow(value, row);
            Grid.SetColumn(value, 1);
            grid.Children.Add(value);
        }

        private UIElement BuildPreviewGrid()
        {
            var grid = new Grid
            {
                Background = SystemColors.ControlBrush,
                Margin = new Thickness(0, 0, 0, 6)
            };

            for (int i = 0; i < 3; i++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(112) });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(64) });
            }
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            AddPreview(grid, 0, 0, 16, 16, false);
            AddPreview(grid, 1, 0, 24, 16, false);
            AddPreview(grid, 2, 0, 48, 18, true);
            AddPreview(grid, 0, 1, 32, 32, false);
            AddPreview(grid, 1, 1, 48, 32, false);
            AddPreview(grid, 2, 1, 64, 32, true);
            AddPreview(grid, 0, 2, 48, 48, false);
            AddPreview(grid, 1, 2, 72, 48, false);
            AddPreview(grid, 2, 2, 80, 48, true);

            var bitmaps = new Grid();
            bitmaps.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bitmaps.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _backgroundView = new ThemeBitmapView
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _glyphView = new ThemeBitmapView
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_backgroundView, 0);
            Grid.SetColumn(_glyphView, 1);
            bitmaps.Children.Add(_backgroundView);
            bitmaps.Children.Add(_glyphView);

            Grid.SetColumn(bitmaps, 3);
            Grid.SetRow(bitmaps, 0);
            Grid.SetRowSpan(bitmaps, 3);
            grid.Children.Add(bitmaps);

            return grid;
        }

        private void AddPreview(Grid grid, int column, int row, double width, double height, bool drawText)
        {
            int index = column + row * 3;
            var preview = new ThemePreview
            {
                Width = width,
                Height = height,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            _previewDrawText[index] = drawText;
            _previews[index] = preview;

            Grid.SetColumn(preview, column);
            Grid.SetRow(preview, row);
            grid.Children.Add(preview);
        }

        private UIElement BuildPropertyList()
        {
            _properties = new ListView
            {
                Margin = new Thickness(0, 4, 0, 0),
                BorderThickness = new Thickness(1),
                Background = Brushes.White,
                SelectionMode = SelectionMode.Extended
            };

            var view = new GridView();
            _columns = new GridViewColumn[5];
            string[] members = { "Type", "Id", "Property", "Value", "Origin" };
            for (int i = 0; i < _columns.Length; i++)
            {
                _columns[i] = new GridViewColumn
                {
                    Width = 100,
                    DisplayMemberBinding = new Binding(members[i])
                };
                view.Columns.Add(_columns[i]);
            }

            _properties.View = view;

            _copySelectedItem = new MenuItem { Header = ToMenuHeader(Strings.MenuEditCopySelected) };
            _copySelectedItem.Click += delegate { CopyPropertyRows(false); };
            _copyAllItem = new MenuItem { Header = ToMenuHeader(Strings.MenuEditCopyAll) };
            _copyAllItem.Click += delegate { CopyPropertyRows(true); };
            _propertyMenu = new ContextMenu();
            _propertyMenu.Items.Add(_copySelectedItem);
            _propertyMenu.Items.Add(_copyAllItem);
            _properties.ContextMenu = _propertyMenu;
            _properties.PreviewKeyDown += OnPropertyListPreviewKeyDown;

            VirtualizingStackPanel.SetIsVirtualizing(_properties, true);
            VirtualizingStackPanel.SetVirtualizationMode(_properties, VirtualizationMode.Recycling);
            ScrollViewer.SetCanContentScroll(_properties, true);
            ScrollViewer.SetHorizontalScrollBarVisibility(_properties, ScrollBarVisibility.Disabled);

            return _properties;
        }

        private void OnPropertyListPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.C && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                CopyPropertyRows(false);
                e.Handled = true;
            }
        }

        private void CopyPropertyRows(bool all)
        {
            var lines = new List<string>();
            if (all)
            {
                foreach (object item in _properties.Items)
                {
                    var row = item as PropertyRow;
                    if (row != null)
                        lines.Add(FormatPropertyRow(row));
                }
            }
            else
            {
                foreach (object item in _properties.SelectedItems)
                {
                    var row = item as PropertyRow;
                    if (row != null)
                        lines.Add(FormatPropertyRow(row));
                }
            }

            if (lines.Count == 0)
                return;

            try
            {
                Clipboard.SetText(string.Join(Environment.NewLine, lines.ToArray()));
            }
            catch (Exception)
            {
                // Clipboard access can fail while another process owns it.
            }
        }

        private static string FormatPropertyRow(PropertyRow row)
        {
            return string.Join("\t", new string[]
            {
                row.Type, row.Id, row.Property, row.Value, row.Origin
            });
        }

        private void TryLoadIcon()
        {
            try
            {
                System.Reflection.Assembly entry = System.Reflection.Assembly.GetEntryAssembly();
                if (entry == null)
                    return;

                using (System.Drawing.Icon icon = System.Drawing.Icon.ExtractAssociatedIcon(entry.Location))
                {
                    if (icon == null)
                        return;

                    BitmapSource source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                        icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    source.Freeze();
                    Icon = source;
                }
            }
            catch (ArgumentException)
            {
            }
            catch (System.IO.IOException)
            {
            }
        }

        // ------------------------------------------------------------------
        // Localization
        // ------------------------------------------------------------------

        private void ApplyLocalization()
        {
            Title = Strings.AppTitle;

            _fileMenu.Header = ToMenuHeader(Strings.MenuFile);
            _fileExit.Header = ToMenuHeader(Strings.MenuFileExit);
            _viewMenu.Header = ToMenuHeader(Strings.MenuView);
            _showAllItem.Header = ToMenuHeader(Strings.MenuViewShowAll);
            _languageMenu.Header = ToMenuHeader(Strings.MenuViewLanguage);
            _englishItem.Header = ToMenuHeader(Strings.MenuLanguageEnglish);
            _chineseItem.Header = ToMenuHeader(Strings.MenuLanguageChinese);
            _helpMenu.Header = ToMenuHeader(Strings.MenuHelp);
            _aboutItem.Header = ToMenuHeader(Strings.MenuHelpAbout);
            _copySelectedItem.Header = ToMenuHeader(Strings.MenuEditCopySelected);
            _copyAllItem.Header = ToMenuHeader(Strings.MenuEditCopyAll);

            _englishItem.IsChecked = !UiCulture.IsChinese;
            _chineseItem.IsChecked = UiCulture.IsChinese;

            _classCaption.Text = Strings.LabelClass;
            _partCaption.Text = Strings.LabelPart;
            _stateCaption.Text = Strings.LabelState;

            _columns[0].Header = Strings.ColumnType;
            _columns[1].Header = Strings.ColumnId;
            _columns[2].Header = Strings.ColumnProperty;
            _columns[3].Header = Strings.ColumnValue;
            _columns[4].Header = Strings.ColumnOrigin;

            _backgroundView.ToolTip = Strings.TooltipBackgroundBitmap;
            _glyphView.ToolTip = Strings.TooltipGlyphBitmap;
            _backgroundView.SetEmptyText(Strings.NoBitmap);
            _glyphView.SetEmptyText(Strings.NoBitmap);

            FontFamily = new FontFamily(UiCulture.IsChinese ? "Microsoft YaHei UI" : "Segoe UI");

            AdjustColumnWidths();

            if (_currentClass != null)
                UpdateSelection(_currentClass, _currentPart, _currentState);
            else
                UpdateStatus();
        }

        private static string ToMenuHeader(string text)
        {
            // WPF uses '_' for access keys; the .resx files use the WinForms '&'.
            return text.Replace("&", "_");
        }

        // ------------------------------------------------------------------
        // Tree
        // ------------------------------------------------------------------

        private void PopulateTree()
        {
            _updating = true;
            _tree.Items.Clear();

            foreach (ThemeClassInfo cls in ThemeDatabase.Classes)
            {
                var classNode = new ThemeNode(cls, null);
                var classItem = new TreeViewItem { Header = cls.DisplayName, Tag = classNode };

                foreach (ThemePartInfo part in cls.Parts)
                {
                    var partNode = new ThemeNode(part, classNode);
                    var partItem = new TreeViewItem { Header = part.DisplayName, Tag = partNode };

                    foreach (ThemeStateInfo state in part.States)
                    {
                        var stateNode = new ThemeNode(state, partNode);
                        partItem.Items.Add(new TreeViewItem { Header = state.DisplayName, Tag = stateNode });
                    }

                    classItem.Items.Add(partItem);
                }

                _tree.Items.Add(classItem);
            }

            _updating = false;
        }

        private void SelectInitialNode()
        {
            TreeViewItem classItem = FindClassItem("Button");
            if (classItem == null)
            {
                if (_tree.Items.Count == 0)
                    return;

                classItem = (TreeViewItem)_tree.Items[0];
            }

            ThemeClassInfo cls = ((ThemeNode)classItem.Tag).Model as ThemeClassInfo;

            TreeViewItem partItem = null;
            TreeViewItem stateItem = null;
            ThemePartInfo part = null;
            ThemeStateInfo state = null;

            foreach (object child in classItem.Items)
            {
                var candidate = child as TreeViewItem;
                if (candidate == null)
                    continue;

                var candidatePart = ((ThemeNode)candidate.Tag).Model as ThemePartInfo;
                if (candidatePart == null)
                    continue;

                partItem = candidate;
                part = candidatePart;
                if (candidate.Items.Count > 0)
                {
                    stateItem = (TreeViewItem)candidate.Items[0];
                    state = ((ThemeNode)stateItem.Tag).Model as ThemeStateInfo;
                }
                break;
            }

            _updating = true;
            classItem.IsExpanded = true;
            if (partItem != null)
            {
                partItem.IsExpanded = true;
                if (stateItem != null)
                    stateItem.IsSelected = true;
                else
                    partItem.IsSelected = true;
            }
            else
            {
                classItem.IsSelected = true;
            }
            _updating = false;

            classItem.BringIntoView();
            UpdateSelection(cls, part, state);
        }

        private TreeViewItem FindClassItem(string className)
        {
            foreach (object child in _tree.Items)
            {
                var item = child as TreeViewItem;
                if (item == null)
                    continue;

                var cls = ((ThemeNode)item.Tag).Model as ThemeClassInfo;
                if (cls != null && string.Equals(cls.Name, className, StringComparison.Ordinal))
                    return item;
            }

            return null;
        }

        private void OnTreeSelected(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (_updating)
                return;

            var item = e.NewValue as TreeViewItem;
            if (item == null)
                return;

            var node = item.Tag as ThemeNode;
            ThemeClassInfo cls = null;
            ThemePartInfo part = null;
            ThemeStateInfo state = null;

            for (ThemeNode current = node; current != null; current = current.Parent)
            {
                if (cls == null && current.Model is ThemeClassInfo)
                    cls = (ThemeClassInfo)current.Model;
                else if (part == null && current.Model is ThemePartInfo)
                    part = (ThemePartInfo)current.Model;
                else if (state == null && current.Model is ThemeStateInfo)
                    state = (ThemeStateInfo)current.Model;
            }

            if (cls != null)
                UpdateSelection(cls, part, state);
        }

        // ------------------------------------------------------------------
        // Selection
        // ------------------------------------------------------------------

        private void UpdateSelection(ThemeClassInfo cls, ThemePartInfo part, ThemeStateInfo state)
        {
            _currentClass = cls;
            _currentPart = part;
            _currentState = state;

            int partId = part != null ? part.Id : 0;
            int stateId = state != null ? state.Id : 0;

            _classValue.Text = cls.DisplayName;
            _partValue.Text = string.Format(Strings.PartTitle, partId,
                part != null ? part.DisplayName : string.Empty);
            _stateValue.Text = string.Format(Strings.StateTitle, stateId,
                state != null ? state.DisplayName : string.Empty);

            VisualStyle style = null;
            try
            {
                style = _context.Open(cls.Name, cls.SubClass);
            }
            catch (Exception exception)
            {
                MessageBox.Show(this,
                    string.Format(Strings.ErrorOpenTheme, cls.DisplayName, exception.Message),
                    Strings.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }

            using (style)
            {
                UpdatePreviewImages(style, partId, stateId);
                UpdateBitmaps(style, partId, stateId);
                PopulateProperties(style, partId, stateId);
            }

            _hasTheme = style != null;
            UpdateStatus();
        }

        private void UpdatePreviewImages(VisualStyle style, int partId, int stateId)
        {
            for (int i = 0; i < _previews.Length; i++)
            {
                ThemePreview preview = _previews[i];
                BitmapSource image = style == null
                    ? null
                    : ThemeRenderService.RenderPartSource(style, partId, stateId,
                        preview.Width, preview.Height, _previewDrawText[i],
                        Strings.SampleText, ControlBackground);

                preview.SetImage(image);
            }
        }

        private void UpdateBitmaps(VisualStyle style, int partId, int stateId)
        {
            if (style == null)
            {
                _backgroundView.SetImage(null);
                _glyphView.SetImage(null);
                return;
            }

            _backgroundView.SetImage(ThemeRenderService.RenderBitmap(style, partId, stateId,
                ThemeBitmapKind.Background));
            _glyphView.SetImage(ThemeRenderService.RenderBitmap(style, partId, stateId,
                ThemeBitmapKind.Glyph));
        }

        private void PopulateProperties(VisualStyle style, int partId, int stateId)
        {
            var rows = new List<PropertyRow>();

            if (style != null)
            {
                foreach (ThemePropertyInfo property in ThemeDatabase.Properties)
                {
                    ThemePropertyValue value;
                    bool hasValue;
                    try
                    {
                        hasValue = style.TryGetProperty(partId, stateId, property, out value);
                    }
                    catch (Exception)
                    {
                        hasValue = false;
                        value = null;
                    }

                    ThemePropertyOrigin origin = style.GetPropertyOrigin(partId, stateId, property.Id);
                    if (!hasValue && !_showAllProperties
                        && origin != ThemePropertyOrigin.State
                        && origin != ThemePropertyOrigin.Part
                        && origin != ThemePropertyOrigin.Class)
                    {
                        continue;
                    }

                    rows.Add(new PropertyRow
                    {
                        Type = PropertyValueFormatter.GetTypeName(property.Type),
                        Id = property.Id.ToString(CultureInfo.InvariantCulture),
                        Property = property.Name,
                        Value = hasValue ? PropertyValueFormatter.Format(value) : string.Empty,
                        Origin = GetOriginText(origin, hasValue)
                    });
                }
            }

            _properties.ItemsSource = rows;
            AdjustColumnWidths();
        }

        private static string GetOriginText(ThemePropertyOrigin origin, bool hasValue)
        {
            if (!hasValue && origin != ThemePropertyOrigin.State
                && origin != ThemePropertyOrigin.Part && origin != ThemePropertyOrigin.Class)
            {
                return string.Empty;
            }

            return PropertyValueFormatter.GetOriginName(origin);
        }

        // ------------------------------------------------------------------
        // Status and layout
        // ------------------------------------------------------------------

        private void UpdateStatus()
        {
            if (!ThemeSystem.IsVisualStylesEnabled)
            {
                _statusItem.Content = Strings.StatusThemeInactive;
                return;
            }

            if (_currentClass == null)
            {
                _statusItem.Content = Strings.StatusReady;
                return;
            }

            if (!_hasTheme)
            {
                _statusItem.Content = Strings.StatusNoTheme;
                return;
            }

            ThemeInfo info = ThemeSystem.GetCurrentTheme();
            _statusItem.Content = string.Format(Strings.StatusTheme, info.ShortFileName);
        }

        private void AdjustColumnWidths()
        {
            if (_properties == null || _columns == null || _columns.Length != 5)
                return;

            double width = _properties.ActualWidth - SystemParameters.VerticalScrollBarWidth - 20;
            if (width <= 0)
                return;

            double[] percentages = { 13, 8, 32, 30, 17 };
            for (int i = 0; i < _columns.Length; i++)
                _columns[i].Width = Math.Max(60, width * percentages[i] / 100.0);
        }

        // ------------------------------------------------------------------
        // Commands
        // ------------------------------------------------------------------

        private void OnShowAllClick(object sender, RoutedEventArgs e)
        {
            _showAllProperties = _showAllItem.IsChecked;
            if (_currentClass != null)
                UpdateSelection(_currentClass, _currentPart, _currentState);
        }

        private void OnAboutClick(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(this, Strings.AboutText, Strings.AboutTitle,
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SetLanguage(string cultureName)
        {
            UiCulture.Set(CultureInfo.GetCultureInfo(cultureName));
            ApplyLocalization();
        }

        // ------------------------------------------------------------------
        // Automation helpers used by --selftest
        // ------------------------------------------------------------------

        internal string StatusText
        {
            get { return _statusItem.Content as string ?? string.Empty; }
        }

        internal int PropertyRowCount
        {
            get { return _properties.Items.Count; }
        }

        internal string ClassText
        {
            get { return _classValue.Text; }
        }

        internal string PartText
        {
            get { return _partValue.Text; }
        }

        internal string StateText
        {
            get { return _stateValue.Text; }
        }

        internal string LayoutSummary
        {
            get
            {
                return string.Format("{0}x{1}; preview={2}x{3}; dpi={4:0.##}",
                    ActualWidth, ActualHeight, _previews[8].Width, _previews[8].Height,
                    ThemeRenderService.Scale);
            }
        }

        internal void SelectTheme(string className, int partId, int stateId)
        {
            TreeViewItem classItem = FindClassItem(className);
            if (classItem == null)
                throw new ArgumentException("Theme class '" + className + "' was not found.", nameof(className));

            ThemeClassInfo cls = (ThemeClassInfo)((ThemeNode)classItem.Tag).Model;
            ThemePartInfo part = null;
            ThemeStateInfo state = null;
            TreeViewItem partItem = null;
            TreeViewItem stateItem = null;

            foreach (object child in classItem.Items)
            {
                var candidate = child as TreeViewItem;
                if (candidate == null)
                    continue;

                var candidatePart = ((ThemeNode)candidate.Tag).Model as ThemePartInfo;
                if (candidatePart == null || candidatePart.Id != partId)
                    continue;

                part = candidatePart;
                partItem = candidate;
                foreach (object stateChild in candidate.Items)
                {
                    var candidateStateItem = stateChild as TreeViewItem;
                    if (candidateStateItem == null)
                        continue;

                    var candidateState = ((ThemeNode)candidateStateItem.Tag).Model as ThemeStateInfo;
                    if (candidateState != null && candidateState.Id == stateId)
                    {
                        state = candidateState;
                        stateItem = candidateStateItem;
                        break;
                    }
                }
                break;
            }

            _updating = true;
            classItem.IsExpanded = true;
            if (partItem != null)
            {
                partItem.IsExpanded = true;
                if (stateItem != null)
                    stateItem.IsSelected = true;
                else
                    partItem.IsSelected = true;
            }
            else
            {
                classItem.IsSelected = true;
            }
            _updating = false;

            if (stateItem != null)
                stateItem.BringIntoView();
            else if (partItem != null)
                partItem.BringIntoView();
            else
                classItem.BringIntoView();

            UpdateSelection(cls, part, state);
        }

        internal void ChangeLanguage(string cultureName)
        {
            SetLanguage(cultureName);
        }

        internal void SetShowAllProperties(bool value)
        {
            _showAllItem.IsChecked = value;
            _showAllProperties = value;
            if (_currentClass != null)
                UpdateSelection(_currentClass, _currentPart, _currentState);
        }

        internal void ScrollTreeToSelection()
        {
            var item = _tree.SelectedItem as TreeViewItem;
            if (item == null)
                return;

            for (var node = item.Tag as ThemeNode; node != null; node = node.Parent)
            {
                var ancestor = FindItemForNode(node);
                if (ancestor != null)
                    ancestor.IsExpanded = true;
            }

            item.BringIntoView();
        }

        private TreeViewItem FindItemForNode(ThemeNode node)
        {
            if (node == null)
                return null;

            ThemeNode parent = node.Parent;
            TreeViewItem parentItem = parent == null ? null : FindItemForNode(parent);
            IEnumerable items = parentItem == null ? _tree.Items : parentItem.Items;
            foreach (object child in items)
            {
                var item = child as TreeViewItem;
                if (item != null && ReferenceEquals(item.Tag, node))
                    return item;
            }

            return null;
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            foreach (ThemePreview preview in _previews)
                preview.SetImage(null);

            _backgroundView.SetImage(null);
            _glyphView.SetImage(null);
            _properties.ItemsSource = null;

            _context.Dispose();
        }

        private sealed class ThemeNode
        {
            internal ThemeNode(object model, ThemeNode parent)
            {
                Model = model;
                Parent = parent;
            }

            internal object Model { get; }

            internal ThemeNode Parent { get; }
        }
    }
}
