using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactions.DragAndDrop;

namespace SystemTools;

/// <summary>
/// 悬浮窗按钮拖放处理器，参照 ClassIsland 的 EditableComponentsListBoxDropHandler 设计
/// 处理按钮在行内排序、跨行移动、从组件库添加
/// </summary>
public class FloatingWindowDropHandler : DropHandlerBase
{
    private FloatingWindowEditorSettingsPage? _page;

    public FloatingWindowDropHandler(FloatingWindowEditorSettingsPage page)
    {
        _page = page;
    }

    private static (int index, bool found) GetTargetIndex(ListBox listBox, DragEventArgs e,
        ObservableCollection<FloatingTriggerItem> items)
    {
        var pos = e.GetPosition(listBox);
        if (listBox.GetVisualAt(pos) is Control targetControl
            && targetControl.FindAncestorOfType<ListBoxItem>() is { } listBoxItem
            && listBoxItem.DataContext is FloatingTriggerItem targetItem)
        {
            var rPos = e.GetPosition(listBoxItem);
            var index = items.IndexOf(targetItem);
            if (index >= 0)
            {
                return (rPos.X <= listBoxItem.Bounds.Width / 2 ? index - 1 : index, true);
            }
        }

        return (items.Count > 0 ? items.Count - 1 : -1, items.Count > 0);
    }

    private bool ValidateCore(ListBox listBox, DragEventArgs e, object? sourceContext, object? targetContext,
        bool execute)
    {
        e.Handled = true;
        if (_page == null) return false;

        var viewModel = _page.ViewModel;
        if (viewModel == null) return false;

        // 处理从组件库拖入的按钮（sourceContext 是 FloatingTriggerItem，来自按钮池）
        if (sourceContext is FloatingTriggerItem poolItem && targetContext is ObservableCollection<FloatingTriggerItem> targetList)
        {
            // 检查是否来自按钮池（不在任何行中）
            var isInRow = viewModel.FloatingTriggerRows.Any(r => r.Buttons.Any(b => b.ButtonId == poolItem.ButtonId));
            if (isInRow) return false;

            if (execute)
            {
                var (targetIndex, foundTargetIndex) = GetTargetIndex(listBox, e, targetList);
                var insertIndex = foundTargetIndex ? targetIndex + 1 : targetList.Count;

                // 找到目标行
                var targetRow = viewModel.FloatingTriggerRows.FirstOrDefault(r => r.Buttons == targetList);
                if (targetRow == null) return false;

                var rowIndex = viewModel.FloatingTriggerRows.IndexOf(targetRow);
                viewModel.AddTriggerFromPool(poolItem.ButtonId, rowIndex, insertIndex);
            }

            return true;
        }

        // 处理行内按钮拖拽排序/跨行移动
        if (sourceContext is FloatingWindowButtonDragData data
            && targetContext is ObservableCollection<FloatingTriggerItem> components)
        {
            if (data.Item == null) return false;

            var (targetIndex, foundTargetIndex) = GetTargetIndex(listBox, e, components);
            var insertIndex = foundTargetIndex ? targetIndex + 1 : components.Count;

            if (execute)
            {
                var targetRow = viewModel.FloatingTriggerRows.FirstOrDefault(r => r.Buttons == components);
                if (targetRow == null) return false;
                var rowIndex = viewModel.FloatingTriggerRows.IndexOf(targetRow);

                if (data.SourceCollection != null && !ReferenceEquals(data.SourceCollection, components))
                {
                    // 跨行移动
                    var sourceRow = viewModel.FloatingTriggerRows.FirstOrDefault(r => r.Buttons == data.SourceCollection);
                    if (sourceRow == null) return false;
                    var sourceRowIndex = viewModel.FloatingTriggerRows.IndexOf(sourceRow);
                    var sourceIndex = data.SourceCollection.IndexOf(data.Item);
                    if (sourceIndex < 0) return false;

                    viewModel.MoveFloatingTrigger(data.Item.ButtonId, rowIndex, insertIndex);
                }
                else
                {
                    // 行内排序
                    var sourceIndex = components.IndexOf(data.Item);
                    if (sourceIndex < 0) return false;

                    if (ReferenceEquals(data.SourceCollection, components))
                    {
                        var moveIndex = foundTargetIndex ? targetIndex : components.Count - 1;
                        var newIndex = sourceIndex > moveIndex ? moveIndex + 1 : moveIndex;
                        MoveItem(components, sourceIndex, System.Math.Clamp(newIndex, 0, components.Count - 1));
                        viewModel.PersistFloatingTriggerRows();
                    }
                }
            }

            return true;
        }

        return false;
    }

    public override bool Validate(object? sender, DragEventArgs e, object? sourceContext, object? targetContext,
        object? state)
    {
        if (e.Handled) return false;
        return sender switch
        {
            ListBox listBox => ValidateCore(listBox, e, sourceContext, targetContext, false),
            _ => false
        };
    }

    public override bool Execute(object? sender, DragEventArgs e, object? sourceContext, object? targetContext,
        object? state)
    {
        if (e.Handled) return false;
        return sender switch
        {
            ListBox listBox => ValidateCore(listBox, e, sourceContext, targetContext, true),
            _ => false
        };
    }
}
