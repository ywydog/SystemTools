using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Data.Converters;

namespace SystemTools;

/// <summary>
/// 悬浮窗按钮拖拽数据，参照 ClassIsland 的 EditableComponentsListBoxDragData 设计
/// 使用 MultiBinding + Create 转换器模式
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
    /// MultiBinding 转换器，与 ClassIsland 的 EditableComponentsListBoxDragData.Create 模式一致
    /// 绑定顺序：{Binding}（当前项）, {Binding $parent[ListBox].ItemsSource}（源集合）
    /// </summary>
    public static FuncMultiValueConverter<object?, FloatingWindowButtonDragData?> Create { get; } = new(o =>
    {
        var l = o.ToList();
        if (l.Count < 2 || l[0] is not FloatingTriggerItem item
            || l[1] is not ObservableCollection<FloatingTriggerItem> source)
            return null;
        return new FloatingWindowButtonDragData()
        {
            Item = item,
            SourceCollection = source
        };
    });
}
