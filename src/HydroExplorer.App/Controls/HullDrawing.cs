using System.Globalization;
using Avalonia;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace HydroExplorer.App.Controls;

/// <summary>
/// Colours and small drawing helpers shared by the two hull controls (side view and isometric view),
/// so both draw the water and labels the same way.
/// </summary>
internal static class HullDrawing
{
    public const double LabelFontSize = 12;

    // Immutable brushes and pens can be shared safely between controls and threads.
    public static readonly IBrush SeaBrush = new ImmutableSolidColorBrush(Color.Parse("#331E88E5"));
    public static readonly IBrush SubmergedBrush = new ImmutableSolidColorBrush(Color.Parse("#991E88E5"));
    public static readonly IPen WaterlinePen = new ImmutablePen(Color.Parse("#1E88E5").ToUInt32(), 2);

    /// <summary>Draws <paramref name="text"/> vertically centred on <paramref name="anchor"/>.</summary>
    public static void DrawLabel(DrawingContext context, string text, Point anchor, IBrush brush, HorizontalAlignment alignment)
    {
        var formatted = new FormattedText(
            text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, LabelFontSize, brush);

        double x = alignment switch
        {
            HorizontalAlignment.Center => anchor.X - formatted.Width / 2,
            HorizontalAlignment.Right => anchor.X - formatted.Width,
            _ => anchor.X,
        };
        context.DrawText(formatted, new Point(x, anchor.Y - formatted.Height / 2));
    }

    /// <summary>The caption above either drawing: the current draft, or a hint when none is selected.</summary>
    public static string Caption(double? draft) => draft is null
        ? "Select a draft to show the waterline"
        : Format($"Waterline at T = {draft:F2} m");

    /// <summary>Formats an interpolated string with the user's culture (decimal separator etc.).</summary>
    public static string Format(FormattableString text) => text.ToString(CultureInfo.CurrentCulture);
}
