# 悬浮窗编辑页面 ClassIsland 组件库风格重构设计

## 概述

将悬浮窗编辑页面（FloatingWindowEditorSettingsPage）的 UI 和拖拽交互完全重构为 ClassIsland ComponentsSettingsPage 风格，复用 ClassIsland.Core 的拖拽框架（AdvancedManagedContextDragBehavior、TouchDragThumb、ManagedDragDropService、DragPreviewService），并针对低性能设备（学校白板）做性能平衡优化。

## 当前状态分析

### 当前布局
- 使用 `SettingsExpander` 包裹按钮布局区域
- 行列表使用 `ItemsControl` + `DataTemplate`
- 按钮池使用 `ItemsControl` + `WrapPanel`
- 拖拽使用 Avalonia 原生 `DragDrop.DoDragDrop`（OS 级拖拽）
- 手动实现 `PointerPressed/Moved/Released` 事件
- 规则集面板内联在按钮下方

### 当前问题
1. **OS DragDrop 触摸体验差**：触摸屏上拖拽不流畅，需要长按才能触发
2. **无拖拽预览**：拖拽时看不到被拖动的元素
3. **布局不够直观**：SettingsExpander 嵌套过深，按钮池和行列表混在一起
4. **行操作按钮拥挤**：规则集、插入行、删除行按钮挤在一行
5. **规则集面板占用空间大**：展开后挤压其他按钮

### ClassIsland ComponentsSettingsPage 参考架构
- **行列表**：`ListBox` + 垂直排列，每行内嵌水平 `ListBox`（`VirtualizingStackPanel`）
- **组件库**：`ListBox` + `WrapPanel`（`WrapPanelAutoResizeBehavior`），标记为 `drag-source`
- **拖拽**：`AdvancedManagedContextDragBehavior`（进程内拖拽）+ `ManagedContextDropBehavior`
- **拖拽把手**：`TouchDragThumb`（触摸模式自动显示）
- **拖拽预览**：`DragPreviewService`（半透明跟随窗口）
- **设置面板**：`TabControl`（组件库 Tab + 组件设置 Tab + 高级设置 Tab + 行设置 Tab）
- **行操作**：选中行时在右侧浮出操作按钮（主行标记、通知标记、插入行、删除行）

## 设计方案

### 1. 整体布局结构

```
┌─────────────────────────────────────────────────┐
│ 方案管理 + 显示开关（保持现有设计）                │
├─────────────────────────────────────────────────┤
│ 提示文字："以下按钮将显示在悬浮窗上..."            │
├─────────────────────────────────────────────────┤
│ ┌─────────────────────────────────────────────┐ │
│ │ 行 1: [拖拽] [按钮A] [按钮B] [按钮C]  [操作] │ │  ← ListBox（垂直）
│ │ 行 2: [拖拽] [按钮D] [按钮E]        [操作] │ │
│ └─────────────────────────────────────────────┘ │
├─────────────── GridSplitter ────────────────────┤
│ ┌──────────┬──────────┬──────────┐              │
│ │ 组件库    │ 按钮设置  │ 行设置   │              │  ← TabControl
│ ├──────────┼──────────┼──────────┤              │
│ │ [拖拽] 组件1 │ 缩放: ──●──  │ 规则集: │              │
│ │ [拖拽] 组件2 │ 图标: ──●──  │ 透明度: │              │
│ │ [拖拽] 组件3 │ 透明: ──●──  │ ...     │              │
│ └──────────┴──────────┴──────────┘              │
├─────────────────────────────────────────────────┤
│ 外观设置（SettingsExpander，保持现有）            │
│ 层级设置（SettingsExpander，保持现有）            │
│ 规则集设置（SettingsExpander，保持现有）          │
└─────────────────────────────────────────────────┘
```

### 2. 行列表区域

**实现方式**：`ListBox` + `DataTemplate`

参照 ClassIsland ComponentsSettingsPage 的行列表设计：

