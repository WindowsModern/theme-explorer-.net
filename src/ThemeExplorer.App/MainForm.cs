using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ThemeExplorer.Core;
using ThemeExplorer.Ui;

namespace ThemeExplorer.App
{
    /// <summary>
    /// Main window.  Reproduces the layout of the original Theme Explorer: a class
    /// tree on the left, previews and bitmaps on the top right, and the property
    /// list below them.
    /// </summary>
    internal sealed class MainForm : Form
    {
        private const int WmSettingChange = 0x001A;
        private const int WmThemeChanged = 0x031A;
        private const int WmSysColorChange = 0x0015;
        private const int WmDwmColorizationColorChanged = 0x0320;

        private readonly VisualStyleContext _context;
        private readonly ToolTip _toolTip = new ToolTip();

        private TreeView _tree;
        private SplitContainer _split;
        private Label _classCaption;
        private Label _partCaption;
        private Label _stateCaption;
        private Label _classValue;
        private Label _partValue;
        private Label _stateValue;
        private ThemePreviewControl[] _previews;
        private BitmapPreview _backgroundBitmap;
        private BitmapPreview _glyphBitmap;
        private BufferedListView _properties;
        private StatusStrip _status;
        private ToolStripStatusLabel _statusLabel;
        private MenuStrip _menu;
        private ToolStripMenuItem _fileMenu;
        private ToolStripMenuItem _fileExit;
        private ToolStripMenuItem _viewMenu;
        private ToolStripMenuItem _showAllItem;
        private ToolStripMenuItem _autoUpdateItem;
        private ToolStripMenuItem _languageMenu;
        private ToolStripMenuItem _englishItem;
        private ToolStripMenuItem _chineseItem;
        private ToolStripMenuItem _helpMenu;
        private ToolStripMenuItem _aboutItem;
        private ContextMenuStrip _propertyMenu;
        private ToolStripMenuItem _copySelectedItem;
        private ToolStripMenuItem _copyAllItem;

        private VisualStyle _style;
        private ThemeClassInfo _class;
        private ThemePartInfo _partInfo;
        private ThemeStateInfo _stateInfo;
        private int _partId;
        private int _stateId;
        private bool _showAllProperties;
        private bool _buildingTree;
        private bool _autoUpdateTheme = true;
        private readonly Timer _themeChangeTimer = new Timer();

        internal MainForm()
        {
            _context = new VisualStyleContext();
            _themeChangeTimer.Interval = 250;
            _themeChangeTimer.Tick += OnThemeChangeTimerTick;
            TryLoadApplicationIcon();

            BuildUi();
            ApplyLocalization();
            PopulateTree();
            SelectInitialNode();
        }

        private void TryLoadApplicationIcon()
        {
            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch (ArgumentException)
            {
                // Keep the default icon if the executable has none.
            }
            catch (System.IO.IOException)
            {
            }
        }

        // ------------------------------------------------------------------
        // UI construction
        // ------------------------------------------------------------------

        private void BuildUi()
        {
            SuspendLayout();

            Text = Strings.AppTitle;
            Font = UiFont.Get();
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(940, 640);
            MinimumSize = new Size(780, 500);
            KeyPreview = true;

            BuildMenu();

            _tree = new TreeView
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                HideSelection = false,
                ShowLines = true,
                ShowRootLines = true,
                ShowPlusMinus = true,
                FullRowSelect = false
            };
            _tree.AfterSelect += OnTreeAfterSelect;

            _split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterWidth = 5
            };
            _split.Panel1.Padding = new Padding(4, 4, 0, 4);
            _split.Panel1.Controls.Add(_tree);
            _split.Panel2.Padding = new Padding(8);
            _split.Panel2.Controls.Add(BuildDetailsPanel());

            _status = new StatusStrip { SizingGrip = true };
            _statusLabel = new ToolStripStatusLabel
            {
                Spring = true,
                TextAlign = ContentAlignment.MiddleLeft
            };
            _status.Items.Add(_statusLabel);

            Controls.Add(_split);
            Controls.Add(_status);
            Controls.Add(_menu);
            MainMenuStrip = _menu;

