using System.Text;
using Terminal.Gui;

namespace JSSP.UI.Views;

/// <summary>
/// A Terminal.Gui <see cref="View"/> that renders a live ASCII line chart of
/// algorithm convergence: best makespan (Y-axis) plotted against generation
/// number (X-axis). Each call to <see cref="AddPoint"/> appends a data point
/// and schedules a repaint.
/// </summary>
/// <remarks>
/// Thread safety: <see cref="AddPoint"/> and all field mutations must be called
/// from the UI thread (i.e., inside <c>Application.Invoke</c>).
/// <see cref="OnDrawingContent"/> is invoked by Terminal.Gui on the UI thread —
/// no additional synchronisation is required.
/// </remarks>
public class ConvergenceGraphView : View
{
    private readonly List<(int Generation, int Makespan)> _points = [];
    private int _minMakespan = int.MaxValue;
    private int _maxMakespan = int.MinValue;
    private int _maxGeneration;

    // Y-axis: 5-digit number + '┤' separator = 6 chars wide.
    private const int YAxisWidth = 6;

    // X-axis baseline (1 row) + generation label row (1 row).
    private const int XAxisHeight = 2;

    /// <summary>
    /// Appends a new convergence data point and schedules a repaint.
    /// Must be called on the UI thread.
    /// </summary>
    /// <param name="generation">Generation index from the convergence event.</param>
    /// <param name="makespan">Best makespan observed up to this generation.</param>
    public void AddPoint(int generation, int makespan)
    {
        _points.Add((generation, makespan));
        if (makespan < _minMakespan) _minMakespan = makespan;
        if (makespan > _maxMakespan) _maxMakespan = makespan;
        if (generation > _maxGeneration) _maxGeneration = generation;
        SetNeedsDraw();
    }

    /// <inheritdoc/>
    protected override bool OnDrawingContent()
    {
        base.OnDrawingContent();

        // Viewport gives the drawable area in terminal cells.
        int totalWidth = Viewport.Width;
        int totalHeight = Viewport.Height;

        int plotWidth = totalWidth - YAxisWidth;
        int plotHeight = totalHeight - XAxisHeight;

        if (plotWidth <= 0 || plotHeight <= 0) return true;

        DrawXAxis(plotHeight, plotWidth);
        DrawXAxisLabels(plotHeight, plotWidth);

        if (_points.Count > 0)
        {
            DrawYAxisLabels(plotHeight);
            PlotPoints(plotWidth, plotHeight);
        }

        return true;
    }

    // -------------------------------------------------------------------------
    // Rendering helpers (called on UI thread from OnDrawingContent)
    // -------------------------------------------------------------------------

    private void DrawYAxisLabels(int plotHeight)
    {
        DrawYLabel(row: 0, value: _maxMakespan);
        DrawYLabel(row: plotHeight / 2, value: (_minMakespan + _maxMakespan) / 2);
        DrawYLabel(row: plotHeight - 1, value: _minMakespan);
    }

    private void DrawYLabel(int row, int value)
    {
        Move(0, row);
        AddStr(value.ToString().PadLeft(5));
        Move(5, row);
        AddRune((Rune)'┤');
    }

    private void DrawXAxis(int plotHeight, int plotWidth)
    {
        Move(YAxisWidth - 1, plotHeight);
        AddRune((Rune)'└');
        for (int c = 0; c < plotWidth; c++)
        {
            Move(YAxisWidth + c, plotHeight);
            AddRune((Rune)'─');
        }
    }

    private void DrawXAxisLabels(int plotHeight, int plotWidth)
    {
        int labelRow = plotHeight + 1;

        Move(YAxisWidth, labelRow);
        AddStr("0");

        if (_maxGeneration == 0) return;

        string rightLabel = _maxGeneration.ToString();
        int rightCol = YAxisWidth + plotWidth - rightLabel.Length;
        if (rightCol > YAxisWidth + 2)
        {
            Move(rightCol, labelRow);
            AddStr(rightLabel);
        }

        string midLabel = (_maxGeneration / 2).ToString();
        int midCol = YAxisWidth + plotWidth / 2 - midLabel.Length / 2;
        if (midCol > YAxisWidth + 2 && midCol + midLabel.Length < rightCol - 2)
        {
            Move(midCol, labelRow);
            AddStr(midLabel);
        }
    }

