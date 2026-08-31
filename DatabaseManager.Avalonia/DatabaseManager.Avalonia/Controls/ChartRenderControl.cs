using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using DatabaseManager.AppCore.Models;

namespace DatabaseManager.Avalonia.Controls;

/// <summary>
/// 轻量自绘图表控件（零依赖）：支持柱状图 / 折线图 / 饼图。
/// 数据源为 UI 无关的 <see cref="ChartRenderModel"/>；空数据时显示占位文本。
/// </summary>
public class ChartRenderControl : Control
{
    private static readonly string[] Palette =
    {
        "#1677FF", "#43A047", "#FB8C00", "#8E24AA", "#00ACC1",
        "#E53935", "#F6BF26", "#5E35B1", "#D81B60", "#6D4C41",
    };

    public static readonly StyledProperty<ChartRenderModel?> ChartProperty =
        AvaloniaProperty.Register<ChartRenderControl, ChartRenderModel?>(nameof(Chart));

    public ChartRenderModel? Chart
    {
        get => GetValue(ChartProperty);
        set => SetValue(ChartProperty, value);
    }

    static ChartRenderControl()
    {
        AffectsRender<ChartRenderControl>(ChartProperty);
    }

    public override void Render(DrawingContext context)
    {
        var model = Chart;
        var bounds = new Rect(default, Bounds.Size);

        context.FillRectangle(Brushes.Transparent, bounds);

        if (model is null || model.Series.Count == 0 || model.Labels.Count == 0)
        {
            DrawPlaceholder(context, bounds, model?.Title);
            return;
        }

        if (model.ChartType == ChartTypes.Pie)
        {
            DrawPie(context, bounds, model);
        }
        else
        {
            DrawAxisChart(context, bounds, model);
        }

        DrawTitle(context, bounds, model.Title);
    }

    private static void DrawPlaceholder(DrawingContext context, Rect bounds, string? title)
    {
        var text = string.IsNullOrWhiteSpace(title) ? "（无数据）" : $"{title}（无数据）";
        DrawText(context, text, bounds.Center, Typeface.Default, 12, GetBrush("#888888"), true);
    }

    private static void DrawTitle(DrawingContext context, Rect bounds, string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return;

        DrawText(context, title, new Point(bounds.Center.X, bounds.Top + 10), Typeface.Default, 12, GetBrush("#666666"), true);
    }

