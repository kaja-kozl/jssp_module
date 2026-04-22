using System.Collections.ObjectModel;
using Terminal.Gui;
using JSSP.Algorithms;
using JSSP.Core.Orchestration;

namespace JSSP.UI.Views;

/// <summary>
/// View that lists available optimisation algorithms and lets the user select one.
/// Algorithm names are sourced from <see cref="AlgorithmFactory"/> rather than
/// reflection, so adding a new algorithm only requires updating the factory.
/// </summary>
public class ChooseAlgorithmView : FrameView
{
    private readonly ListView _algorithmsView;
    private readonly ActionButton _confirmButton;
    private readonly ViewManager _viewManager;
    private readonly FlowContext _context;

    /// <summary>Initialises a new instance of <see cref="ChooseAlgorithmView"/>.</summary>
    public ChooseAlgorithmView(ViewManager manager, FlowContext context) : base()
    {
        Title = "Select Algorithm";
        X = 0;
        Y = 0;
        Width = Dim.Fill();
        Height = Dim.Fill();

        _viewManager = manager;
        _context = context;

        var instructionLabel = new Label
        {
            Text = "Select an optimisation algorithm, then press Confirm.",
            X = 1,
            Y = 1,
        };

        int jobCount = context.Jobs?.Count ?? 0;
        string jobsText = jobCount > 0
            ? $"Loaded: {jobCount} job(s)"
            : "No jobs loaded";

        var jobInfoLabel = new Label
        {
            Text = jobsText,
            X = 1,
            Y = Pos.Bottom(instructionLabel),
        };

        _algorithmsView = new ListView
        {
            X = 1,
            Y = Pos.Bottom(jobInfoLabel) + 1,
            Width = Dim.Fill(1),
            Height = Dim.Fill(6),
        };

        var algorithmSource = new ObservableCollection<string>(AlgorithmFactory.GetAvailableAlgorithms());
        bool isStub = false;

        if (algorithmSource.Count == 0)
        {
            algorithmSource.Add("No algorithms found — check AlgorithmFactory");
            _algorithmsView.CanFocus = false;
            isStub = true;
        }

        _algorithmsView.SetSource(algorithmSource);

        var warningLabel = new Label
        {
            Text = string.Empty,
            X = 1,
            Y = Pos.AnchorEnd(4),
            Width = Dim.Fill(1),
        };

        _confirmButton = new ActionButton
        {
            Text = "  Confirm  ",
            X = Pos.Center() - 9,
            Y = Pos.AnchorEnd(2),
        };

        var compareAllButton = new ActionButton
        {
            Text = "Compare All",
            X = Pos.Center() + 3,
            Y = Pos.AnchorEnd(2),
            Enabled = !isStub,
        };

        _algorithmsView.SelectedItemChanged += (_, args) =>
        {
            if (isStub || args.Item < 0 || args.Item >= algorithmSource.Count)
                return;

            bool isBbTooLarge = algorithmSource[args.Item] == nameof(BranchBound)
                && context.Jobs is not null
                && !BranchBound.IsViable(context.Jobs);

            if (isBbTooLarge)
            {
                int ops = context.Jobs!.Sum(j => j.Operations.Count);
                warningLabel.Text =
                    $"Branch & Bound requires ≤ {BranchBound.MaxTractableOperations} " +
                    $"total operations. This instance has {ops}.";
                _confirmButton.Enabled = false;
            }
            else
            {
                warningLabel.Text = string.Empty;
                _confirmButton.Enabled = true;
            }
        };

        _confirmButton.OnClickAction = () =>
        {
            if (isStub || _algorithmsView.SelectedItem < 0) return;

            _context.SelectedAlgorithmName = algorithmSource[_algorithmsView.SelectedItem];
            _viewManager.ShowOptimisingView();
        };

        compareAllButton.OnClickAction = () => _viewManager.ShowCompareAllView();

        Add(instructionLabel, jobInfoLabel, _algorithmsView, warningLabel,
            _confirmButton, compareAllButton);
    }
}
