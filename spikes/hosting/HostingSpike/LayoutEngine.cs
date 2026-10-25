using System.Windows;

namespace HostingSpike;

public enum LayoutMode { Tabs, Columns, Rows, Grid, Focus }

/// <summary>Where one pane sits in the shell's grid. Invisible slots are collapsed (Tabs mode).</summary>
public readonly record struct Slot(int Row, int Col, int RowSpan, int ColSpan, bool Visible);

public sealed record LayoutPlan(GridLength[] Columns, GridLength[] Rows, Slot[] Slots);

/// <summary>Pure layout maths from docs/tech/05: equal fractions, grid shapes 2×1, 2+1, 2×2, 3+2, 3×2, 3×3, focus = main plus a 360 px strip.</summary>
public static class LayoutEngine
{
    public const double StripWidth = 360;

    public static LayoutPlan Plan(LayoutMode mode, int n, int active, int focus)
    {
        if (n <= 0) return new LayoutPlan(new[] { Star(1) }, new[] { Star(1) }, Array.Empty<Slot>());
        active = Math.Clamp(active, 0, n - 1);
        focus = Math.Clamp(focus, 0, n - 1);
        switch (mode)
        {
            case LayoutMode.Tabs:
                return new LayoutPlan(new[] { Star(1) }, new[] { Star(1) }, Enumerable.Range(0, n).Select(i => new Slot(0, 0, 1, 1, i == active)).ToArray());
            case LayoutMode.Columns:
                return new LayoutPlan(Stars(n), new[] { Star(1) }, Enumerable.Range(0, n).Select(i => new Slot(0, i, 1, 1, true)).ToArray());
            case LayoutMode.Rows:
                return new LayoutPlan(new[] { Star(1) }, Stars(n), Enumerable.Range(0, n).Select(i => new Slot(i, 0, 1, 1, true)).ToArray());
            case LayoutMode.Focus:
            {
                if (n == 1) return new LayoutPlan(new[] { Star(1) }, new[] { Star(1) }, new[] { new Slot(0, 0, 1, 1, true) });
                var rows = n - 1;
                var slots = new Slot[n];
                var strip = 0;
                for (var i = 0; i < n; i++)
                    slots[i] = i == focus ? new Slot(0, 0, rows, 1, true) : new Slot(strip++, 1, 1, 1, true);
                return new LayoutPlan(new[] { Star(1), new GridLength(StripWidth) }, Stars(rows), slots);
            }
            default:
            {
                var cols = (int)Math.Ceiling(Math.Sqrt(n));
                var rows = (int)Math.Ceiling(n / (double)cols);
                var last = n - (rows - 1) * cols;
                var units = Lcm(cols, last);
                var slots = new Slot[n];
                for (var i = 0; i < n; i++)
                {
                    var row = i / cols;
                    if (row < rows - 1) slots[i] = new Slot(row, (i % cols) * (units / cols), 1, units / cols, true);
                    else { var j = i - (rows - 1) * cols; slots[i] = new Slot(row, j * (units / last), 1, units / last, true); }
                }
                return new LayoutPlan(Stars(units), Stars(rows), slots);
            }
        }
    }

    private static GridLength Star(double v) => new(v, GridUnitType.Star);
    private static GridLength[] Stars(int n) => Enumerable.Repeat(Star(1), n).ToArray();
    private static int Gcd(int a, int b) => b == 0 ? a : Gcd(b, a % b);
    private static int Lcm(int a, int b) => a / Gcd(a, b) * b;
}