```xml
<ListBox ItemsSource="{Binding ViewModel.FloatingTriggerRows}"
         SelectedItem="{Binding ViewModel.SelectedFloatingTriggerRow}"
         x:Name="ListBoxRows"
         Margin="-12 0">
    <!-- 行拖拽排序：AdvancedItemDragBehavior + TouchDragThumb -->
    <ListBox.ItemTemplate>
        <DataTemplate>
            <Grid ColumnDefinitions="Auto, *, Auto" Height="40">
                <ci:TouchDragThumb Grid.Column="0" Width="20" IsExplicitVisible="True" IsCompact="True"/>
                <!-- 行内按钮列表：水平 ListBox -->
                <ListBox Grid.Column="1" ItemsSource="{Binding Buttons}"
                         Classes="button-listBox">
                    <!-- 按钮拖拽：AdvancedManagedContextDragBehavior -->
                    <!-- 放置：ManagedContextDropBehavior -->
                </ListBox>
                <!-- 行操作按钮（选中时显示） -->
                <Border Grid.Column="1" VerticalAlignment="Center" HorizontalAlignment="Right"
                        IsVisible="{Binding IsSelected, RelativeSource={...}}">
                    <StackPanel Orientation="Horizontal" Spacing="2">
                        <Button Content="{ci:FluentIcon &#xE00D;}" ToolTip.Tip="在下方插入一行" />
                        <Button Content="{ci:FluentIcon &#xE61D;}" ToolTip.Tip="删除行" />
                    </StackPanel>
                </Border>
            </Grid>
        </DataTemplate>
    </ListBox.ItemTemplate>
</ListBox>
```

**行内按钮模板**：
```xml
<StackPanel Orientation="Horizontal">
    <ci:TouchDragThumb Margin="-4 0 -2 0" IsCompact="True"/>
    <controls:IconSourceElement IconSource="{Binding IconSource}" />
    <TextBlock Text="{Binding ButtonName}" />
    <Button Content="{ci:FluentIcon &#xEBAC;}" ToolTip.Tip="更多选项…" />
    <Button Content="{ci:FluentIcon &#xE61D;}" ToolTip.Tip="删除" />
</StackPanel>
```

### 3. 组件库区域

**实现方式**：`ListBox` + `WrapPanel`（标记为 `drag-source`）

参照 ClassIsland 的组件库设计：

```xml
<TabItem Header="组件库">
    <ListBox ItemsSource="{Binding ViewModel.AvailableFloatingTriggerItems}"
             Classes="drag-source">
        <ListBox.ItemTemplate>
            <DataTemplate>
                <Grid ColumnDefinitions="Auto,Auto,*" Margin="0 8" Height="50">
                    <ci:TouchDragThumb Grid.Column="0" Width="24"/>
                    <controls:IconSourceElement Grid.Column="1" Width="32" Height="32"
                                                IconSource="{Binding IconSource}"/>
                    <StackPanel Grid.Column="2">
                        <TextBlock Text="{Binding ButtonName}" />
                        <TextBlock Text="拖拽或点击添加" Opacity="0.75" FontSize="12"/>
                    </StackPanel>
                </Grid>
            </DataTemplate>
        </ListBox.ItemTemplate>
        <ListBox.ItemsPanel>
            <ItemsPanelTemplate>
                <WrapPanel>
                    <Interaction.Behaviors>
                        <ci:WrapPanelAutoResizeBehavior TargetWidth="225"/>
                    </Interaction.Behaviors>
                </WrapPanel>
            </ItemsPanelTemplate>
        </ListBox.ItemsPanel>
    </ListBox>
</TabItem>
```

### 4. 拖拽系统

**核心组件**（均来自 ClassIsland.Core，无需自研）：

| 组件 | 用途 |
|------|------|
| `AdvancedManagedContextDragBehavior` | 进程内拖拽发起，支持触摸和鼠标 |
| `ManagedContextDropBehavior` | 进程内拖拽接收 |
| `ManagedDragDropService` | 拖拽上下文管理（单例） |
| `DragPreviewService` | 拖拽预览窗口管理 |
| `TouchDragThumb` | 触摸模式自动显示的拖拽把手 |
| `AdvancedItemDragBehavior` | 行级拖拽排序 |

**拖拽数据格式**：

创建 `FloatingWindowButtonDragData` 类，参照 ClassIsland 的 `EditableComponentsListBoxDragData`：

```csharp
public class FloatingWindowButtonDragData
{
    public FloatingTriggerItem Item { get; set; }
    public ObservableCollection<FloatingTriggerItem> SourceCollection { get; set; }

    public static FloatingWindowButtonDragData Create(FloatingTriggerItem item, ObservableCollection<FloatingTriggerItem> source)
        => new() { Item = item, SourceCollection = source };
}
```

