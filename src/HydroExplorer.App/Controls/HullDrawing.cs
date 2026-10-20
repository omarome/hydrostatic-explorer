using System.Globalization;
using Avalonia;
using Avalonia.Layout;
using Avalonia.Media;

namespace HydroExplorer.App.Controls;

/// <summary>
/// Small drawing helpers shared by the two hull controls (side view and isometric view), so both draw
/// labels and captions the same way. Colours are not here: each control exposes its own StyledProperty
/// brushes (see HullProfileView/HullIsometricView), set from Palette.axaml by a style in AppStyles.axaml.
/// </summary>
internal static class HullDrawing
{
    public const double LabelFontSize = 12;

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
