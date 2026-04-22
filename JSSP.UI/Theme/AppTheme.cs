using Terminal.Gui;

// Alias avoids the ambiguity between Terminal.Gui.Attribute and System.Attribute
// that arises from the implicit global usings in .NET 10.
using TAttr = Terminal.Gui.Attribute;

namespace JSSP.UI.Theme;

/// <summary>
/// Centralised colour palette for the JSSP TUI.
/// Call <see cref="Apply"/> once from <see cref="Views.MainWindow"/> after
/// <c>Application.Init</c> has run (i.e. inside the window constructor) to
/// push all schemes into <see cref="Colors.ColorSchemes"/>.
/// </summary>
internal static class AppTheme
{
    /// <summary>
    /// Dark base scheme — dark-gray background, white body text, cyan focus
    /// highlight, bright-yellow button hot-keys.
    /// </summary>
    internal static readonly ColorScheme Base = new()
    {
        Normal    = new TAttr(Color.White,        Color.DarkGray),
        Focus     = new TAttr(Color.Black,        Color.BrightCyan),
        HotNormal = new TAttr(Color.BrightYellow, Color.DarkGray),
        HotFocus  = new TAttr(Color.Black,        Color.BrightYellow),
        Disabled  = new TAttr(Color.Gray,         Color.DarkGray),
    };

    /// <summary>
    /// Convergence graph scheme — bright-green on black for maximum contrast
    /// on the ASCII plot.
    /// </summary>
    internal static readonly ColorScheme Graph = new()
    {
        Normal    = new TAttr(Color.BrightGreen, Color.Black),
        Focus     = new TAttr(Color.BrightGreen, Color.Black),
        HotNormal = new TAttr(Color.BrightGreen, Color.Black),
        HotFocus  = new TAttr(Color.BrightGreen, Color.Black),
        Disabled  = new TAttr(Color.Green,        Color.Black),
    };

    /// <summary>
    /// Log-panel scheme — bright-cyan on black, keeping the improvement log
    /// visually distinct from the main body text.
    /// </summary>
    internal static readonly ColorScheme Log = new()
    {
        Normal    = new TAttr(Color.BrightCyan, Color.Black),
        Focus     = new TAttr(Color.White,      Color.Black),
        HotNormal = new TAttr(Color.BrightCyan, Color.Black),
        HotFocus  = new TAttr(Color.White,      Color.Black),
        Disabled  = new TAttr(Color.Cyan,       Color.Black),
    };

    /// <summary>
    /// Pushes all named schemes into <see cref="Colors.ColorSchemes"/> so
    /// every view that has not set an explicit scheme inherits the dark theme.
    /// Must be called after <c>Application.Init</c>.
    /// </summary>
    internal static void Apply()
    {
        Colors.ColorSchemes["Base"]     = Base;
        Colors.ColorSchemes["TopLevel"] = Base;
        Colors.ColorSchemes["Dialog"]   = Base;
    }
}
