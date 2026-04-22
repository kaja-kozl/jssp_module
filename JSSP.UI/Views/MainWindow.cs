using Terminal.Gui;
using JSSP.Core.Orchestration;
using JSSP.UI.Theme;

namespace JSSP.UI.Views;

/// <summary>
/// Root window that owns the <see cref="ViewManager"/> and the shared <see cref="FlowContext"/>.
/// An "Exit" button is always visible at the bottom so the user can quit from any view.
/// </summary>
public class MainWindow : Window
{
    private readonly FrameView _viewContainer;
    private readonly ViewManager _viewManager;

    /// <summary>Initialises the main window and shows the initial file-selection view.</summary>
    public MainWindow()
    {
        Title = "JSSP Optimizer  ·  Job-Shop Scheduling Problem";

        // Push the dark theme into all named slots so every view inherits it.
        // AppTheme.Apply() must run after Application.Init (guaranteed here
        // because Application.Run<MainWindow> calls Init before creating the window).
        AppTheme.Apply();
        ColorScheme = AppTheme.Base;

        var context = new FlowContext();
        _viewManager = new ViewManager(context);
        _viewManager.SetMainWindow(this);

        // Leave 3 rows at the bottom for the Exit button and surrounding space.
        _viewContainer = new FrameView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(3),
        };

        var exitButton = new ActionButton
        {
            Text = "  Exit  ",
            X = Pos.Center(),
            Y = Pos.Bottom(_viewContainer) + 1,
        };
        exitButton.OnClickAction = () => Application.RequestStop();

        _viewManager.SetViewContainer(_viewContainer);
        Add(_viewContainer, exitButton);

        _viewManager.ShowFileSelectionView();
    }

    /// <summary>Returns the view manager for this window.</summary>
    public ViewManager GetViewManager() => _viewManager;
}
