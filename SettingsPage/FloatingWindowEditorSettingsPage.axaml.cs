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
using ClassIsland.Core.Controls.Ruleset;
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

        ViewModel.RefreshFloatingWindowProfiles();
        ViewModel.RefreshFloatingTriggers();
        ViewModel.CurrentFloatingWindowProfile.PropertyChanged += OnProfilePropertyChanged;
        ViewModel.Settings.PropertyChanged += OnSettingsPropertyChanged;
        ViewModel.ProfileChanged += OnViewModelProfileChanged;

        // 注册悬浮窗规则集变更监听
        RegisterHidingRulesEvents();
    }

    public SystemToolsSettingsViewModel ViewModel { get; }

    private bool _isDisposed;

    // ===== 拖拽状态 =====
    private FloatingTriggerItem? _dragItem;
    private ObservableCollection<FloatingTriggerItem>? _dragSourceCollection;
    private FloatingTriggerRow? _dragRow;
    private bool _isDragging;
    private Point _dragStartPoint;
    // 触摸屏需要更大阈值，避免误触拖拽
    private const double DragThreshold = 8.0;

    // ===== 规则集 Drawer 状态 =====
    private enum RulesetTargetType { Button, Row, Window }
    private RulesetTargetType _currentRulesetTarget;
    private FloatingTriggerItem? _currentButtonTarget;
    private FloatingTriggerRow? _currentRowTarget;

    // Drawer 内的控件引用
    private ToggleSwitch? _drawerIsVisibleToggle;
    private ToggleSwitch? _drawerHideOnRuleToggle;
    private RulesetControl? _drawerRulesetControl;

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

        UnregisterHidingRulesEvents();

        ViewModel.Dispose();
        _isDisposed = true;
    }

    private void OnProfilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(FloatingWindowProfile.ShowFloatingWindow)
            or nameof(FloatingWindowProfile.FloatingWindowScale)
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
        else if (e.PropertyName == nameof(FloatingWindowProfile.FloatingWindowHidingRules))
        {
            // Ruleset 对象被替换时，重新注册事件
            UnregisterHidingRulesEvents();
            RegisterHidingRulesEvents();
            IAppHost.GetService<FloatingWindowService>().ProfileManager.SaveProfile();
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
        UnregisterHidingRulesEvents();
        RegisterHidingRulesEvents();
    }

    private void RegisterHidingRulesEvents()
    {
        if (ViewModel.CurrentFloatingWindowProfile.FloatingWindowHidingRules is INotifyPropertyChanged hidingRules)
        {
            hidingRules.PropertyChanged += OnHidingRulesPropertyChanged;
        }
    }

    private void UnregisterHidingRulesEvents()
    {
        if (ViewModel.CurrentFloatingWindowProfile.FloatingWindowHidingRules is INotifyPropertyChanged hidingRules)
        {
            hidingRules.PropertyChanged -= OnHidingRulesPropertyChanged;
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

        var row = control.DataContext as FloatingTriggerRow;
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

    // ===== 规则集 Drawer（参照 ClassIsland，含 IsVisible/HideOnRule 开关） =====

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
        _currentRulesetTarget = RulesetTargetType.Button;
        _currentButtonTarget = item;
        _currentRowTarget = null;

        OpenRulesetDrawer(item.Config.HidingRules, item.Config.IsVisible, item.Config.HideOnRule);
    }

    /// <summary>
    /// 行规则集按钮点击：打开该行的规则集 Drawer
    /// </summary>
    private void OnRowRulesetClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control control)
            return;

        // 通过 DataContext 获取所属行
        var row = control.DataContext as FloatingTriggerRow;
        if (row == null) return;

        ViewModel.SelectedFloatingTriggerRow = row;
        _currentRulesetTarget = RulesetTargetType.Row;
        _currentRowTarget = row;
        _currentButtonTarget = null;

        OpenRulesetDrawer(row.RowRuleset.HidingRules, row.RowRuleset.IsVisible, row.RowRuleset.HideOnRule);
    }

    private void ButtonOpenFloatingWindowRuleset_OnClick(object? sender, RoutedEventArgs e)
    {
        _currentRulesetTarget = RulesetTargetType.Window;
        _currentButtonTarget = null;
        _currentRowTarget = null;

        var profile = ViewModel.CurrentFloatingWindowProfile;
        OpenRulesetDrawer(profile.FloatingWindowHidingRules, true, profile.FloatingWindowHideOnRule);
    }

    /// <summary>
    /// 打开规则集 Drawer，包含 IsVisible/HideOnRule 开关和规则集编辑器（参照 ClassIsland）
    /// </summary>
    private void OpenRulesetDrawer(ClassIsland.Core.Models.Ruleset.Ruleset ruleset, bool isVisible, bool hideOnRule)
    {
        // 每次打开时动态构建 Drawer 内容，避免资源单例问题
        var panel = new StackPanel { Spacing = 8, Margin = new Thickness(0, 8, 0, 0) };

        // 开关面板
        var togglesPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16, Margin = new Thickness(0, 0, 0, 8) };

        _drawerIsVisibleToggle = new ToggleSwitch
        {
            OnContent = "显示",
            OffContent = "隐藏",
            ToolTip.Tip = "控制此项目是否显示",
            IsChecked = isVisible,
            IsVisible = _currentRulesetTarget != RulesetTargetType.Window
        };
        _drawerIsVisibleToggle.IsCheckedChanged += OnDrawerIsVisibleChanged;

        _drawerHideOnRuleToggle = new ToggleSwitch
        {
            OnContent = "按规则隐藏",
            OffContent = "禁用规则",
            ToolTip.Tip = "启用后，满足规则集条件时自动隐藏",
            IsChecked = hideOnRule
        };
        _drawerHideOnRuleToggle.IsCheckedChanged += OnDrawerHideOnRuleChanged;

        togglesPanel.Children.Add(_drawerIsVisibleToggle);
        togglesPanel.Children.Add(_drawerHideOnRuleToggle);
        panel.Children.Add(togglesPanel);

        // 规则集编辑器
        _drawerRulesetControl = new RulesetControl { Classes = { "in-drawer" }, Ruleset = ruleset };
        panel.Children.Add(_drawerRulesetControl);

        // 将内容放入 Resources 并打开 Drawer
        this.Resources["RulesetDrawerContent"] = panel;
        OpenDrawer("RulesetDrawerContent");
    }

    private void OnDrawerIsVisibleChanged(object? sender, RoutedEventArgs e)
    {
        var value = _drawerIsVisibleToggle?.IsChecked == true;

        switch (_currentRulesetTarget)
        {
            case RulesetTargetType.Button when _currentButtonTarget != null:
                _currentButtonTarget.Config.IsVisible = value;
                break;
            case RulesetTargetType.Row when _currentRowTarget != null:
                _currentRowTarget.RowRuleset.IsVisible = value;
                break;
        }

        IAppHost.GetService<FloatingWindowService>().ProfileManager.SaveProfile();
        IAppHost.GetService<FloatingWindowService>().UpdateWindowState();
    }

    private void OnDrawerHideOnRuleChanged(object? sender, RoutedEventArgs e)
    {
        var value = _drawerHideOnRuleToggle?.IsChecked == true;

        switch (_currentRulesetTarget)
        {
            case RulesetTargetType.Button when _currentButtonTarget != null:
                _currentButtonTarget.Config.HideOnRule = value;
                break;
            case RulesetTargetType.Row when _currentRowTarget != null:
                _currentRowTarget.RowRuleset.HideOnRule = value;
                break;
            case RulesetTargetType.Window:
                ViewModel.CurrentFloatingWindowProfile.FloatingWindowHideOnRule = value;
                break;
        }

        IAppHost.GetService<FloatingWindowService>().ProfileManager.SaveProfile();
        IAppHost.GetService<FloatingWindowService>().UpdateWindowState();
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

        // 延迟执行添加操作，确保 SelectionChanged 事件处理完成后再修改集合
        // 否则 AvailableFloatingTriggerItems.Remove 会在选择模型迭代期间触发集合变更，导致 ArgumentOutOfRangeException
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            // 点击组件库项：添加到第一行末尾
            if (ViewModel.FloatingTriggerRows.Count == 0)
            {
                ViewModel.AddFloatingTriggerRow();
            }
            ViewModel.AddTriggerFromPool(buttonId, 0, ViewModel.FloatingTriggerRows[0].Buttons.Count);
        });
    }

    // ===== 拖拽处理（标准 Avalonia DragDrop） =====

    /// <summary>
    /// 行拖拽把手按下：开始行拖拽
    /// </summary>
    private void OnRowDragThumbPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control control) return;

        // 通过 DataContext 获取所属行
        var row = control.DataContext as FloatingTriggerRow;
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

        // 通过 ViewModel 查找所属行的 Buttons 集合
        var row = ViewModel.FloatingTriggerRows.FirstOrDefault(r => r.Buttons.Contains(item));
        if (row == null) return;

        _dragItem = item;
        _dragSourceCollection = row.Buttons;
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
            data.Set("FloatingWindowButtonId", _dragItem.ButtonId);
            data.Set("FloatingWindowButtonSource", _dragSourceCollection);
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

    // ===== 行区域拖放处理 =====

    private void OnRowDropBorderDragOver(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains("FloatingWindowButtonId") || e.Data.Contains("FloatingWindowRow"))
        {
            e.DragEffects = DragDropEffects.Move;
            e.Handled = true;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private void OnRowDropBorderDrop(object? sender, DragEventArgs e)
    {
        e.Handled = true;

        // 处理行拖拽排序
        if (e.Data.Contains("FloatingWindowRow"))
        {
            var sourceRow = e.Data.Get("FloatingWindowRow") as FloatingTriggerRow;
            if (sourceRow == null) return;

            var targetIndex = FindTargetRowIndex(e, sender as Control);
            if (targetIndex < 0) return;

            var sourceIndex = ViewModel.FloatingTriggerRows.IndexOf(sourceRow);
            if (sourceIndex < 0 || sourceIndex == targetIndex) return;

            // 移动行
            ViewModel.FloatingTriggerRows.RemoveAt(sourceIndex);
            if (targetIndex > sourceIndex) targetIndex--;
            ViewModel.FloatingTriggerRows.Insert(targetIndex, sourceRow);

            // 重新计算行索引
            for (int i = 0; i < ViewModel.FloatingTriggerRows.Count; i++)
            {
                ViewModel.FloatingTriggerRows[i].RowIndex = i + 1;
            }

            ViewModel.PersistFloatingTriggerRows();
            return;
        }

        // 处理按钮拖拽
        if (!e.Data.Contains("FloatingWindowButtonId")) return;

        var buttonId = e.Data.Get("FloatingWindowButtonId") as string;
        if (string.IsNullOrEmpty(buttonId)) return;

        var sourceCollection = e.Data.Get("FloatingWindowButtonSource") as ObservableCollection<FloatingTriggerItem>;

        // 确定目标行和位置
        if (ViewModel.FloatingTriggerRows.Count == 0)
        {
            ViewModel.AddFloatingTriggerRow();
        }

        var targetRowIndex = FindTargetRowIndex(e, sender as Control);
        if (targetRowIndex < 0) targetRowIndex = 0;
        targetRowIndex = System.Math.Clamp(targetRowIndex, 0, ViewModel.FloatingTriggerRows.Count - 1);
        var targetIndex = ViewModel.FloatingTriggerRows[targetRowIndex].Buttons.Count;

        if (sourceCollection == null)
        {
            // 从组件库拖入
            ViewModel.AddTriggerFromPool(buttonId, targetRowIndex, targetIndex);
        }
        else
        {
            // 从其他行拖入
            ViewModel.MoveFloatingTrigger(buttonId, targetRowIndex, targetIndex);
        }
    }

    /// <summary>
    /// 根据拖放位置确定目标行索引
    /// </summary>
    private int FindTargetRowIndex(DragEventArgs e, Control? targetControl)
    {
        if (targetControl == null) return -1;

        var pos = e.GetPosition(targetControl);
        if (this.FindControl<ListBox>("ListBoxRows") is not ListBox rowsList)
            return -1;

        for (int i = 0; i < ViewModel.FloatingTriggerRows.Count; i++)
        {
            if (rowsList.ContainerFromIndex(i) is ListBoxItem lbi)
            {
                var transform = lbi.TransformToVisual(rowsList);
                if (transform == null) continue;
                var itemPos = transform.Value.Transform(new Point(0, 0));
                var itemBounds = lbi.Bounds;
                if (pos.Y >= itemPos.Y && pos.Y <= itemPos.Y + itemBounds.Height)
                {
                    return i;
                }
            }
        }

        return -1;
    }

    // ===== 行内按钮拖放处理 =====

    private void OnInnerButtonDragOver(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains("FloatingWindowButtonId"))
        {
            e.DragEffects = DragDropEffects.Move;
            e.Handled = true;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private void OnInnerButtonDrop(object? sender, DragEventArgs e)
    {
        if (!e.Data.Contains("FloatingWindowButtonId")) return;

        var buttonId = e.Data.Get("FloatingWindowButtonId") as string;
        if (string.IsNullOrEmpty(buttonId)) return;

        var sourceCollection = e.Data.Get("FloatingWindowButtonSource") as ObservableCollection<FloatingTriggerItem>;

        // 通过 DataContext 获取目标行
        if (sender is not Control targetControl) return;
        var targetRow = targetControl.DataContext as FloatingTriggerRow;
        if (targetRow == null) return;

        var targetRowIndex = ViewModel.FloatingTriggerRows.IndexOf(targetRow);
        if (targetRowIndex < 0) return;

        // 尝试确定精确的插入位置
        var targetIndex = targetRow.Buttons.Count;
        if (sender is ListBox listBox)
        {
            var pos = e.GetPosition(listBox);
            for (int i = 0; i < targetRow.Buttons.Count; i++)
            {
                if (listBox.ContainerFromIndex(i) is ListBoxItem lbi)
                {
                    var itemPos = lbi.TransformToVisual(listBox).Value.Transform(new Point(0, 0));
                    var itemBounds = lbi.Bounds;
                    if (pos.X >= itemPos.X && pos.X <= itemPos.X + itemBounds.Width / 2)
                    {
                        targetIndex = i;
                        break;
                    }
                    if (pos.X <= itemPos.X + itemBounds.Width && i == targetRow.Buttons.Count - 1)
                    {
                        targetIndex = i + 1;
                    }
                }
            }
        }

        e.Handled = true;

        if (sourceCollection == null)
        {
            // 从组件库拖入
            ViewModel.AddTriggerFromPool(buttonId, targetRowIndex, targetIndex);
        }
        else if (!ReferenceEquals(sourceCollection, targetRow.Buttons))
        {
            // 跨行移动
            ViewModel.MoveFloatingTrigger(buttonId, targetRowIndex, targetIndex);
        }
        else
        {
            // 行内排序
            var item = targetRow.Buttons.FirstOrDefault(b => b.ButtonId == buttonId);
            if (item == null) return;
            var sourceIndex = targetRow.Buttons.IndexOf(item);
            if (sourceIndex < 0 || sourceIndex == targetIndex) return;

            targetRow.Buttons.Move(sourceIndex, System.Math.Clamp(targetIndex, 0, targetRow.Buttons.Count - 1));
            ViewModel.PersistFloatingTriggerRows();
        }
    }
}
