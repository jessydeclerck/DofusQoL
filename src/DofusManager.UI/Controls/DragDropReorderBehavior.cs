using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using DofusManager.UI.ViewModels;

namespace DofusManager.UI.Controls;

/// <summary>
/// Attached behavior pour le drag-and-drop de réordonnement dans un ListBox de CharacterRowViewModel.
/// Affiche un InsertionAdorner (ligne bleue) pour indiquer la position de destination.
/// </summary>
public static class DragDropReorderBehavior
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(DragDropReorderBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

    private static Point _dragStartPoint;
    private static bool _isDragging;
    private static int _dragFromIndex = -1;
    private static InsertionAdorner? _insertionAdorner;

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ListBox listBox) return;

        if ((bool)e.NewValue)
        {
            listBox.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
            listBox.PreviewMouseMove += OnPreviewMouseMove;
            listBox.DragOver += OnDragOver;
            listBox.DragLeave += OnDragLeave;
            listBox.Drop += OnDrop;
            listBox.AllowDrop = true;
        }
        else
        {
            listBox.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
            listBox.PreviewMouseMove -= OnPreviewMouseMove;
            listBox.DragOver -= OnDragOver;
            listBox.DragLeave -= OnDragLeave;
            listBox.Drop -= OnDrop;
            listBox.AllowDrop = false;
        }
    }

    private static void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
        _isDragging = false;

        // Ne pas interférer avec les contrôles interactifs (CheckBox, TextBox, HotkeyCaptureBox)
        if (e.OriginalSource is DependencyObject source &&
            (FindAncestor<CheckBox>(source) is not null || FindAncestor<TextBox>(source) is not null))
        {
            _dragFromIndex = -1;
            return;
        }

        if (sender is ListBox listBox)
        {
            var item = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
            _dragFromIndex = item is not null
                ? listBox.ItemContainerGenerator.IndexFromContainer(item)
                : -1;
        }
    }

    private static void OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragFromIndex < 0) return;

        var currentPos = e.GetPosition(null);
        var diff = _dragStartPoint - currentPos;

        if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        // Ne pas déclencher le drag si on est sur un contrôle interactif
        if (e.OriginalSource is DependencyObject source &&
            (FindAncestor<CheckBox>(source) is not null || FindAncestor<TextBox>(source) is not null))
            return;

        if (sender is ListBox listBox && !_isDragging)
        {
            _isDragging = true;
            var data = new DataObject("DragDropReorderIndex", _dragFromIndex);
            DragDrop.DoDragDrop(listBox, data, DragDropEffects.Move);
            _isDragging = false;
            RemoveInsertionAdorner();
        }
    }

    private static void OnDragOver(object sender, DragEventArgs e)
    {
        if (sender is not ListBox listBox) return;
        if (!e.Data.GetDataPresent("DragDropReorderIndex")) return;

        e.Effects = DragDropEffects.Move;
        e.Handled = true;

        // Trouver le ListBoxItem sous le curseur
        var targetItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
        if (targetItem is null)
        {
            RemoveInsertionAdorner();
            return;
        }

        var targetIndex = listBox.ItemContainerGenerator.IndexFromContainer(targetItem);
        var fromIndex = (int)e.Data.GetData("DragDropReorderIndex")!;
        if (targetIndex < 0 || targetIndex == fromIndex)
        {
            RemoveInsertionAdorner();
            return;
        }

        // Déterminer si le curseur est dans la moitié haute ou basse de l'élément
        var posInItem = e.GetPosition(targetItem);
        var isAbove = posInItem.Y < targetItem.ActualHeight / 2;

        // Recréer l'adorner si la position a changé
        RemoveInsertionAdorner();

        var adornerLayer = AdornerLayer.GetAdornerLayer(targetItem);
        if (adornerLayer is not null)
        {
            _insertionAdorner = new InsertionAdorner(isAbove, targetItem, adornerLayer);
        }
    }

    private static void OnDragLeave(object sender, DragEventArgs e)
    {
        RemoveInsertionAdorner();
    }

    private static void OnDrop(object sender, DragEventArgs e)
    {
        RemoveInsertionAdorner();

        if (sender is not ListBox listBox) return;
        if (!e.Data.GetDataPresent("DragDropReorderIndex")) return;

        var fromIndex = (int)e.Data.GetData("DragDropReorderIndex")!;

        // Trouver l'index de destination
        var targetItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
        if (targetItem is null) return;

        var toIndex = listBox.ItemContainerGenerator.IndexFromContainer(targetItem);
        if (toIndex < 0 || fromIndex == toIndex) return;

        // Ajuster l'index selon la moitié haute/basse
        var posInItem = e.GetPosition(targetItem);
        var isAbove = posInItem.Y < targetItem.ActualHeight / 2;

        // Si on drop dans la moitié basse et qu'on vient d'au-dessus, on veut l'index +1
        // Si on drop dans la moitié haute et qu'on vient d'en-dessous, on veut l'index -1
        // Mais Move() gère déjà les décalages, donc on utilise l'index tel quel
        // sauf si on est en moitié basse d'un élément au-dessus de la source
        if (!isAbove && toIndex < fromIndex)
            toIndex++;
        else if (isAbove && toIndex > fromIndex)
            toIndex--;

        if (toIndex < 0 || toIndex >= listBox.Items.Count || fromIndex == toIndex) return;

        if (listBox.DataContext is DashboardViewModel vm)
        {
            vm.MoveCharacter(fromIndex, toIndex);
        }

        e.Handled = true;
    }

    private static void RemoveInsertionAdorner()
    {
        _insertionAdorner?.Detach();
        _insertionAdorner = null;
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T found) return found;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}
