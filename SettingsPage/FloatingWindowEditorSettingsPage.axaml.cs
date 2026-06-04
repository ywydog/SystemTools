using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ClassIsland.Core;
using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Shared;
using SystemTools.ConfigHandlers;
using SystemTools.Services;
using SystemTools.Shared;

namespace SystemTools;

[HidePageTitle]
[SettingsPageInfo("systemtools.settings.floating", "悬浮窗编辑", "\uEA37", "\uEA37")]
public partial class FloatingWindowEditorSettingsPage : SettingsPageBase
{
    public FloatingWindowEditorSettingsPage()
    {
        if (GlobalConstants.MainConfig == null)
            GlobalConstants.MainConfig = new MainConfigHandler(GlobalConstants.PluginConfigFolder
                                                               ?? Path.Combine(
                                                                   Environment.GetFolderPath(Environment.SpecialFolder
                                                                       .LocalApplicationData), "ClassIsland", "Plugins",
                                                                   "SystemTools"));

        ViewModel = new SystemToolsSettingsViewModel(GlobalConstants.MainConfig,
            IAppHost.GetService<FloatingWindowService>());
        DataContext = this;
        InitializeComponent();

        DropHandler = new FloatingWindowDropHandler(this);

        ViewModel.RefreshFloatingWindowProfiles();
        ViewModel.RefreshFloatingTriggers();
        ViewModel.CurrentFloatingWindowProfile.PropertyChanged += OnProfilePropertyChanged;
        ViewModel.Settings.PropertyChanged += OnSettingsPropertyChanged;
        ViewModel.ProfileChanged += OnViewModelProfileChanged;

        // 注册悬浮窗规则集变更监听
        if (ViewModel.CurrentFloatingWindowProfile.FloatingWindowHidingRules is INotifyPropertyChanged hidingRules)
        {
            hidingRules.PropertyChanged += OnHidingRulesPropertyChanged;
        }
    }

    public SystemToolsSettingsViewModel ViewModel { get; }
    public FloatingWindowDropHandler DropHandler { get; }

    private bool _isDisposed;

    // ===== 拖拽状态 =====
    private FloatingTriggerItem? _dragItem;
    private ObservableCollection<FloatingTriggerItem>? _dragSourceCollection;
    private FloatingTriggerRow? _dragRow;
    private bool _isDragging;
    private Point _dragStartPoint;
    private const double DragThreshold = 5.0;

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        if (_isDisposed)
        {
            return;
        }

        // 页面卸载时确保所有配置（包括规则集）都已保存
        IAppHost.GetService<FloatingWindowService>().ProfileManager.SaveProfile();

        ViewModel.CurrentFloatingWindowProfile.PropertyChanged -= OnProfilePropertyChanged;
        ViewModel.Settings.PropertyChanged -= OnSettingsPropertyChanged;
        ViewModel.ProfileChanged -= OnViewModelProfileChanged;

        // 注销悬浮窗规则集变更监听
        if (ViewModel.CurrentFloatingWindowProfile.FloatingWindowHidingRules is INotifyPropertyChanged hidingRules)
        {
            hidingRules.PropertyChanged -= OnHidingRulesPropertyChanged;
        }

