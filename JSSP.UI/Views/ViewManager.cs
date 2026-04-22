using Terminal.Gui;
using JSSP.Core.Orchestration;

namespace JSSP.UI.Views;

/// <summary>
/// Manages view transitions by swapping child views inside the shared container.
/// </summary>
public class ViewManager
{
    private Toplevel? _mainWindow;
    private View? _currentView;
    private FrameView? _viewContainer;
    private readonly FlowContext _context;

    /// <summary>Initialises a new <see cref="ViewManager"/> with the given shared context.</summary>
    public ViewManager(FlowContext context)
    {
        _context = context;
    }

    /// <summary>Sets the root window that owns this manager.</summary>
    public void SetMainWindow(Toplevel window) => _mainWindow = window;

    /// <summary>Sets the container into which views are rendered.</summary>
    public void SetViewContainer(FrameView container) => _viewContainer = container;

    /// <summary>Shows the CSV file-selection view.</summary>
    public void ShowFileSelectionView()
    {
        if (_viewContainer is null) return;
        _viewContainer.RemoveAll();
        _currentView = new ChooseFileView(this, _context);
        _viewContainer.Add(_currentView);
    }

    /// <summary>Shows the algorithm-selection view.</summary>
    public void ShowAlgorithmSelectionView()
    {
        if (_viewContainer is null) return;
        _viewContainer.RemoveAll();
        _currentView = new ChooseAlgorithmView(this, _context);
        _viewContainer.Add(_currentView);
    }

    /// <summary>Shows the optimising view.</summary>
    public void ShowOptimisingView()
    {
        if (_viewContainer is null) return;
        _viewContainer.RemoveAll();
        _currentView = new OptimisingPage(this, _context);
        _viewContainer.Add(_currentView);
    }

    /// <summary>Shows the compare-all-algorithms view.</summary>
    public void ShowCompareAllView()
    {
        if (_viewContainer is null) return;
        _viewContainer.RemoveAll();
        _currentView = new CompareAllPage(this, _context);
        _viewContainer.Add(_currentView);
    }

    /// <summary>Terminates the application cleanly.</summary>
    public void Quit() => Application.RequestStop();
}
