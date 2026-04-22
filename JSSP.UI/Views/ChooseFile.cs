using System.Collections.ObjectModel;
using Terminal.Gui;
using JSSP.Core.Orchestration;
using JSSP.IO;

namespace JSSP.UI.Views;

/// <summary>
/// View that lists available CSV files and lets the user select one to load.
/// CSV files must be placed in the same directory as the executable.
/// </summary>
public class ChooseFileView : FrameView
{
    private readonly ListView _filesView;
    private readonly ActionButton _confirmButton;
    private readonly ViewManager _viewManager;
    private readonly FlowContext _context;

    /// <summary>
    /// Returns the names of CSV files found in the application's base directory.
    /// </summary>
    public static ObservableCollection<string> FetchFiles()
    {
        string[] files = Directory.GetFiles(AppContext.BaseDirectory, "*.csv")
                                  .Select(Path.GetFileName)
                                  .Where(f => f is not null)
                                  .ToArray()!;
        return new ObservableCollection<string>(files);
    }

    /// <summary>Initialises a new instance of <see cref="ChooseFileView"/>.</summary>
    public ChooseFileView(ViewManager manager, FlowContext context) : base()
    {
        Title = "Select File";
        X = 0;
        Y = 0;
        Width = Dim.Fill();
        Height = Dim.Fill();

        _viewManager = manager;
        _context = context;

        var instructionLabel = new Label
        {
            Text = "Select a CSV job-data file, then press Confirm.",
            X = 1,
            Y = 1,
        };

        var directoryLabel = new Label
        {
            Text = $"Looking in: {AppContext.BaseDirectory}",
            X = 1,
            Y = Pos.Bottom(instructionLabel),
        };

        _filesView = new ListView
        {
            X = 1,
            Y = Pos.Bottom(directoryLabel) + 1,
            Width = Dim.Fill(1),
            Height = Dim.Fill(4),
        };

        var fileSource = FetchFiles();
        bool isStub = false;

        if (fileSource.Count == 0)
        {
            fileSource.Add("No CSV files found — place .csv files next to the executable");
            _filesView.CanFocus = false;
            isStub = true;
        }

        _filesView.SetSource(fileSource);
        _filesView.AllowsMarking = false;

        _confirmButton = new ActionButton
        {
            Text = "  Confirm  ",
            X = Pos.Center(),
            Y = Pos.AnchorEnd(2),
        };

        _confirmButton.OnClickAction = () =>
        {
            if (isStub || _filesView.SelectedItem < 0) return;

            string selectedFileName = fileSource[_filesView.SelectedItem];
            string fullPath = Path.Combine(AppContext.BaseDirectory, selectedFileName);

            try
            {
                _context.Jobs = CsvLoader.Load(fullPath);
                _viewManager.ShowAlgorithmSelectionView();
            }
            catch (Exception ex)
            {
                MessageBox.ErrorQuery("Load Error", ex.Message, "OK");
            }
        };

        Add(instructionLabel, directoryLabel, _filesView, _confirmButton);
    }
}
