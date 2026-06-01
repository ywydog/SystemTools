# 悬浮窗按钮规则集内联UI优化计划

## 需求概述

用户提出两个核心问题：
1. **按钮规则集UI应该像ClassIsland组件一样**：点击按钮后，可以在按钮旁边直接调整规则集设置，而不是在底部的折叠面板中集中管理
2. **拖拽反馈不明确**：拖动按钮时无法确定自己是否正在拖动，缺少视觉反馈

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
- 每个按钮在行内显示时，右侧添加一个"设置"按钮
- 点击设置按钮后，在该按钮下方展开规则集配置面板
- 面板包含：显示/隐藏开关、启用规则集开关、规则集编辑器

#### XAML结构变更

**行内按钮模板修改：**
```xml
<!-- 当前按钮卡片 -->
<Border>
    <Grid ColumnDefinitions="Auto,*,Auto,Auto">
        <!-- 拖拽把手 -->
        <Border Grid.Column="0">...</Border>
        <!-- 图标+名称 -->
        <StackPanel Grid.Column="1">...</StackPanel>
        <!-- 设置按钮（新增） -->
        <Button Grid.Column="2"
                Content="{ci:FluentIcon &#xE713;}"
                ToolTip.Tip="按钮设置"
                Click="OnButtonSettingsClick"
                Theme="{StaticResource TransparentButton}" />
        <!-- 删除按钮 -->
        <Button Grid.Column="3">...</Button>
    </Grid>
</Border>

<!-- 按钮设置展开面板（新增，放在按钮卡片下方） -->
<Border IsVisible="{Binding IsSettingsExpanded}">
    <StackPanel>
        <Grid ColumnDefinitions="*,Auto">
            <TextBlock Text="显示/隐藏" />
            <ToggleSwitch IsChecked="{Binding Config.IsVisible}" />
        </Grid>
        <Grid ColumnDefinitions="*,Auto">
            <TextBlock Text="启用规则集控制" />
            <ToggleSwitch IsChecked="{Binding Config.RulesetEnabled}" />
        </Grid>
        <ruleset:RulesetControl Ruleset="{Binding Config.Ruleset}" 
                                IsEnabled="{Binding Config.RulesetEnabled}" />
    </StackPanel>
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
    [ObservableProperty] private bool _isSettingsExpanded = false;  // 新增
    [ObservableProperty] private ButtonRulesetConfig _config = new(); // 新增，直接绑定规则集配置
}
```

**重构RefreshFloatingTriggers：**
- 行内按钮直接绑定对应的 `ButtonRulesetConfig`
- 移除底部集中的 `FloatingTriggerButtonConfigs` 集合
- 或者保留底部面板作为备用，但默认折叠

#### 代码后置事件

```csharp
private void OnButtonSettingsClick(object? sender, RoutedEventArgs e)
{
    // 找到对应的 FloatingTriggerItem
    // 切换 IsSettingsExpanded 状态
    // 关闭其他按钮的设置面板（单开模式）
}
```

### 二、拖拽视觉反馈优化

#### 1. 拖拽把手扩大热区
```xml
<!-- 当前：padding="4,2" 太小 -->
<!-- 改为：padding="8,6" 增加可点击区域 -->
<Border Padding="8,6" Cursor="SizeAll">
```

#### 2. 拖拽开始时添加视觉反馈
```csharp
private void OnFloatingTriggerItemPointerPressed(object? sender, PointerPressedEventArgs e)
{
    // ... 现有逻辑 ...
    
    // 添加按压效果
    border.Opacity = 0.7;
    border.BoxShadow = new BoxShadows(new BoxShadow { ... }); // 添加阴影
}

private void OnFloatingTriggerItemPointerMoved(object? sender, PointerEventArgs e)
{
    // ... 现有逻辑 ...
    
    // 拖拽开始时添加拖动效果
    if (border != null)
    {
        border.Opacity = 0.5;
        // 或者添加 IsDragging 样式类
    }
}

private void OnFloatingTriggerItemPointerReleased(object? sender, PointerReleasedEventArgs e)
{
    // 恢复视觉效果
    if (_floatingDragSourceBorder != null)
    {
        _floatingDragSourceBorder.Opacity = 1.0;
        _floatingDragSourceBorder.Classes.Remove("dragging");
    }
    // ... 现有逻辑 ...
}
```

#### 3. 添加拖拽样式
```xml
<Style Selector="Border.dragging">
    <Setter Property="Opacity" Value="0.6" />
    <Setter Property="RenderTransform" Value="scale(1.05)" />
    <Setter Property="BoxShadow" Value="0 4 12 0 #40000000" />
</Style>
```

#### 4. 按钮池卡片拖拽反馈
- 整个卡片作为拖拽区域时，鼠标悬停显示拖拽光标
- 拖拽开始时卡片半透明并放大
- 拖拽把手始终可见，提示用户可以拖拽

### 三、数据流调整

#### 当前数据流
```
FloatingWindowProfile.FloatingWindowButtonRulesets[buttonId] -> 
    ViewModel.FloatingTriggerButtonConfigs -> 
        XAML ItemsControl (底部面板)
```

#### 新数据流
```
FloatingWindowProfile.FloatingWindowButtonRulesets[buttonId] -> 
    ViewModel.FloatingTriggerRows[].Buttons[].Config -> 
        XAML 行内展开面板
```

#### 实现步骤
1. 给 `FloatingTriggerItem` 添加 `Config` 属性
2. 在 `RefreshFloatingTriggers` 中，为每个按钮查找对应的 `ButtonRulesetConfig`
3. 修改行内按钮XAML，添加设置按钮和展开面板
4. 移除或折叠底部的按钮规则集面板

## 实施步骤

### 步骤1：修改数据模型
- [ ] 给 `FloatingTriggerItem` 添加 `IsSettingsExpanded` 和 `Config` 属性
- [ ] 修改 `RefreshFloatingTriggers` 为每个按钮绑定 `ButtonRulesetConfig`

### 步骤2：修改行内按钮XAML
- [ ] 在按钮卡片Grid中添加"设置"按钮列
- [ ] 在按钮卡片下方添加规则集配置展开面板
- [ ] 绑定 `IsSettingsExpanded`、`Config.IsVisible`、`Config.RulesetEnabled`、`Config.Ruleset`

### 步骤3：添加事件处理
- [ ] 添加 `OnButtonSettingsClick` 方法
- [ ] 实现单开逻辑（展开一个时关闭其他）

### 步骤4：移除/折叠底部按钮规则集面板
- [ ] 将底部"按钮规则集"区域移除或改为可选查看

### 步骤5：优化拖拽视觉反馈
- [ ] 扩大拖拽把手热区
- [ ] 添加拖拽开始/结束时的透明度变化
- [ ] 添加拖拽样式类
- [ ] 为按钮池卡片添加悬停和拖拽光标提示

### 步骤6：测试验证
- [ ] 测试点击设置按钮展开/折叠规则集
- [ ] 测试规则集修改后实时生效
- [ ] 测试拖拽按钮时的视觉反馈
- [ ] 测试按钮池拖拽到行中

## 风险与注意事项

1. **性能**：每个按钮都绑定一个 `RulesetControl`，如果按钮很多可能影响性能。建议采用懒加载或虚拟化。
2. **空间**：行内展开面板会占用较多垂直空间，需要确保展开/折叠体验流畅。
3. **数据同步**：行内修改的 `Config` 需要同步到底层的 `FloatingWindowProfile.FloatingWindowButtonRulesets` 字典中。
4. **拖拽冲突**：设置按钮的点击事件和拖拽事件需要正确区分，避免点击设置按钮时触发拖拽。
