using System.Collections.ObjectModel;
using Avalonia;

namespace SystemTools;

/// <summary>
/// 悬浮窗按钮拖拽数据，参照 ClassIsland 的 EditableComponentsListBoxDragData 设计
/// </summary>
public class FloatingWindowButtonDragData : AvaloniaObject
{
    public static readonly StyledProperty<FloatingTriggerItem?> ItemProperty =
        AvaloniaProperty.Register<FloatingWindowButtonDragData, FloatingTriggerItem?>(nameof(Item));

    public FloatingTriggerItem? Item
    {
        get => GetValue(ItemProperty);
        set => SetValue(ItemProperty, value);
    }

    public static readonly StyledProperty<ObservableCollection<FloatingTriggerItem>?> SourceCollectionProperty =
        AvaloniaProperty.Register<FloatingWindowButtonDragData, ObservableCollection<FloatingTriggerItem>?>(
            nameof(SourceCollection));

    public ObservableCollection<FloatingTriggerItem>? SourceCollection
    {
        get => GetValue(SourceCollectionProperty);
        set => SetValue(SourceCollectionProperty, value);
    }

    /// <summary>
    /// 是否来自组件库（按钮池），而非行内
    /// </summary>
    public static readonly StyledProperty<bool> IsFromPoolProperty =
        AvaloniaProperty.Register<FloatingWindowButtonDragData, bool>(nameof(IsFromPool));

    public bool IsFromPool
    {
        get => GetValue(IsFromPoolProperty);
        set => SetValue(IsFromPoolProperty, value);
    }
}
