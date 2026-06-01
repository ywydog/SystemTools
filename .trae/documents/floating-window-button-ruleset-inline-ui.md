# 悬浮窗按钮规则集内联UI优化计划

## 需求概述

用户提出两个核心问题：
1. **按钮规则集UI应该像ClassIsland组件一样**：点击按钮后，可以在按钮旁边直接调整规则集设置，而不是在底部的折叠面板中集中管理
2. **拖拽反馈不明确**：拖动按钮时无法确定自己是否正在拖动，缺少视觉反馈

用户特别叮嘱：
- **性能优化**：避免不必要的UI渲染和重复保存
- **去掉单独的按钮显示开关**：不需要 `IsVisible` 开关，因为删除按钮即可实现隐藏
- **配置文件保存**：确保修改后及时、正确地写入JSON文件

## 现状分析

### 当前按钮规则集UI
- 位置：在"规则集设置"折叠面板中，所有按钮的规则集集中显示
- 问题：按钮和规则集配置分离，用户需要滚动到页面底部才能配置，不直观

### 当前拖拽机制
- 触发方式：`PointerPressed`/`PointerMoved`/`PointerReleased` 在拖拽把手上
- 问题：
  - 只有 `⋮` 把手区域可以触发拖拽，但把手太小（4px padding）
  - 拖拽开始时没有视觉反馈（如半透明、阴影、光标变化）
  - 拖拽过程中没有预览效果
  - 按钮池中的卡片也是整个区域触发拖拽，但没有视觉提示

## 设计方案

### 一、按钮规则集内联配置（核心改动）

参考ClassIsland组件配置方式：
- 每个按钮在行内显示时，右侧添加一个"规则集"按钮（齿轮图标）
- 点击后在该按钮下方展开**精简的规则集配置面板**
- 面板只包含：**启用规则集开关** + **规则集编辑器**（去掉 `IsVisible` 开关，因为删除按钮即可隐藏）

#### XAML结构变更

**行内按钮模板修改：**
```xml
<Border>
    <Grid RowDefinitions="Auto,Auto">
        <!-- 第一行：按钮主体 -->
        <Grid Grid.Row="0" ColumnDefinitions="Auto,*,Auto,Auto">
            <!-- 拖拽把手 -->
            <Border Grid.Column="0">...</Border>
            <!-- 图标+名称 -->
            <StackPanel Grid.Column="1">...</StackPanel>
            <!-- 规则集按钮（新增） -->
            <Button Grid.Column="2"
                    Content="{ci:FluentIcon &#xE9A8;}"
                    ToolTip.Tip="规则集"
                    Click="OnButtonRulesetClick"
                    Theme="{StaticResource TransparentButton}" />
            <!-- 删除按钮 -->
            <Button Grid.Column="3">...</Button>
        </Grid>
        
        <!-- 第二行：规则集展开面板（新增） -->
        <Border Grid.Row="1" IsVisible="{Binding IsRulesetExpanded}">
            <StackPanel>
                <Grid ColumnDefinitions="*,Auto">
                    <TextBlock Text="启用规则集控制" />
                    <ToggleSwitch IsChecked="{Binding Config.RulesetEnabled}" />
                </Grid>
                <ruleset:RulesetControl Ruleset="{Binding Config.Ruleset}" 
                                        IsEnabled="{Binding Config.RulesetEnabled}" />
            </StackPanel>
        </Border>
    </Grid>
</Border>
```

#### ViewModel变更

**FloatingTriggerItem新增属性：**
```csharp
public partial class FloatingTriggerItem : ObservableObject
{
    [ObservableProperty] private string _buttonId = string.Empty;
    [ObservableProperty] private string _icon = string.Empty;
    [ObservableProperty] private string _buttonName = string.Empty;
    [ObservableProperty] private bool _isRulesetExpanded = false;  // 新增：控制展开状态
    [ObservableProperty] private ButtonRulesetConfig _config = new(); // 新增：直接引用规则集配置
}
```