        ViewModel.Dispose();
        _isDisposed = true;
    }

    private void OnProfilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(FloatingWindowProfile.FloatingWindowScale)
            or nameof(FloatingWindowProfile.FloatingWindowIconSize)
            or nameof(FloatingWindowProfile.FloatingWindowTextSize)
            or nameof(FloatingWindowProfile.FloatingWindowOpacity)
            or nameof(FloatingWindowProfile.FloatingWindowShadowEnabled)
            or nameof(FloatingWindowProfile.FloatingWindowLayer)
            or nameof(FloatingWindowProfile.FloatingWindowLayerRecheckMode)
            or nameof(FloatingWindowProfile.FloatingWindowHideOnRule)
            or nameof(FloatingWindowProfile.FloatingWindowDragHandleAlwaysVisible)
            or nameof(FloatingWindowProfile.FloatingWindowHorizontal))
        {
            IAppHost.GetService<FloatingWindowService>().ProfileManager.SaveProfile();
            IAppHost.GetService<FloatingWindowService>().UpdateWindowState();
        }
    }

    /// <summary>
    /// 重新注册 Profile 属性变更事件监听（切换方案后需要重新注册）
    /// </summary>
    public void ReattachProfilePropertyChanged()
    {
        ViewModel.CurrentFloatingWindowProfile.PropertyChanged -= OnProfilePropertyChanged;
        ViewModel.CurrentFloatingWindowProfile.PropertyChanged += OnProfilePropertyChanged;

        // 重新注册悬浮窗规则集变更监听
        if (ViewModel.CurrentFloatingWindowProfile.FloatingWindowHidingRules is INotifyPropertyChanged hidingRules)
        {
            hidingRules.PropertyChanged -= OnHidingRulesPropertyChanged;
            hidingRules.PropertyChanged += OnHidingRulesPropertyChanged;
        }
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainConfigData.FloatingWindowTheme))
        {
            GlobalConstants.MainConfig?.Save();
            IAppHost.GetService<FloatingWindowService>().UpdateWindowState();
        }
    }

    private void OnViewModelProfileChanged(object? sender, EventArgs e)
    {
        ReattachProfilePropertyChanged();
    }

    private void OnHidingRulesPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        IAppHost.GetService<FloatingWindowService>().ProfileManager.SaveProfile();
    }

    private void OnFloatingWindowVisibleToggleChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is not ToggleSwitch toggle)
        {
            return;
        }

        var service = IAppHost.GetService<FloatingWindowService>();
        var profile = ViewModel.CurrentFloatingWindowProfile;

        // 没有可用按钮时强制隐藏
        var shouldShow = toggle.IsChecked == true && service.Entries.Count > 0;
        profile.ShowFloatingWindow = shouldShow;

        // 同步 ToggleSwitch 状态（可能被强制隐藏）
        if (toggle.IsChecked != shouldShow)
        {
            toggle.IsChecked = shouldShow;
        }

        service.ProfileManager.SaveProfile();
        service.UpdateWindowState();
    }

    private void OnFloatingWindowProfileSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox comboBox || comboBox.SelectedItem is not string profileName)
        {
            return;
        }

        ViewModel.SwitchFloatingWindowProfile(profileName);
    }

    private void OnToggleFloatingWindowProfileClick(object? sender, RoutedEventArgs e)
    {
        IAppHost.GetService<FloatingWindowService>().ToggleWindowProfile();
        ViewModel.RefreshFloatingWindowProfiles();
        ViewModel.RefreshFloatingTriggers();
    }

    private void OnAddFloatingWindowProfileClick(object? sender, RoutedEventArgs e)
    {
        ViewModel.AddFloatingWindowProfile();
    }

    private void OnRemoveCurrentProfileClick(object? sender, RoutedEventArgs e)
    {
        var currentName = ViewModel.SelectedFloatingWindowProfile;
        if (string.IsNullOrWhiteSpace(currentName))
        {
            return;
        }

        ViewModel.RemoveFloatingWindowProfile(currentName);
    }

    private void OnInsertRowBelowClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control control)
        {
            return;
        }

        var row = control.GetVisualAncestors()
            .OfType<Border>()
            .Select(b => b.DataContext)
            .OfType<FloatingTriggerRow>()
            .FirstOrDefault();

        if (row == null)
        {
            return;
        }

        var index = ViewModel.FloatingTriggerRows.IndexOf(row);
        if (index < 0)
        {
            return;
        }

        ViewModel.InsertFloatingTriggerRow(index + 1);
    }

    private void OnAddFloatingTriggerRowClick(object? sender, RoutedEventArgs e)
    {
        ViewModel.AddFloatingTriggerRow();
    }

    private void OnRemoveFloatingTriggerRowClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: FloatingTriggerRow row })
        {
            return;
        }

        if (ViewModel.FloatingTriggerRows.Count <= 1)
        {
            return;
        }

        _ = ViewModel.RemoveFloatingTriggerRow(row);
    }

    private void OnRemoveTriggerFromRowClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string buttonId)
        {
            return;
        }

        ViewModel.RemoveTriggerToPool(buttonId);
    }

    /// <summary>
    /// 按钮规则集按钮点击：打开该按钮的规则集 Drawer
    /// </summary>
    private void OnButtonRulesetClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string buttonId)
            return;

        // 在所有行中查找该按钮
        var item = ViewModel.FloatingTriggerRows
            .SelectMany(r => r.Buttons)
            .FirstOrDefault(b => b.ButtonId == buttonId);
        if (item == null) return;

        ViewModel.SelectedFloatingTriggerItem = item;

        if (this.FindResource("RulesetControl") is not ClassIsland.Core.Controls.Ruleset.RulesetControl control)
            return;
        control.Ruleset = item.Config.HidingRules;
        OpenDrawer("RulesetControl");
    }

    /// <summary>
    /// 行规则集按钮点击：打开该行的规则集 Drawer
    /// </summary>
    private void OnRowRulesetClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control control)
            return;

        var row = control.GetVisualAncestors()
            .OfType<Border>()
            .Select(b => b.DataContext)
            .OfType<FloatingTriggerRow>()
            .FirstOrDefault();
        if (row == null) return;

        ViewModel.SelectedFloatingTriggerRow = row;

        if (this.FindResource("RulesetControl") is not ClassIsland.Core.Controls.Ruleset.RulesetControl rulesetControl)
            return;
        rulesetControl.Ruleset = row.RowRuleset.HidingRules;
        OpenDrawer("RulesetControl");
    }

    // ===== 选中状态处理 =====

    private void OnRowSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox listBox && listBox.SelectedItem is FloatingTriggerRow row)
        {
            ViewModel.SelectedFloatingTriggerRow = row;
        }
    }

    private void OnButtonSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox listBox && listBox.SelectedItem is FloatingTriggerItem item)
        {
            ViewModel.SelectedFloatingTriggerItem = item;
        }
    }

    private void OnAvailableItemSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox listBox || listBox.SelectedItem is not FloatingTriggerItem item)
        {
            return;
        }

        // 先清除选中状态，避免移除项时选择模型与集合冲突（ArgumentOutOfRangeException）
        var buttonId = item.ButtonId;
        listBox.SelectedItem = null;

        // 点击组件库项：添加到第一行末尾
        if (ViewModel.FloatingTriggerRows.Count == 0)
        {
            ViewModel.AddFloatingTriggerRow();
        }
        ViewModel.AddTriggerFromPool(buttonId, 0, ViewModel.FloatingTriggerRows[0].Buttons.Count);
    }

    // ===== 拖拽处理（标准 Avalonia DragDrop） =====

    /// <summary>
    /// 行拖拽把手按下：开始行拖拽
    /// </summary>
    private void OnRowDragThumbPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control control) return;

        // 找到所属行
        var row = control.GetVisualAncestors()
            .OfType<Border>()
            .Select(b => b.DataContext)
            .OfType<FloatingTriggerRow>()
            .FirstOrDefault();
        if (row == null) return;

        _dragRow = row;
        _dragItem = null;
        _dragSourceCollection = null;
        _dragStartPoint = e.GetPosition(this);
        _isDragging = false;

        e.Handled = true;
    }

    /// <summary>
    /// 行内按钮按下：开始按钮拖拽
    /// </summary>
    private void OnButtonPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control control || control.DataContext is not FloatingTriggerItem item) return;

        // 找到所属行的 Buttons 集合
        var row = control.GetVisualAncestors()
            .OfType<Border>()
            .Select(b => b.DataContext)
            .OfType<FloatingTriggerRow>()
            .FirstOrDefault();
        if (row == null) return;

        _dragItem = item;
        _dragSourceCollection = row.Buttons;
        _dragRow = null;
        _dragStartPoint = e.GetPosition(this);
        _isDragging = false;

        e.Handled = true;
    }

    /// <summary>
    /// 组件库项按下：开始从组件库拖拽
    /// </summary>
    private void OnPoolItemPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control control || control.DataContext is not FloatingTriggerItem item) return;

        _dragItem = item;
        _dragSourceCollection = null; // null 表示来自组件库
        _dragRow = null;
        _dragStartPoint = e.GetPosition(this);
        _isDragging = false;

        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (_dragItem == null && _dragRow == null) return;
        if (_isDragging) return;

        var currentPos = e.GetPosition(this);
        var delta = currentPos - _dragStartPoint;

        if (System.Math.Abs(delta.X) < DragThreshold && System.Math.Abs(delta.Y) < DragThreshold)
            return;

        _isDragging = true;

        // 执行拖拽
        if (_dragRow != null)
        {
            // 行拖拽
            var data = new DataObject();
            data.Set("FloatingWindowRow", _dragRow);
            DragDrop.DoDragDrop(e, data, DragDropEffects.Move);
        }
        else if (_dragItem != null)
        {
            // 按钮拖拽（行内/跨行/从组件库）
            var data = new DataObject();
            data.Set("FloatingWindowButton", new FloatingWindowButtonDragData
            {
                Item = _dragItem,
                SourceCollection = _dragSourceCollection
            });
            DragDrop.DoDragDrop(e, data, DragDropEffects.Move);
        }

        _dragItem = null;
        _dragSourceCollection = null;
        _dragRow = null;
        _isDragging = false;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _dragItem = null;
        _dragSourceCollection = null;
        _dragRow = null;
        _isDragging = false;
    }

    // ===== 规则集 Drawer（参照 ClassIsland） =====

    private void ButtonOpenFloatingWindowRuleset_OnClick(object? sender, RoutedEventArgs e)
    {
        if (this.FindResource("RulesetControl") is not ClassIsland.Core.Controls.Ruleset.RulesetControl control)
            return;
        control.Ruleset = ViewModel.CurrentFloatingWindowProfile.FloatingWindowHidingRules;
        OpenDrawer("RulesetControl");
    }
}