    private void PlotPoints(int plotWidth, int plotHeight)
    {
        // Pre-compute all screen-space coordinates so we can draw lines first,
        // then overdraw data-point markers on top.
        var scaled = new (int Col, int Row)[_points.Count];
        for (int i = 0; i < _points.Count; i++)
        {
            scaled[i] = (
                YAxisWidth + ScaleToColumn(_points[i].Generation, _maxGeneration, plotWidth),
                ScaleToRow(_points[i].Makespan, _minMakespan, _maxMakespan, plotHeight)
            );
        }

        // Draw connecting lines between consecutive scaled points.
        for (int i = 1; i < scaled.Length; i++)
            DrawSegment(scaled[i - 1].Col, scaled[i - 1].Row, scaled[i].Col, scaled[i].Row);

        // Draw data-point markers on top of the lines.
        foreach (var (col, row) in scaled)
        {
            Move(col, row);
            AddRune((Rune)'●');
        }
    }

    /// <summary>
    /// Draws a line from (x0, y0) to (x1, y1) using Bresenham's algorithm.
    /// Characters are chosen based on the local slope so the line reads
    /// naturally: '─' horizontal, '│' vertical, '╱' or '╲' diagonal.
    /// Endpoint cells are left blank — the caller draws markers there.
    /// </summary>
    private void DrawSegment(int x0, int y0, int x1, int y1)
    {
        int dx = Math.Abs(x1 - x0);
        int dy = Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;
        int x = x0;
        int y = y0;

        while (true)
        {
            bool isEndpoint = (x == x0 && y == y0) || (x == x1 && y == y1);

            if (!isEndpoint)
            {
                Move(x, y);
                AddRune((Rune)PickLineChar(dx, dy, sx, sy, err));
            }

            if (x == x1 && y == y1) break;

            int e2 = 2 * err;
            bool stepX = e2 > -dy;
            bool stepY = e2 < dx;
            if (stepX) { err -= dy; x += sx; }
            if (stepY) { err += dx; y += sy; }
        }
    }

    /// <summary>
    /// Picks a Unicode box/line character that matches the dominant direction
    /// of the current Bresenham step.
    /// </summary>
    private static char PickLineChar(int dx, int dy, int sx, int sy, int err)
    {
        // Horizontal step dominates → flat line character.
        if (dx > dy) return '─';

        // Vertical step dominates → vertical bar.
        if (dy > dx) return '│';

        // Diagonal — direction determines slash orientation.
        // sx and sy are +1 or -1; matching signs means top-left→bottom-right (╲),
        // opposing signs means bottom-left→top-right (╱).
        return sx == sy ? '╲' : '╱';
    }

    // -------------------------------------------------------------------------
    // Internal scaling helpers — static and side-effect-free so unit tests can
    // call them directly without initialising a Terminal.Gui Application.
    // -------------------------------------------------------------------------

    /// <summary>
    /// Maps a generation index to a column offset within the plot area
    /// [0, <paramref name="plotWidth"/>-1].
    /// </summary>
    internal static int ScaleToColumn(int generation, int maxGeneration, int plotWidth)
    {
        if (maxGeneration == 0 || plotWidth <= 1) return 0;
        return (int)Math.Round((double)generation / maxGeneration * (plotWidth - 1));
    }

    /// <summary>
    /// Maps a makespan value to a row index within the plot area
    /// [0, <paramref name="plotHeight"/>-1].
    /// Row 0 is the top (worst / highest makespan);
    /// row <paramref name="plotHeight"/>-1 is the bottom (best / lowest makespan).
    /// </summary>
    internal static int ScaleToRow(int makespan, int minMakespan, int maxMakespan, int plotHeight)
    {
        // All values equal — render as a single horizontal line at the bottom.
        if (maxMakespan == minMakespan || plotHeight <= 1) return plotHeight - 1;

        double ratio = (double)(makespan - minMakespan) / (maxMakespan - minMakespan);
        return (int)Math.Round((1.0 - ratio) * (plotHeight - 1));
    }
}
