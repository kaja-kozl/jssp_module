using System.Collections.ObjectModel;
using Terminal.Gui;
using JSSP.Algorithms;
using JSSP.Core.Interfaces;
using JSSP.Core.Models;
using JSSP.Core.Orchestration;

namespace JSSP.UI.Views;

/// <summary>
/// View that runs every registered algorithm sequentially on the loaded jobs
/// and presents a side-by-side comparison table.
///
/// Each algorithm gets its own <see cref="CancellationTokenSource"/> with a
/// <see cref="UiTimeoutSeconds"/>-second timeout. Results rows appear in the table
/// as each algorithm finishes. All Terminal.Gui mutations are marshalled to the
/// main thread via <see cref="Application.Invoke"/>.
/// </summary>
public class CompareAllPage : FrameView
{
    private const int UiPopulationSize = 50;
    private const int UiGenerations = 100;
    private const int UiRepetitions = 3;
    private const int UiTimeoutSeconds = 60;

    // Column widths for the formatted table.
    private const int ColName = 20;
    private const int ColBest = 6;
    private const int ColMean = 7;
    private const int ColStdDev = 7;
    private const int ColTime = 7;

    private static readonly string TableHeader =
        FormatRow("Algorithm", "Best", "Mean", "Std Dev", "Time", "Status");

    private static readonly string TableSeparator =
        "  " + new string('─', TableHeader.Length - 2);

    private readonly Label _statusLabel;
    private readonly ObservableCollection<string> _rows;
    private readonly ListView _rowsView;

    /// <summary>
    /// Initialises the view and immediately starts running all algorithms
    /// sequentially on a background <see cref="Task"/>.
    /// </summary>
    public CompareAllPage(ViewManager manager, FlowContext context) : base()
    {
        Title = "Compare All Algorithms";
        X = 0;
        Y = 0;
        Width = Dim.Fill();
        Height = Dim.Fill();

        var headerLabel = new Label
        {
            Text = $"Jobs: {context.Jobs?.Count ?? 0}   " +
                   $"Pop: {UiPopulationSize}   " +
                   $"Gen: {UiGenerations}   " +
                   $"Reps: {UiRepetitions}   " +
                   $"Timeout: {UiTimeoutSeconds}s per algorithm",
            X = 1,
            Y = 1,
        };

        _statusLabel = new Label
        {
            Text = "Status: Starting...",
            X = 1,
            Y = Pos.Bottom(headerLabel) + 1,
        };

        var tableHeaderLabel = new Label
        {
            Text = TableHeader,
            X = 0,
            Y = Pos.Bottom(_statusLabel) + 1,
        };

        var separatorLabel = new Label
        {
            Text = TableSeparator,
            X = 0,
            Y = Pos.Bottom(tableHeaderLabel),
        };

        var resultsFrame = new FrameView
        {
            Title = "Results",
            X = 0,
            Y = Pos.Bottom(separatorLabel) + 1,
            Width = Dim.Fill(),
            Height = Dim.Fill(4),
        };

        _rows = new ObservableCollection<string>();
        _rowsView = new ListView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            CanFocus = false,
        };
        _rowsView.SetSource(_rows);
        resultsFrame.Add(_rowsView);

        var runAgainButton = new ActionButton
        {
            Text = "Run Again",
            X = Pos.Center() - 10,
            Y = Pos.AnchorEnd(2),
            Enabled = false,
        };

        var newFileButton = new ActionButton
        {
            Text = "New File",
            X = Pos.Center() + 2,
            Y = Pos.AnchorEnd(2),
            Enabled = false,
        };

        runAgainButton.OnClickAction = () => manager.ShowAlgorithmSelectionView();
        newFileButton.OnClickAction = () => manager.ShowFileSelectionView();

        Add(headerLabel, _statusLabel, tableHeaderLabel, separatorLabel,
            resultsFrame, runAgainButton, newFileButton);

        if (context.Jobs is null || context.Jobs.Count == 0)
        {
            _statusLabel.Text = "Error: no jobs loaded. Use \"New File\" to go back.";
            runAgainButton.Enabled = true;
            newFileButton.Enabled = true;
            return;
        }

        var baseConfig = new AlgorithmConfig
        {
            PopulationSize = UiPopulationSize,
            Generations = UiGenerations,
            Repetitions = UiRepetitions,
            EliteCount = 2,
            MutationRate = 0.02,
            TournamentSize = 5,
        };

        var names = AlgorithmFactory.GetAvailableAlgorithms();
        var capturedJobs = context.Jobs;

