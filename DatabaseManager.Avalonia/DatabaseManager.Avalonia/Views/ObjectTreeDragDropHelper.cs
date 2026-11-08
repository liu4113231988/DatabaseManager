using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DatabaseManager.AppCore.Models;

namespace DatabaseManager.Avalonia.Views;

/// <summary>
/// 对象树拖拽源行为：从树节点拖拽对象名/SQL 模板到编辑器，
/// 以及拖拽悬停 500ms 自动展开下一层节点。
/// </summary>
public sealed class ObjectTreeDragDropHelper
{
    private static readonly Point DragThreshold = new(4, 4);
    private const int AutoExpandHoverMs = 500;

    private readonly TreeView _tree;
    private DbObjectTreeNode? _dragStartNode;
    private Point _dragStartPoint;
    private bool _isDragging;
    private PointerPressedEventArgs? _dragPressArgs;
    private DispatcherTimer? _autoExpandTimer;
    private DbObjectTreeNode? _autoExpandTarget;

    public ObjectTreeDragDropHelper(TreeView tree)
    {
        _tree = tree;

        // Avalonia 12 中 TreeViewItem 在容器内直接处理 PointerPressed 并标记 Handled=true，
        // 必须用 handledEventsToo:true 才能收到按下事件。
        tree.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Bubble, handledEventsToo: true);
        tree.PointerMoved += OnPointerMoved;
        tree.AddHandler(InputElement.PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Bubble, handledEventsToo: true);
        tree.PointerCaptureLost += OnPointerCaptureLost;
        tree.AddHandler(DragDrop.DragOverEvent, OnDragOver);
        tree.AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(_tree).Properties.IsLeftButtonPressed)
            return;

        var point = e.GetPosition(_tree);
        var hitNode = FindNodeAtPosition(point);
        if (hitNode is null || !IsDraggableNode(hitNode))
            return;

        _dragStartNode = hitNode;
        _dragStartPoint = point;
        _dragPressArgs = e;
        _isDragging = false;
    }

    private async void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragStartNode is null || _isDragging || _dragPressArgs is null)
            return;

        var point = e.GetPosition(_tree);
        var delta = _dragStartPoint - point;
        if (Math.Abs(delta.X) < DragThreshold.X && Math.Abs(delta.Y) < DragThreshold.Y)
            return;

        _isDragging = true;

        var mode = ResolveDragTextMode(e.KeyModifiers);
        var text = _dragStartNode.GetDragDropSqlText(mode);
        if (string.IsNullOrEmpty(text))
        {
            ResetDragState();
            return;
        }

        var data = new DataTransfer();
        data.Add(DataTransferItem.CreateText(text));

        e.Handled = true;
        await DragDrop.DoDragDropAsync(_dragPressArgs, data, DragDropEffects.Copy);

        ResetDragState();
    }

    private static DragTextMode ResolveDragTextMode(KeyModifiers modifiers)
    {
        bool ctrl = modifiers.HasFlag(KeyModifiers.Control);
        bool shift = modifiers.HasFlag(KeyModifiers.Shift);
        bool alt = modifiers.HasFlag(KeyModifiers.Alt);

        if (ctrl) return DragTextMode.PlainName;
        if (shift && !alt) return DragTextMode.SelectTemplate;
        if (alt) return DragTextMode.ColumnAlias;
        return DragTextMode.Default;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e) => ResetDragState();

    private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e) => ResetDragState();

    private void ResetDragState()
    {
        _dragStartNode = null;
        _dragPressArgs = null;
        _isDragging = false;
        CancelAutoExpand();
    }

    private void CancelAutoExpand()
    {
        _autoExpandTimer?.Stop();
        _autoExpandTarget = null;
    }

    private static bool IsDraggableNode(DbObjectTreeNode node)
    {
        if (node.IsPlaceholder || string.IsNullOrWhiteSpace(node.Name))
            return false;
        return node.NodeType == DbObjectTreeNodeType.DbObject
               || node.NodeType == DbObjectTreeNodeType.ChildObject;
    }

    private DbObjectTreeNode? FindNodeAtPosition(Point point)
    {
        var hit = _tree.InputHitTest(point);
        if (hit is null) return null;

        if (hit is TreeViewItem directItem && directItem.DataContext is DbObjectTreeNode directNode)
            return directNode;

        var visual = hit as Visual;
        while (visual is not null)
        {
            if (visual is TreeViewItem tvi && tvi.DataContext is DbObjectTreeNode node)
                return node;
            visual = visual.GetVisualParent();
        }
        return null;
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (_dragStartNode is null) return;

        var point = e.GetPosition(_tree);
        var target = FindNodeAtPosition(point);

        if (target is null || !CanAutoExpand(target))
        {
            CancelAutoExpand();
            return;
        }

        if (_autoExpandTarget != target)
        {
            _autoExpandTarget = target;
            StartAutoExpandTimer(target);
        }
    }

    private void OnDragLeave(object? sender, DragEventArgs e) => CancelAutoExpand();

    private static bool CanAutoExpand(DbObjectTreeNode node)
    {
        if (node.IsLoading || node.IsPlaceholder) return false;
        if (node.IsExpanded || node.Children.Count == 0) return false;

        return node.NodeType is DbObjectTreeNodeType.Connection
            or DbObjectTreeNodeType.Database
            or DbObjectTreeNodeType.Schema
            or DbObjectTreeNodeType.Folder
            or DbObjectTreeNodeType.DbObject
            or DbObjectTreeNodeType.ChildFolder;
    }

    private void StartAutoExpandTimer(DbObjectTreeNode target)
    {
        _autoExpandTimer?.Stop();

        var timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(AutoExpandHoverMs),
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();

            if (_autoExpandTarget == target && CanAutoExpand(target))
            {
                target.IsExpanded = true;
            }

            _autoExpandTarget = null;
            _autoExpandTimer = null;
        };

        _autoExpandTimer = timer;
        timer.Start();
    }
}