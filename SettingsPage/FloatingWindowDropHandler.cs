using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace SystemTools;

/// <summary>
/// 悬浮窗按钮拖放处理器
/// 处理按钮在行内排序、跨行移动、从组件库添加
/// 使用标准 Avalonia DragDrop，不依赖 ClassIsland 的 ManagedDragDrop
/// </summary>
public class FloatingWindowDropHandler
{
    private readonly FloatingWindowEditorSettingsPage _page;

    public FloatingWindowDropHandler(FloatingWindowEditorSettingsPage page)
    {
        _page = page;
    }

    /// <summary>
    /// 处理按钮拖放到行内
    /// </summary>
    public bool HandleButtonDrop(ListBox targetListBox, DragEventArgs e,
        FloatingWindowButtonDragData dragData, ObservableCollection<FloatingTriggerItem> targetList)
    {
        var viewModel = _page.ViewModel;
        if (viewModel == null) return false;

        var (targetIndex, foundTargetIndex) = GetTargetIndex(targetListBox, e, targetList);
        var insertIndex = foundTargetIndex ? targetIndex + 1 : targetList.Count;

        // 找到目标行
        var targetRow = viewModel.FloatingTriggerRows.FirstOrDefault(r => r.Buttons == targetList);
        if (targetRow == null) return false;
        var rowIndex = viewModel.FloatingTriggerRows.IndexOf(targetRow);

        if (dragData.SourceCollection == null)
        {
            // 从组件库添加
            if (dragData.Item == null) return false;
            var isInRow = viewModel.FloatingTriggerRows.Any(r => r.Buttons.Any(b => b.ButtonId == dragData.Item.ButtonId));
            if (isInRow) return false;
            viewModel.AddTriggerFromPool(dragData.Item.ButtonId, rowIndex, insertIndex);
        }
        else if (!ReferenceEquals(dragData.SourceCollection, targetList))
        {
            // 跨行移动
            if (dragData.Item == null) return false;
            viewModel.MoveFloatingTrigger(dragData.Item.ButtonId, rowIndex, insertIndex);
        }
        else
        {
            // 行内排序
            if (dragData.Item == null) return false;
            var sourceIndex = targetList.IndexOf(dragData.Item);
            if (sourceIndex < 0) return false;
            var moveIndex = foundTargetIndex ? targetIndex : targetList.Count - 1;
            var newIndex = sourceIndex > moveIndex ? moveIndex + 1 : moveIndex;
            MoveItem(targetList, sourceIndex, System.Math.Clamp(newIndex, 0, targetList.Count - 1));
            viewModel.PersistFloatingTriggerRows();
        }

        return true;
    }

    /// <summary>
    /// 处理从组件库拖入的按钮（SourceCollection 为 null）
    /// </summary>
    public bool HandlePoolItemDrop(ListBox targetListBox, DragEventArgs e,
        FloatingTriggerItem poolItem, ObservableCollection<FloatingTriggerItem> targetList)
    {
        var viewModel = _page.ViewModel;
        if (viewModel == null) return false;

        var isInRow = viewModel.FloatingTriggerRows.Any(r => r.Buttons.Any(b => b.ButtonId == poolItem.ButtonId));
        if (isInRow) return false;

        var (targetIndex, foundTargetIndex) = GetTargetIndex(targetListBox, e, targetList);
        var insertIndex = foundTargetIndex ? targetIndex + 1 : targetList.Count;

        var targetRow = viewModel.FloatingTriggerRows.FirstOrDefault(r => r.Buttons == targetList);
        if (targetRow == null) return false;
        var rowIndex = viewModel.FloatingTriggerRows.IndexOf(targetRow);

        viewModel.AddTriggerFromPool(poolItem.ButtonId, rowIndex, insertIndex);
        return true;
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

    private static void MoveItem(ObservableCollection<FloatingTriggerItem> list, int oldIndex, int newIndex)
    {
        if (oldIndex == newIndex) return;
        var item = list[oldIndex];
        list.RemoveAt(oldIndex);
        list.Insert(newIndex, item);
    }
}