    private static void DrawPie(DrawingContext context, Rect bounds, ChartRenderModel model)
    {
        var series = model.Series[0];
        double total = series.Values.Sum();
        if (total <= 0)
        {
            DrawPlaceholder(context, bounds, model.Title);
            return;
        }

        double titleSpace = string.IsNullOrWhiteSpace(model.Title) ? 0 : 18;
        double radius = Math.Min(bounds.Width, bounds.Height) / 2 - 24 - titleSpace / 2;
        var center = new Point(bounds.Width / 2, (bounds.Height + titleSpace) / 2);

        if (radius <= 10)
            return;

        double startAngle = -90;

        for (int i = 0; i < series.Values.Count && i < model.Labels.Count; i++)
        {
            double value = Math.Max(0, series.Values[i]);
            double sweep = value / total * 360;
            if (sweep <= 0)
            {
                continue;
            }

            double startRad = startAngle * Math.PI / 180;
            double endRad = (startAngle + Math.Min(sweep, 359.99)) * Math.PI / 180;

            var startPoint = new Point(
                center.X + radius * Math.Cos(startRad),
                center.Y + radius * Math.Sin(startRad));
            var endPoint = new Point(
                center.X + radius * Math.Cos(endRad),
                center.Y + radius * Math.Sin(endRad));

            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(center, true);
                ctx.LineTo(startPoint, true);
                ctx.ArcTo(endPoint, new Size(radius, radius), 0, sweep > 180, SweepDirection.Clockwise);
                ctx.EndFigure(true);
            }

            var brush = GetBrush(SeriesColor(i, series.ColorHex));
            context.DrawGeometry(brush, new Pen(Brushes.White, 1), geometry);

            // 扇区外标注：标签 + 占比。
            double midRad = (startAngle + sweep / 2) * Math.PI / 180;
            var labelPoint = new Point(
                center.X + (radius + 14) * Math.Cos(midRad),
                center.Y + (radius + 14) * Math.Sin(midRad));
            double percent = value / total * 100;
            DrawText(context, $"{model.Labels[i]} {percent:0.#}%", labelPoint, Typeface.Default, 10, GetBrush("#666666"), true);

            startAngle += sweep;
        }
    }

    private static void DrawAxisChart(DrawingContext context, Rect bounds, ChartRenderModel model)
    {
        double titleSpace = string.IsNullOrWhiteSpace(model.Title) ? 0 : 16;
        double left = 46, right = 12, top = titleSpace + 6, bottom = 30;

        var plot = new Rect(left, top, Math.Max(10, bounds.Width - left - right), Math.Max(10, bounds.Height - top - bottom));

        double maxValue = 0;
        foreach (var s in model.Series)
        {
            foreach (var v in s.Values)
            {
                maxValue = Math.Max(maxValue, v);
            }
        }

        if (maxValue <= 0)
        {
            maxValue = 1;
        }

        // Y 轴网格线与刻度。
        var axisPen = new Pen(GetBrush("#DDDDDD"), 1, dashStyle: DashStyle.Dash);
        var axisTextBrush = GetBrush("#999999");

        for (int i = 0; i <= 4; i++)
        {
            double ratio = i / 4.0;
            double y = plot.Bottom - plot.Height * ratio;
            context.DrawLine(axisPen, new Point(plot.Left, y), new Point(plot.Right, y));
            DrawText(
                context,
                FormatAxisValue(maxValue * ratio),
                new Point(plot.Left - 6, y),
                Typeface.Default, 9, axisTextBrush, alignRight: true);
        }

        // 基线。
        context.DrawLine(new Pen(GetBrush("#BBBBBB"), 1), new Point(plot.Left, plot.Bottom), new Point(plot.Right, plot.Bottom));

        int labelCount = model.Labels.Count;
        double slot = plot.Width / Math.Max(1, labelCount);

        // X 轴标签。
        for (int i = 0; i < labelCount; i++)
        {
            var center = new Point(plot.Left + slot * (i + 0.5), plot.Bottom + 10);
            DrawText(context, Truncate(model.Labels[i], 8), center, Typeface.Default, 9, axisTextBrush, true);
        }

        if (model.ChartType == ChartTypes.Line)
        {
            DrawLines(context, plot, model, maxValue);
        }
        else
        {
            DrawBars(context, plot, model, maxValue);
        }

        DrawLegend(context, bounds, model);
    }

    private static void DrawBars(DrawingContext context, Rect plot, ChartRenderModel model, double maxValue)
    {
        int labelCount = Math.Max(1, model.Labels.Count);
        int seriesCount = Math.Max(1, model.Series.Count);
        double slot = plot.Width / labelCount;
        double groupWidth = slot * 0.7;
        double barWidth = Math.Max(2, groupWidth / seriesCount);

        for (int s = 0; s < model.Series.Count; s++)
        {
            var series = model.Series[s];
            var brush = GetBrush(SeriesColor(s, series.ColorHex));

            for (int i = 0; i < series.Values.Count && i < labelCount; i++)
            {
                double value = Math.Max(0, series.Values[i]);
                double height = plot.Height * value / maxValue;
                double x = plot.Left + slot * i + (slot - groupWidth) / 2 + barWidth * s;
                var rect = new Rect(x, plot.Bottom - height, barWidth * 0.9, height);
                context.FillRectangle(brush, rect);
            }
        }
    }

    private static void DrawLines(DrawingContext context, Rect plot, ChartRenderModel model, double maxValue)
    {
        int labelCount = Math.Max(1, model.Labels.Count);
        double slot = plot.Width / labelCount;

        foreach (var series in model.Series)
        {
            var brush = GetBrush(SeriesColor(model.Series.IndexOf(series), series.ColorHex));
            var pen = new Pen(brush, 2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);

            bool started = false;
            var previous = default(Point);
            int pointIndex = 0;

            for (int i = 0; i < labelCount; i++)
            {
                if (pointIndex >= series.Values.Count)
                {
                    break;
                }

                double value = Math.Max(0, series.Values[pointIndex++]);
                var point = new Point(
                    plot.Left + slot * (i + 0.5),
                    plot.Bottom - plot.Height * value / maxValue);

                if (started)
                {
                    context.DrawLine(pen, previous, point);
                }

                context.DrawEllipse(brush, null, point, 3, 3);
                previous = point;
                started = true;
            }
        }
    }

    private static void DrawLegend(DrawingContext context, Rect bounds, ChartRenderModel model)
    {
        if (model.Series.Count <= 1 || model.ChartType == ChartTypes.Pie)
            return;

        double x = bounds.Right - 10;
        const double itemHeight = 14;

        for (int i = model.Series.Count - 1; i >= 0; i--)
        {
            var series = model.Series[i];
            double width = 18 + MeasureText(series.Name, Typeface.Default, 10).Width;
            x -= width;
            if (x < bounds.Left + 40)
            {
                break;
            }

            double y = bounds.Top + 8;
            context.FillRectangle(GetBrush(SeriesColor(i, series.ColorHex)), new Rect(x, y + 2, 10, 10));
            DrawText(context, series.Name, new Point(x + 14, y + 7), Typeface.Default, 10, GetBrush("#666666"));
        }
    }

    private static string SeriesColor(int index, string? colorHex)
        => string.IsNullOrEmpty(colorHex) ? Palette[index % Palette.Length] : colorHex;

    private static string FormatAxisValue(double value)
        => value switch
        {
            >= 1_000_000 => $"{value / 1_000_000:0.#}M",
            >= 1_000 => $"{value / 1_000:0.#}k",
            _ => value.ToString("0.##"),
        };

    private static string Truncate(string text, int max)
    {
        text ??= string.Empty;
        return text.Length <= max ? text : text[..max] + "…";
    }

    private static FormattedText MeasureText(string text, Typeface typeface, double size)
        => new(
            text ?? string.Empty,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            size,
            Brushes.Transparent);

    private static void DrawText(
        DrawingContext context,
        string text,
        Point anchor,
        Typeface typeface,
        double size,
        IBrush brush,
        bool centered = false,
        bool alignRight = false)
    {
        var formatted = new FormattedText(
            text ?? string.Empty,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            size,
            brush);

        double x = alignRight ? anchor.X - formatted.Width : anchor.X;
        double y = anchor.Y - formatted.Height / 2;

        if (centered)
        {
            x = anchor.X - formatted.Width / 2;
        }

        context.DrawText(formatted, new Point(x, y));
    }

    private static IBrush GetBrush(string hex)
    {
        try
        {
            return new SolidColorBrush(Color.Parse(hex));
        }
        catch
        {
            return Brushes.Gray;
        }
    }
}