        Task.Run(() => RunAllSequentially(names, capturedJobs, baseConfig))
            .ContinueWith(t =>
                Application.Invoke(() =>
                {
                    context.CompareResults = t.IsFaulted ? [] : t.Result;
                    _statusLabel.Text = t.IsFaulted
                        ? $"Unexpected error: {t.Exception?.InnerException?.Message ?? "unknown"}"
                        : "All algorithms complete.";
                    runAgainButton.Enabled = true;
                    newFileButton.Enabled = true;
                }));
    }

    // -------------------------------------------------------------------------
    // Background worker — called on a Task thread, never touches Views directly.
    // All View mutations are delegated to Application.Invoke.
    // -------------------------------------------------------------------------

    private List<AlgorithmComparisonResult> RunAllSequentially(
        IReadOnlyList<string> names,
        List<Job> jobs,
        AlgorithmConfig baseConfig)
    {
        var results = new List<AlgorithmComparisonResult>();

        for (int i = 0; i < names.Count; i++)
        {
            string name = names[i];
            int indexCapture = i;

            Application.Invoke(() =>
                _statusLabel.Text = $"Running {name}...  ({indexCapture + 1}/{names.Count})");

            if (name == nameof(BranchBound) && !BranchBound.IsViable(jobs))
            {
                int ops = jobs.Sum(j => j.Operations.Count);
                var tooBig = new AlgorithmComparisonResult(
                    name, null,
                    $"Instance too large ({ops} ops > {BranchBound.MaxTractableOperations})");
                results.Add(tooBig);
                Application.Invoke(() => AppendRow(tooBig));
                continue;
            }

            IAlgorithm algorithm;
            try
            {
                algorithm = AlgorithmFactory.Create(name);
            }
            catch (ArgumentException ex)
            {
                var factoryError = new AlgorithmComparisonResult(name, null, ex.Message);
                results.Add(factoryError);
                Application.Invoke(() => AppendRow(factoryError));
                continue;
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(UiTimeoutSeconds));
            var config = new AlgorithmConfig
            {
                PopulationSize = baseConfig.PopulationSize,
                Generations = baseConfig.Generations,
                Repetitions = baseConfig.Repetitions,
                EliteCount = baseConfig.EliteCount,
                MutationRate = baseConfig.MutationRate,
                TournamentSize = baseConfig.TournamentSize,
                CancellationToken = cts.Token,
            };

            string capturedName = name;
            algorithm.OnConvergence += (_, args) =>
                Application.Invoke(() =>
                    _statusLabel.Text =
                        $"Running {capturedName}...  Gen {args.Generation}  Best: {args.BestMakespan}");

            AlgorithmComparisonResult result;
            try
            {
                algorithm.Solve(jobs, config);
                var stats = algorithm.GetStats();
                string? warning = cts.IsCancellationRequested ? "(timed out)" : null;
                result = new AlgorithmComparisonResult(capturedName, stats, warning);
            }
            catch (Exception ex)
            {
                string msg = ex.InnerException?.Message ?? ex.Message;
                result = new AlgorithmComparisonResult(capturedName, null, msg);
            }

            results.Add(result);
            Application.Invoke(() => AppendRow(result));
        }

        return results;
    }

    // -------------------------------------------------------------------------
    // UI helpers — must only be called on the main thread (inside Application.Invoke).
    // -------------------------------------------------------------------------

    private void AppendRow(AlgorithmComparisonResult result)
    {
        string row;

        if (result.HasError || result.Stats is null)
        {
            string errMsg = result.ErrorMessage ?? "unknown error";
            if (errMsg.Length > 22) errMsg = errMsg[..19] + "...";
            row = FormatRow(result.AlgorithmName, "—", "—", "—", "—", errMsg);
        }
        else
        {
            var s = result.Stats;
            string elapsed = s.ElapsedTime.TotalSeconds >= 60
                ? s.ElapsedTime.ToString(@"m\:ss") + "m"
                : s.ElapsedTime.TotalSeconds.ToString("F1") + "s";

            string status = result.ErrorMessage is not null ? result.ErrorMessage : "Done";
            row = FormatRow(
                result.AlgorithmName,
                s.BestMakespan.ToString(),
                s.MeanMakespan.ToString("F1"),
                s.StdDev.ToString("F1"),
                elapsed,
                status);
        }

        _rows.Add(row);
        _rowsView.SelectedItem = _rows.Count - 1;
    }

    private static string FormatRow(
        string name, string best, string mean, string stdDev, string time, string status)
    {
        if (name.Length > ColName) name = name[..ColName];
        return $"  {name,-20} | {best,ColBest} | {mean,ColMean} | {stdDev,ColStdDev} | {time,ColTime} | {status}";
    }
}