**重构RefreshFloatingTriggers：**
- 为每个按钮查找并绑定对应的 `ButtonRulesetConfig`
- 如果字典中不存在，自动创建新的配置并加入字典
- 移除底部集中的 `FloatingTriggerButtonConfigs` 集合及相关代码

#### 代码后置事件

```csharp
private void OnButtonRulesetClick(object? sender, RoutedEventArgs e)
{
    // 找到对应的 FloatingTriggerItem
    // 切换 IsRulesetExpanded 状态
    // 关闭其他按钮的规则集面板（单开模式，避免同时展开多个占用空间）
}
```

#### 配置文件保存策略

```csharp
// 规则集修改后自动保存
// 利用 ButtonRulesetConfig 的 PropertyChanged 事件
// 或者由 RulesetControl 的变更触发保存

// 在 ViewModel 中订阅 Config 属性变更
private void SubscribeButtonConfigChanges()
{
    foreach (var row in FloatingTriggerRows)
    {
        foreach (var button in row.Buttons)
        {
            button.Config.PropertyChanged += OnButtonConfigPropertyChanged;
        }
    }
}

private void OnButtonConfigPropertyChanged(object? sender, PropertyChangedEventArgs e)
{
    // 直接保存整个 profile，确保 JSON 文件更新
    _floatingWindowService.ProfileManager.SaveProfile();
    // 通知悬浮窗更新显示状态
    _floatingWindowService.UpdateWindowState();
}
```

### 二、移除底部按钮规则集面板

- 删除 XAML 中"规则集设置"折叠面板内的"按钮规则集"区域
- 删除 `FloatingTriggerButtonConfigItem` 类
- 删除 `FloatingTriggerButtonConfigs` 集合
- 删除 `RefreshFloatingTriggerButtonConfigs` 方法
- 保留"悬浮窗规则集"（整体控制悬浮窗显示/隐藏）

### 三、拖拽视觉反馈优化

#### 1. 拖拽把手扩大热区
```xml
<!-- 当前：padding="4,2" 太小 -->
<!-- 改为：padding="8,6" 增加可点击区域，并设置拖拽光标 -->
<Border Padding="8,6" Cursor="SizeAll">
```

#### 2. 拖拽开始时添加视觉反馈
```csharp
private Border? _dragVisualBorder;

private void OnFloatingTriggerItemPointerPressed(object? sender, PointerPressedEventArgs e)
{
    // ... 现有逻辑 ...
    
    // 记录原始视觉效果
    _dragVisualBorder = border;
    _dragOriginalOpacity = border.Opacity;
}

private void OnFloatingTriggerItemPointerMoved(object? sender, PointerEventArgs e)
{
    // ... 现有逻辑（距离判断）...
    
    // 一旦判定为拖拽，立即添加视觉反馈
    if (_dragVisualBorder != null && _isDragging)
    {
        _dragVisualBorder.Opacity = 0.6;
        _dragVisualBorder.Classes.Add("dragging");
    }
}

private void OnFloatingTriggerItemPointerReleased(object? sender, PointerReleasedEventArgs e)
{
    // 恢复视觉效果
    if (_dragVisualBorder != null)
    {
        _dragVisualBorder.Opacity = _dragOriginalOpacity;
        _dragVisualBorder.Classes.Remove("dragging");
        _dragVisualBorder = null;
    }
    // ... 现有逻辑 ...
}
```

#### 3. 添加拖拽样式（在XAML Resources中）
```xml
<Style Selector="Border.dragging">
    <Setter Property="Opacity" Value="0.6" />
    <Setter Property="BoxShadow" Value="0 4 12 0 #40000000" />
</Style>
```

#### 4. 按钮池卡片拖拽反馈
- 鼠标悬停在按钮池卡片上时显示 `Cursor="SizeAll"`
- 拖拽开始时卡片透明度降至 0.5
- 拖拽把手（`⋮`）使用更深的颜色，始终清晰可见