            ResumeLayout(false);
            PerformLayout();
        }

        private void BuildMenu()
        {
            _menu = new MenuStrip();

            _fileMenu = new ToolStripMenuItem();
            _fileExit = new ToolStripMenuItem();
            _fileMenu.DropDownItems.Add(_fileExit);

            _viewMenu = new ToolStripMenuItem();
            _showAllItem = new ToolStripMenuItem { CheckOnClick = true };
            _showAllItem.CheckedChanged += OnShowAllChanged;
            _autoUpdateItem = new ToolStripMenuItem { CheckOnClick = true, Checked = _autoUpdateTheme };
            _autoUpdateItem.CheckedChanged += OnAutoUpdateChanged;
            _languageMenu = new ToolStripMenuItem();
            _englishItem = new ToolStripMenuItem();
            _chineseItem = new ToolStripMenuItem();
            _languageMenu.DropDownItems.Add(_englishItem);
            _languageMenu.DropDownItems.Add(_chineseItem);
            _viewMenu.DropDownItems.Add(_showAllItem);
            _viewMenu.DropDownItems.Add(_autoUpdateItem);
            _viewMenu.DropDownItems.Add(new ToolStripSeparator());
            _viewMenu.DropDownItems.Add(_languageMenu);

            _helpMenu = new ToolStripMenuItem();
            _aboutItem = new ToolStripMenuItem();
            _helpMenu.DropDownItems.Add(_aboutItem);

            _menu.Items.Add(_fileMenu);
            _menu.Items.Add(_viewMenu);
            _menu.Items.Add(_helpMenu);

            _fileExit.Click += OnExitClick;
            _englishItem.Click += OnEnglishClick;
            _chineseItem.Click += OnChineseClick;
            _aboutItem.Click += OnAboutClick;
        }

        private Control BuildDetailsPanel()
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Margin = new Padding(0)
            };
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 204));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            panel.Controls.Add(BuildHeaderPanel(), 0, 0);
            panel.Controls.Add(BuildPreviewPanel(), 0, 1);
            panel.Controls.Add(BuildPropertyList(), 0, 2);

            return panel;
        }

        private Control BuildHeaderPanel()
        {
            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 3,
                Margin = new Padding(0)
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            for (int i = 0; i < 3; i++)
                header.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));

            _classCaption = CreateCaption();
            _partCaption = CreateCaption();
            _stateCaption = CreateCaption();
            _classValue = CreateValueLabel();
            _partValue = CreateValueLabel();
            _stateValue = CreateValueLabel();

            header.Controls.Add(_classCaption, 0, 0);
            header.Controls.Add(_classValue, 1, 0);
            header.Controls.Add(_partCaption, 0, 1);
            header.Controls.Add(_partValue, 1, 1);
            header.Controls.Add(_stateCaption, 0, 2);
            header.Controls.Add(_stateValue, 1, 2);

            return header;
        }

        private static Label CreateCaption()
        {
            return new Label
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 0, 6, 0)
            };
        }

        private Label CreateValueLabel()
        {
            return new Label
            {
                AutoSize = false,
                AutoEllipsis = true,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font(Font, FontStyle.Bold),
                Margin = new Padding(0)
            };
        }

        private Control BuildPreviewPanel()
        {
            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 5,
                RowCount = 3,
                Margin = new Padding(0, 0, 0, 6)
            };

            for (int i = 0; i < 3; i++)
            {
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
                grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            }
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            _previews = new ThemePreviewControl[9];
            AddPreview(grid, 0, 0, 16, 16, false);
            AddPreview(grid, 1, 0, 24, 16, false);
            AddPreview(grid, 2, 0, 48, 18, true);
            AddPreview(grid, 0, 1, 32, 32, false);
            AddPreview(grid, 1, 1, 48, 32, false);
            AddPreview(grid, 2, 1, 64, 32, true);
            AddPreview(grid, 0, 2, 48, 48, false);
            AddPreview(grid, 1, 2, 72, 48, false);
            AddPreview(grid, 2, 2, 80, 48, true);

            var bitmaps = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0)
            };
            bitmaps.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            bitmaps.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            _backgroundBitmap = CreateBitmapBox();
            _glyphBitmap = CreateBitmapBox();
            bitmaps.Controls.Add(_backgroundBitmap, 0, 0);
            bitmaps.Controls.Add(_glyphBitmap, 1, 0);

            grid.Controls.Add(bitmaps, 3, 0);
            grid.SetRowSpan(bitmaps, 3);

            return grid;
        }

        private static BitmapPreview CreateBitmapBox()
        {
            return new BitmapPreview
            {
                Size = new Size(80, 80),
                Anchor = AnchorStyles.None,
                Margin = new Padding(2)
            };
        }

        private void AddPreview(TableLayoutPanel grid, int column, int row, int width, int height, bool drawText)
        {
            var preview = new ThemePreviewControl(drawText)
            {
                Size = new Size(width, height),
                Anchor = AnchorStyles.None,
                Margin = new Padding(2)
            };

            grid.Controls.Add(preview, column, row);
            _previews[column + row * 3] = preview;
        }

        private Control BuildPropertyList()
        {
            _properties = new BufferedListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                HideSelection = false,
                MultiSelect = true,
                Margin = new Padding(0, 4, 0, 0)
            };

            for (int i = 0; i < 5; i++)
                _properties.Columns.Add(string.Empty, 80);

            _properties.Resize += OnPropertyListResize;
            _properties.KeyDown += OnPropertyListKeyDown;

            _propertyMenu = new ContextMenuStrip();
            _copySelectedItem = new ToolStripMenuItem(Strings.MenuEditCopySelected);
            _copySelectedItem.Click += delegate { CopyPropertyRows(false); };
            _copyAllItem = new ToolStripMenuItem(Strings.MenuEditCopyAll);
            _copyAllItem.Click += delegate { CopyPropertyRows(true); };
            _propertyMenu.Items.Add(_copySelectedItem);
            _propertyMenu.Items.Add(_copyAllItem);
            _properties.ContextMenuStrip = _propertyMenu;

            return _properties;
        }

        // ------------------------------------------------------------------
        // Localisation
        // ------------------------------------------------------------------

        private void ApplyLocalization()
        {
            Text = Strings.AppTitle;

            _fileMenu.Text = Strings.MenuFile;
            _fileExit.Text = Strings.MenuFileExit;
            _viewMenu.Text = Strings.MenuView;
            _showAllItem.Text = Strings.MenuViewShowAll;
            _autoUpdateItem.Text = Strings.MenuViewAutoUpdate;
            _languageMenu.Text = Strings.MenuViewLanguage;
            _englishItem.Text = Strings.MenuLanguageEnglish;
            _chineseItem.Text = Strings.MenuLanguageChinese;
            _helpMenu.Text = Strings.MenuHelp;
            _aboutItem.Text = Strings.MenuHelpAbout;

            _englishItem.Checked = !UiCulture.IsChinese;
            _chineseItem.Checked = UiCulture.IsChinese;

            _classCaption.Text = Strings.LabelClass;
            _partCaption.Text = Strings.LabelPart;
            _stateCaption.Text = Strings.LabelState;

            _properties.Columns[0].Text = Strings.ColumnType;
            _properties.Columns[1].Text = Strings.ColumnId;
            _properties.Columns[2].Text = Strings.ColumnProperty;
            _properties.Columns[3].Text = Strings.ColumnValue;
            _properties.Columns[4].Text = Strings.ColumnOrigin;
            _copySelectedItem.Text = Strings.MenuEditCopySelected;
            _copyAllItem.Text = Strings.MenuEditCopyAll;

            _toolTip.SetToolTip(_backgroundBitmap, Strings.TooltipBackgroundBitmap);
            _toolTip.SetToolTip(_glyphBitmap, Strings.TooltipGlyphBitmap);
            _backgroundBitmap.EmptyText = Strings.NoBitmap;
            _glyphBitmap.EmptyText = Strings.NoBitmap;

            if (_class != null)
                PopulateProperties(_style);

            if (_previews != null)
            {
                foreach (ThemePreviewControl preview in _previews)
                    preview.Invalidate();
            }

            LocalizeTree();
            AdjustColumnWidths();
            UpdateStatus();
        }

        private void LocalizeTree()
        {
            if (_tree == null)
                return;

            _buildingTree = true;
            _tree.BeginUpdate();
            try
            {
                foreach (TreeNode classNode in _tree.Nodes)
                {
                    var cls = classNode.Tag as ThemeClassInfo;
                    if (cls != null)
                        classNode.Text = ThemeNameLocalizer.Class(cls);

                    foreach (TreeNode partNode in classNode.Nodes)
                    {
                        var part = partNode.Tag as ThemePartInfo;
                        if (part != null)
                            partNode.Text = ThemeNameLocalizer.Part(part);

                        foreach (TreeNode stateNode in partNode.Nodes)
                        {
                            var state = stateNode.Tag as ThemeStateInfo;
                            if (state != null)
                                stateNode.Text = ThemeNameLocalizer.State(state);
                        }
                    }
                }
            }
            finally
            {
                _tree.EndUpdate();
                _buildingTree = false;
            }

            if (_class != null)
            {
                _classValue.Text = ThemeNameLocalizer.Class(_class);
                _partValue.Text = string.Format(Strings.PartTitle, _partId, ThemeNameLocalizer.Part(_partInfo));
                _stateValue.Text = string.Format(Strings.StateTitle, _stateId, ThemeNameLocalizer.State(_stateInfo));
            }
        }

        // ------------------------------------------------------------------
        // Tree and selection
        // ------------------------------------------------------------------

        private void PopulateTree()
        {
            _buildingTree = true;
            _tree.BeginUpdate();
            try
            {
                _tree.Nodes.Clear();
                foreach (ThemeClassInfo cls in ThemeDatabase.Classes)
                {
                    var classNode = new TreeNode(ThemeNameLocalizer.Class(cls)) { Tag = cls };
                    foreach (ThemePartInfo part in cls.Parts)
                    {
                        var partNode = new TreeNode(ThemeNameLocalizer.Part(part)) { Tag = part };
                        foreach (ThemeStateInfo state in part.States)
                            partNode.Nodes.Add(new TreeNode(ThemeNameLocalizer.State(state)) { Tag = state });
                        classNode.Nodes.Add(partNode);
                    }
                    _tree.Nodes.Add(classNode);
                }
            }
            finally
            {
                _tree.EndUpdate();
                _buildingTree = false;
            }
        }

        private void SelectInitialNode()
        {
            TreeNode target = null;

            foreach (TreeNode classNode in _tree.Nodes)
            {
                var cls = classNode.Tag as ThemeClassInfo;
                if (cls == null || cls.Name != "Button")
                    continue;

                foreach (TreeNode partNode in classNode.Nodes)
                {
                    var part = partNode.Tag as ThemePartInfo;
                    if (part == null || part.Id != 1)
                        continue;

                    target = partNode.Nodes.Count > 0 ? partNode.Nodes[0] : partNode;
                    break;
                }
                break;
            }

            if (target == null && _tree.Nodes.Count > 0)
                target = _tree.Nodes[0];

            if (target != null)
            {
                _tree.SelectedNode = target;
                target.EnsureVisible();
            }
        }

        private void OnTreeAfterSelect(object sender, TreeViewEventArgs e)
        {
            if (_buildingTree || e.Node == null)
                return;

            ThemeClassInfo cls = null;
            ThemePartInfo part = null;
            ThemeStateInfo state = null;

            for (TreeNode node = e.Node; node != null; node = node.Parent)
            {
                if (cls == null && node.Tag is ThemeClassInfo)
                    cls = (ThemeClassInfo)node.Tag;
                else if (part == null && node.Tag is ThemePartInfo)
                    part = (ThemePartInfo)node.Tag;
                else if (state == null && node.Tag is ThemeStateInfo)
                    state = (ThemeStateInfo)node.Tag;
            }

            if (cls == null)
                return;

            int partId = part != null ? part.Id : 0;
            int stateId = state != null ? state.Id : 0;

            VisualStyle style = null;
            try
            {
                style = _context.Open(cls.Name, cls.SubClass);
            }
            catch (Exception exception)
            {
                MessageBox.Show(this,
                    string.Format(Strings.ErrorOpenTheme, cls.DisplayName, exception.Message),
                    Strings.ErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            _class = cls;
            _partInfo = part;
            _stateInfo = state;
            _partId = partId;
            _stateId = stateId;

            _classValue.Text = ThemeNameLocalizer.Class(cls);
            _partValue.Text = string.Format(Strings.PartTitle, partId, ThemeNameLocalizer.Part(part));
            _stateValue.Text = string.Format(Strings.StateTitle, stateId, ThemeNameLocalizer.State(state));

            foreach (ThemePreviewControl preview in _previews)
                preview.UpdateStyle(style, partId, stateId);

            UpdateBitmaps(style);
            PopulateProperties(style);

            VisualStyle previous = _style;
            _style = style;
            if (previous != null)
                previous.Dispose();

            UpdateStatus();
        }

        private void UpdateBitmaps(VisualStyle style)
        {
            if (style == null)
            {
                _backgroundBitmap.Image = null;
                _glyphBitmap.Image = null;
                return;
            }

            _backgroundBitmap.Image = ThemeRenderer.GetBitmap(style, _partId, _stateId, ThemeBitmapKind.Background);
            _glyphBitmap.Image = ThemeRenderer.GetBitmap(style, _partId, _stateId, ThemeBitmapKind.Glyph);
        }

        // ------------------------------------------------------------------
        // Properties
        // ------------------------------------------------------------------

        private void PopulateProperties(VisualStyle style)
        {
            _properties.BeginUpdate();
            try
            {
                _properties.Items.Clear();

                if (style == null)
                    return;

                foreach (ThemePropertyInfo property in ThemeDatabase.Properties)
                {
                    ThemePropertyValue value;
                    bool hasValue;
                    try
                    {
                        hasValue = style.TryGetProperty(_partId, _stateId, property, out value);
                    }
                    catch (Exception)
                    {
                        hasValue = false;
                        value = null;
                    }

                    ThemePropertyOrigin origin = style.GetPropertyOrigin(_partId, _stateId, property.Id);

                    // Modern Windows reports PO_GLOBAL even for properties that do
                    // not exist, so by default show only rows that actually carry a
                    // value (the "show all" menu item restores the raw behaviour).
                    if (!hasValue && !_showAllProperties
                        && origin != ThemePropertyOrigin.State
                        && origin != ThemePropertyOrigin.Part
                        && origin != ThemePropertyOrigin.Class)
                    {
                        continue;
                    }

                    var item = new ListViewItem(PropertyValueFormatter.GetTypeName(property.Type));
                    item.SubItems.Add(property.Id.ToString(CultureInfo.InvariantCulture));
                    item.SubItems.Add(property.Name);
                    item.SubItems.Add(hasValue ? PropertyValueFormatter.Format(value) : string.Empty);
                    item.SubItems.Add(GetOriginText(origin, hasValue));
                    _properties.Items.Add(item);
                }
            }
            finally
            {
                _properties.EndUpdate();
            }
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

        private void OnShowAllChanged(object sender, EventArgs e)
        {
            _showAllProperties = _showAllItem.Checked;
            if (_class != null)
                PopulateProperties(_style);
        }

        private void OnAutoUpdateChanged(object sender, EventArgs e)
        {
            _autoUpdateTheme = _autoUpdateItem.Checked;
            if (!_autoUpdateTheme)
                _themeChangeTimer.Stop();
        }

        private void OnThemeChangeTimerTick(object sender, EventArgs e)
        {
            _themeChangeTimer.Stop();
            RefreshTheme();
        }

        private void ScheduleThemeRefresh()
        {
            if (!_autoUpdateTheme || _buildingTree || IsDisposed || Disposing)
                return;

            _themeChangeTimer.Stop();
            _themeChangeTimer.Start();
        }

        private void RefreshTheme()
        {
            if (!_autoUpdateTheme || _buildingTree || _tree == null)
                return;

            // Re-open the style for the node that is currently selected.  The
            // tree itself is deliberately not rebuilt, so the selection and
            // expansion state stay exactly where the user left them.
            TreeNode selected = _tree.SelectedNode;
            if (selected == null)
                return;

            OnTreeAfterSelect(this, new TreeViewEventArgs(selected));
        }

        // ------------------------------------------------------------------
        // Status bar
        // ------------------------------------------------------------------

        private void UpdateStatus()
        {
            if (!ThemeSystem.IsVisualStylesEnabled)
            {
                _statusLabel.Text = Strings.StatusThemeInactive;
                return;
            }

            if (_class == null)
            {
                _statusLabel.Text = Strings.StatusReady;
                return;
            }

            if (_style == null)
            {
                _statusLabel.Text = Strings.StatusNoTheme;
                return;
            }

            ThemeInfo info = ThemeSystem.GetCurrentTheme();
            _statusLabel.Text = string.Format(Strings.StatusTheme, info.ShortFileName);
        }

        // ------------------------------------------------------------------
        // Layout helpers
        // ------------------------------------------------------------------

        private void OnPropertyListKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.C)
            {
                CopyPropertyRows(false);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void CopyPropertyRows(bool all)
        {
            var rows = new List<string>();
            if (all)
            {
                foreach (ListViewItem item in _properties.Items)
                    rows.Add(FormatPropertyRow(item));
            }
            else
            {
                foreach (ListViewItem item in _properties.SelectedItems)
                    rows.Add(FormatPropertyRow(item));
            }

            if (rows.Count == 0)
                return;

            try
            {
                Clipboard.SetText(string.Join(Environment.NewLine, rows.ToArray()));
            }
            catch (ExternalException)
            {
                // The clipboard can be locked by another process; copying is best effort.
            }
        }

        private static string FormatPropertyRow(ListViewItem item)
        {
            var cells = new string[item.SubItems.Count];
            for (int i = 0; i < item.SubItems.Count; i++)
                cells[i] = item.SubItems[i].Text;
            return string.Join("\t", cells);
        }

        private void OnPropertyListResize(object sender, EventArgs e)
        {
            AdjustColumnWidths();
        }

        private void AdjustColumnWidths()
        {
            if (_properties == null || _properties.Columns.Count != 5)
                return;

            int width = _properties.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 4;
            if (width <= 0)
                return;

            int[] percentages = { 13, 8, 33, 29, 17 };
            int[] minimums = { 70, 45, 180, 130, 70 };

            for (int i = 0; i < 5; i++)
            {
                int columnWidth = width * percentages[i] / 100;
                if (columnWidth < minimums[i])
                    columnWidth = minimums[i];
                _properties.Columns[i].Width = columnWidth;
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            _split.Panel1MinSize = 170;
            _split.Panel2MinSize = 300;

            // Proportional split so the tree keeps its relative width at any DPI.
            int maximum = _split.Width - _split.Panel2MinSize - _split.SplitterWidth;
            int distance = (int)(_split.Width * 0.27);
            if (distance > maximum)
                distance = maximum;
            if (distance < _split.Panel1MinSize)
                distance = _split.Panel1MinSize;

            _split.SplitterDistance = distance;
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            switch (m.Msg)
            {
                case WmThemeChanged:
                case WmSysColorChange:
                case WmDwmColorizationColorChanged:
                    ScheduleThemeRefresh();
                    break;
                case WmSettingChange:
                    string setting = m.LParam == IntPtr.Zero
                        ? null
                        : Marshal.PtrToStringUni(m.LParam);
                    if (string.IsNullOrEmpty(setting) ||
                        string.Equals(setting, "ImmersiveColorSet", StringComparison.Ordinal) ||
                        string.Equals(setting, "WindowMetrics", StringComparison.Ordinal))
                    {
                        ScheduleThemeRefresh();
                    }
                    break;
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            AdjustColumnWidths();
        }

        // ------------------------------------------------------------------
        // Command handlers
        // ------------------------------------------------------------------

        private void OnExitClick(object sender, EventArgs e)
        {
            Close();
        }

        private void OnEnglishClick(object sender, EventArgs e)
        {
            SetLanguage("en");
        }

        private void OnChineseClick(object sender, EventArgs e)
        {
            SetLanguage("zh-Hans");
        }

        private void SetLanguage(string cultureName)
        {
            UiCulture.Set(CultureInfo.GetCultureInfo(cultureName));
            ApplyLocalization();
        }

        private void OnAboutClick(object sender, EventArgs e)
        {
            MessageBox.Show(this, Strings.AboutText, Strings.AboutTitle,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ------------------------------------------------------------------
        // Automation support for the --selftest switch.
        // ------------------------------------------------------------------

        internal bool AutoUpdateTheme
        {
            get { return _autoUpdateItem.Checked; }
        }

        internal string AutoUpdateThemeText
        {
            get { return _autoUpdateItem.Text; }
        }

        internal void SetAutoUpdateTheme(bool enabled)
        {
            _autoUpdateItem.Checked = enabled;
        }

        internal string SelectedTreeNodeText
        {
            get { return _tree.SelectedNode == null ? string.Empty : _tree.SelectedNode.Text; }
        }

        internal void SimulateThemeChange()
        {
            RefreshTheme();
        }

        internal string StatusText
        {
            get { return _statusLabel.Text; }
        }

        internal string LayoutSummary
        {
            get
            {
                return string.Format("{0}x{1}; preview={2}x{3}; bitmap={4}x{5}",
                    ClientSize.Width, ClientSize.Height,
                    _previews[8].Width, _previews[8].Height,
                    _backgroundBitmap.Width, _backgroundBitmap.Height);
            }
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

        internal void ChangeLanguage(string cultureName)
        {
            SetLanguage(cultureName);
        }

        internal void SetShowAllProperties(bool value)
        {
            _showAllItem.Checked = value;
        }

        internal void ScrollTreeToSelection()
        {
            TreeNode node = _tree.SelectedNode;
            if (node == null)
                return;

            while (node.Parent != null)
                node = node.Parent;

            _tree.TopNode = node;
        }

        internal void SelectTheme(string className, int partId, int stateId)
        {
            if (string.IsNullOrEmpty(className))
                throw new ArgumentException("A class name is required.", nameof(className));

            foreach (TreeNode classNode in _tree.Nodes)
            {
                var cls = classNode.Tag as ThemeClassInfo;
                if (cls == null || !string.Equals(cls.Name, className, StringComparison.Ordinal))
                    continue;

                foreach (TreeNode partNode in classNode.Nodes)
                {
                    var part = partNode.Tag as ThemePartInfo;
                    if (part == null || part.Id != partId)
                        continue;

                    foreach (TreeNode stateNode in partNode.Nodes)
                    {
                        var state = stateNode.Tag as ThemeStateInfo;
                        if (state != null && state.Id == stateId)
                        {
                            _tree.SelectedNode = stateNode;
                            return;
                        }
                    }

                    _tree.SelectedNode = partNode;
                    return;
                }

                _tree.SelectedNode = classNode;
                return;
            }

            throw new ArgumentException("Theme class '" + className + "' was not found in the database.", nameof(className));
        }

        internal IList<string> PropertyRows(int maximum)
        {
            var rows = new List<string>();
            int count = Math.Min(maximum, _properties.Items.Count);
            for (int i = 0; i < count; i++)
            {
                var cells = new List<string>();
                foreach (ListViewItem.ListViewSubItem cell in _properties.Items[i].SubItems)
                    cells.Add(cell.Text);
                rows.Add(string.Join(" | ", cells.ToArray()));
            }
            return rows;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _themeChangeTimer.Stop();
                _themeChangeTimer.Dispose();
                _toolTip.Dispose();

                if (_style != null)
                {
                    _style.Dispose();
                    _style = null;
                }

                _context.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
