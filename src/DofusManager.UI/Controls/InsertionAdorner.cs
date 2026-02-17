using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace DofusManager.UI.Controls;

/// <summary>
/// Adorner qui dessine une ligne d'insertion horizontale avec des triangles aux extrémités.
/// Indique visuellement où l'élément sera inséré lors d'un drag-and-drop.
/// </summary>
public class InsertionAdorner : Adorner
{
    private readonly bool _isAbove;
    private readonly AdornerLayer _adornerLayer;

    private static readonly Pen LinePen;
    private static readonly PathGeometry Triangle;

    static InsertionAdorner()
    {
        LinePen = new Pen { Brush = Brushes.DodgerBlue, Thickness = 2 };
        LinePen.Freeze();

        var firstLine = new LineSegment(new Point(0, -5), false);
        firstLine.Freeze();
        var secondLine = new LineSegment(new Point(0, 5), false);
        secondLine.Freeze();

        var figure = new PathFigure { StartPoint = new Point(5, 0) };
        figure.Segments.Add(firstLine);
        figure.Segments.Add(secondLine);
        figure.Freeze();

        Triangle = new PathGeometry();
        Triangle.Figures.Add(figure);
        Triangle.Freeze();
    }

    public InsertionAdorner(bool isAbove, UIElement adornedElement, AdornerLayer adornerLayer)
        : base(adornedElement)
    {
        _isAbove = isAbove;
        _adornerLayer = adornerLayer;
        IsHitTestVisible = false;
        _adornerLayer.Add(this);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var width = AdornedElement.RenderSize.Width;
        var height = AdornedElement.RenderSize.Height;

        Point start, end;

        if (_isAbove)
        {
            start = new Point(0, 0);
            end = new Point(width, 0);
        }
        else
        {
            start = new Point(0, height);
            end = new Point(width, height);
        }

        // Ligne
        drawingContext.DrawLine(LinePen, start, end);

        // Triangle gauche (pointe vers la droite)
        DrawTriangle(drawingContext, start, 0);

        // Triangle droit (pointe vers la gauche)
        DrawTriangle(drawingContext, end, 180);
    }

    private static void DrawTriangle(DrawingContext drawingContext, Point origin, double angle)
    {
        drawingContext.PushTransform(new TranslateTransform(origin.X, origin.Y));
        drawingContext.PushTransform(new RotateTransform(angle));
        drawingContext.DrawGeometry(LinePen.Brush, null, Triangle);
        drawingContext.Pop();
        drawingContext.Pop();
    }

    public void Detach()
    {
        _adornerLayer.Remove(this);
    }
}