## 实施步骤

### 步骤1：修改数据模型（SystemToolsSettingsViewModel.cs）
- [ ] 给 `FloatingTriggerItem` 添加 `IsRulesetExpanded` 和 `Config` 属性
- [ ] 修改 `RefreshFloatingTriggers`：为每个按钮查找/创建 `ButtonRulesetConfig`
- [ ] 删除 `FloatingTriggerButtonConfigItem` 类
- [ ] 删除 `FloatingTriggerButtonConfigs` 属性
- [ ] 删除 `RefreshFloatingTriggerButtonConfigs` 方法

### 步骤2：修改行内按钮XAML（FloatingWindowEditorSettingsPage.axaml）
- [ ] 将按钮卡片改为 `Grid RowDefinitions="Auto,Auto"`
- [ ] 第一行添加"规则集"按钮（`&#xE9A8;`）
- [ ] 第二行添加规则集展开面板（仅包含启用开关和规则集编辑器）
- [ ] 移除 `IsVisible` ToggleSwitch（用户不需要单独的显示开关）

### 步骤3：添加事件处理（FloatingWindowEditorSettingsPage.axaml.cs）
- [ ] 添加 `OnButtonRulesetClick` 方法
- [ ] 实现单开逻辑（展开一个时关闭其他按钮的规则集面板）

### 步骤4：移除底部按钮规则集面板（FloatingWindowEditorSettingsPage.axaml）
- [ ] 删除"规则集设置"折叠面板中的"按钮规则集"区域

### 步骤5：优化拖拽视觉反馈
- [ ] 扩大拖拽把手热区（padding 4,2 → 8,6）
- [ ] 添加 `Cursor="SizeAll"` 到拖拽把手
- [ ] 在 `PointerMoved` 中判定拖拽后添加 `dragging` 样式类
- [ ] 在 `PointerReleased` 中移除 `dragging` 样式类
- [ ] 添加拖拽样式到 XAML Resources
- [ ] 为按钮池卡片添加悬停光标提示

### 步骤6：配置文件保存
- [ ] 在 `FloatingTriggerItem` 的 `Config` 属性变更时，调用 `SaveProfile()`
- [ ] 确保 `ButtonRulesetConfig` 的 `PropertyChanged` 能正确传播到 profile 保存
- [ ] 测试：修改规则集后检查 JSON 文件是否及时更新

### 步骤7：测试验证
- [ ] 测试点击规则集按钮展开/折叠
- [ ] 测试规则集修改后实时生效且 JSON 保存正确
- [ ] 测试拖拽按钮时的视觉反馈（透明度变化、阴影）
- [ ] 测试按钮池拖拽到行中
- [ ] 测试删除按钮后配置文件中对应规则集是否被清理

## 性能优化策略

1. **懒加载规则集编辑器**：`RulesetControl` 只在展开时初始化，折叠时释放资源
2. **批量保存**：如果连续快速修改多个属性，使用防抖（debounce）延迟保存，避免频繁写入磁盘
3. **避免重复刷新**：`UpdateWindowState()` 只在必要属性变更时调用，而非每次保存都调用
4. **移除冗余集合**：删除 `FloatingTriggerButtonConfigs` 减少内存占用和绑定开销

## 配置文件保存注意事项

1. **保存时机**：
   - 规则集配置修改后立即保存（通过 `PropertyChanged` 监听）
   - 按钮拖拽排序后保存（已有 `PersistFloatingTriggerRows`）
   - 按钮添加/删除后保存

2. **保存内容**：
   - `FloatingWindowButtonRulesets` 字典中只保留当前存在的按钮ID
   - 删除按钮时同步清理对应的规则集配置（在 `PruneInvalidButtonIds` 中已实现）

3. **线程安全**：
   - `SaveProfile()` 使用 `ConfigureFileHelper.SaveConfig`，确保线程安全
   - UI 线程上的修改通过 `Dispatcher.UIThread.Post` 触发保存
