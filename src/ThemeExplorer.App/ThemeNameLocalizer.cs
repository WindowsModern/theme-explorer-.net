using System;
using System.Collections.Generic;
using ThemeExplorer.Core;
using ThemeExplorer.Ui;

namespace ThemeExplorer.App
{
    /// <summary>
    /// Best-effort Chinese names for the UXTHEME class/part/state database.
    /// Unknown technical names deliberately fall back to their English display
    /// name instead of guessing a translation.
    /// </summary>
    internal static class ThemeNameLocalizer
    {
        private static readonly Dictionary<string, string> ClassNames =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "AddressBand", "地址栏" },
            { "AeroWizard", "Aero 向导" },
            { "Button", "按钮" },
            { "CheckBox", "复选框" },
            { "Clock", "时钟" },
            { "Combobox", "组合框" },
            { "CommandLink", "命令链接" },
            { "DatePicker", "日期选择器" },
            { "Edit", "编辑框" },
            { "ExplorerBar", "资源管理器栏" },
            { "Flyout", "浮出控件" },
            { "GroupBox", "分组框" },
            { "Header", "标题" },
            { "Link", "链接" },
            { "ListBox", "列表框" },
            { "ListView", "列表视图" },
            { "Menu", "菜单" },
            { "MenuBand", "菜单栏" },
            { "MonthCal", "月历" },
            { "Navigation", "导航" },
            { "Page", "页面" },
            { "Progress", "进度条" },
            { "RadioButton", "单选按钮" },
            { "Rebar", "伸缩栏" },
            { "ScrollBar", "滚动条" },
            { "Spin", "数值调节" },
            { "StartPanel", "开始面板" },
            { "Static", "静态文本" },
            { "Status", "状态栏" },
            { "Tab", "选项卡" },
            { "TaskBar", "任务栏" },
            { "TaskDialog", "任务对话框" },
            { "Toolbar", "工具栏" },
            { "ToolTip", "工具提示" },
            { "TrackBar", "滑块" },
            { "TrayNotify", "通知区域" },
            { "TreeView", "树视图" },
            { "Window", "窗口" },
        };

        private static readonly Dictionary<string, string> PartNames =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Background", "背景" },
            { "BackgroundBottom", "底边背景" },
            { "BackgroundRight", "右边背景" },
            { "BackgroundTop", "顶部背景" },
            { "BackgroundLeft", "左边背景" },
            { "Bar", "条" },
            { "Border", "边框" },
            { "Borders", "边框" },
            { "Button", "按钮" },
            { "CheckBox", "复选框" },
            { "Chunk", "块" },
            { "Close", "关闭" },
            { "Divider", "分隔线" },
            { "Fill", "填充" },
            { "FootnoteSeparator", "脚注分隔线" },
            { "Gripper", "抓手" },
            { "GripperHorz", "水平抓手" },
            { "GripperVert", "垂直抓手" },
            { "Header", "标题" },
            { "LineHorz", "水平线" },
            { "LineVert", "垂直线" },
            { "PopupSeparator", "弹出菜单分隔线" },
            { "PushButton", "普通按钮" },
            { "RadioButton", "单选按钮" },
            { "Separator", "分隔线" },
            { "SeparatorVert", "垂直分隔线" },
            { "SizingBarBottom", "底边尺寸条" },
            { "SizingBarRight", "右边尺寸条" },
            { "SizingBarTop", "顶部尺寸条" },
            { "SizingBarLeft", "左边尺寸条" },
            { "Text", "文本" },
            { "Thumb", "滑块" },
            { "Track", "轨道" },
        };

        private static readonly Dictionary<string, string> StateNames =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Normal", "正常" },
            { "Hot", "热" },
            { "Pressed", "按下" },
            { "Disabled", "禁用" },
            { "Checked", "选中" },
            { "HotChecked", "热选中" },
            { "Mixed", "混合" },
            { "MixedDisabled", "混合禁用" },
            { "Focused", "聚焦" },
            { "Selected", "选中" },
            { "ReadOnly", "只读" },
            { "Default", "默认" },
            { "NearHot", "邻近热" },
            { "OtherSideHot", "另一侧热" },
        };

        internal static string Class(ThemeClassInfo info)
        {
            if (info == null)
                return string.Empty;

            string name = Translate(ClassNames, info.Name);
            return string.IsNullOrEmpty(info.SubClass) ? name : name + " [" + info.SubClass + "]";
        }

        internal static string Part(ThemePartInfo info)
        {
            return info == null ? string.Empty : Translate(PartNames, info.DisplayName);
        }

        internal static string State(ThemeStateInfo info)
        {
            return info == null ? string.Empty : Translate(StateNames, info.DisplayName);
        }

        private static string Translate(Dictionary<string, string> table, string name)
        {
            if (string.IsNullOrEmpty(name) || !UiCulture.IsChinese)
                return name ?? string.Empty;

            string translated;
            return table.TryGetValue(name, out translated) ? translated : name;
        }
    }
}
