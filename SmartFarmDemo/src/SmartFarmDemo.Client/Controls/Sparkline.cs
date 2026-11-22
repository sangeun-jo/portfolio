using System.Windows;
using System.Windows.Media;

namespace SmartFarmDemo.Client;

public class Sparkline : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty =
        DependencyProperty.Register(nameof(Values), typeof(IEnumerable<double>), typeof(Sparkline),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeProperty =
        DependencyProperty.Register(nameof(Stroke), typeof(Brush), typeof(Sparkline),
            new FrameworkPropertyMetadata(Brushes.DodgerBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    public IEnumerable<double>? Values
    {
        get => (IEnumerable<double>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public Brush Stroke
    {
        get => (Brush)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var values = Values?.ToList();
        if (values is null || values.Count < 2) return;

        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        var min = values.Min();
        var max = values.Max();
        var range = max - min;
        if (range < 0.0001) range = 1;

        var pen = new Pen(Stroke, 1.8) { LineJoin = PenLineJoin.Round };
        var step = w / (values.Count - 1);

        for (int i = 1; i < values.Count; i++)
        {
            var x1 = (i - 1) * step;
            var y1 = h - ((values[i - 1] - min) / range) * (h - 6) - 3;
            var x2 = i * step;
            var y2 = h - ((values[i] - min) / range) * (h - 6) - 3;
            dc.DrawLine(pen, new Point(x1, y1), new Point(x2, y2));
        }
    }
}