**DropHandler**：

创建 `FloatingWindowDropHandler` 实现 `IManagedDropHandler`：

```csharp
public class FloatingWindowDropHandler : IManagedDropHandler
{
    public bool ValidateDrop(object? context, object? data, DragDropEffects effects) { ... }
    public void Drop(object? context, object? data, DragDropEffects effects) { ... }
}
```

### 5. 性能优化

| 优化项 | 措施 | 效果 |
|--------|------|------|
| 拖拽预览尺寸 | 预览窗口缩放为原尺寸 70% | 减少渲染面积 |
| 预览透明度 | `PreviewOpacity = 0.5`（默认 0.65） | 降低混合计算 |
| 行内按钮面板 | 使用 `VirtualizingStackPanel` | 虚拟化减少渲染 |
| 禁用过渡动画 | 拖拽把手 `Transitions` 设为空 | 减少动画计算 |
| 预览渲染方式 | 使用 `VisualBrush` 截图而非克隆控件树 | 减少控件实例化 |

### 6. TabControl 设置面板

**Tab 1 - 组件库**：可用按钮列表（WrapPanel + drag-source）

**Tab 2 - 按钮设置**：选中按钮的规则集和配置
- 规则集开关 + RulesetControl
- 满足规则时隐藏 ToggleSwitch

**Tab 3 - 行设置**：选中行的规则集和配置
- 行可见性 ToggleSwitch
- 行规则集开关 + RulesetControl
- 满足规则时隐藏 ToggleSwitch

### 7. 需要修改的文件

| 文件 | 变更内容 |
|------|---------|
| `FloatingWindowEditorSettingsPage.axaml` | 完全重写布局：ListBox 行列表 + TabControl 设置面板 |
| `FloatingWindowEditorSettingsPage.axaml.cs` | 重写拖拽逻辑，移除 OS DragDrop，改用 ManagedDragDrop；添加 DropHandler |
| `SystemToolsSettingsViewModel.cs` | 添加 SelectedFloatingTriggerRow、SelectedFloatingTriggerItem 属性；添加 FloatingWindowDropHandler；适配新拖拽接口 |
| `FloatingTriggerItem.cs` | 添加 IconSource 属性（FluentIconSource 类型，供 IconSourceElement 使用） |
| `FloatingTriggerRow.cs` | 添加 IsSelected 相关属性 |

### 8. 不变的部分

以下部分保持现有设计不变：
- 方案管理 + 显示开关（顶部栏）
- 外观设置（SettingsExpander）
- 层级设置（SettingsExpander）
- 规则集设置（SettingsExpander）
- FloatingWindowService.cs（悬浮窗服务逻辑不变）
- FloatingWindowProfile.cs（配置数据模型不变）
- 所有配置保存逻辑不变

### 9. 实施步骤

1. **添加拖拽数据类和 DropHandler**
   - 创建 `FloatingWindowButtonDragData`
   - 创建 `FloatingWindowDropHandler`

2. **修改 ViewModel**
   - 添加选中行/选中按钮属性
   - 添加 DropHandler 实例
   - 修改 `MoveFloatingTrigger`/`AddTriggerFromPool` 适配新拖拽接口

3. **重写 XAML 布局**
   - 行列表改为 ListBox + TouchDragThumb
   - 按钮池改为 TabControl 内的 ListBox + WrapPanel
   - 按钮设置/行设置改为 TabControl 内的面板

4. **重写 code-behind 拖拽逻辑**
   - 移除所有 `DragDrop.DoDragDrop` 相关代码
   - 移除手动 `PointerPressed/Moved/Released` 事件处理
   - 改用 `AdvancedManagedContextDragBehavior` + `ManagedContextDropBehavior`

5. **性能调优**
   - 设置拖拽预览参数
   - 验证虚拟化面板工作正常

### 10. 验证标准

- [ ] 触摸屏上拖拽按钮流畅（无卡顿）
- [ ] 拖拽预览正常显示（半透明跟随）
- [ ] 从组件库拖拽按钮到行内正常工作
- [ ] 行内按钮拖拽排序正常
- [ ] 行排序（上下移动行）正常
- [ ] 点击组件库按钮可添加到行
- [ ] 删除按钮后正确回到组件库
- [ ] 规则集设置面板正常工作
- [ ] 配置保存及时性不变
- [ ] 在低性能设备上拖拽预览不卡顿
