using System.Collections.Generic;
using System.Collections.ObjectModel;
using Terminal.Gui;
using JSSP.Algorithms;
using JSSP.Core.Interfaces;
using JSSP.Core.Models;
using JSSP.Core.Orchestration;
using JSSP.IO;
using JSSP.UI.Theme;

namespace JSSP.UI.Views;

/// <summary>
/// View that runs the selected algorithm on a background <see cref="Task"/>,
/// displays live convergence progress, and shows final results on completion.
///
/// Thread-safety rule: all Terminal.Gui widget mutations happen inside
/// <see cref="Application.Invoke"/> callbacks so they execute on the UI thread.
/// </summary>
public class OptimisingPage : FrameView
{
    // UI-appropriate run config (reduced from academic defaults for responsiveness).
    // Will be replaced by AlgorithmConfigView when that view is built.
    private const int UiPopulationSize = 50;
    private const int UiGenerations = 100;
    private const int UiRepetitions = 3;
    private const int UiTimeoutSeconds = 60;

    private readonly Label _statusLabel;
    private readonly ObservableCollection<string> _logLines;
    private readonly ListView _logView;
    private readonly ConvergenceGraphView _graphView;
    private readonly Label _resultsLabel;
    private readonly ActionButton _runAgainButton;
    private readonly ActionButton _newFileButton;
    private readonly ActionButton _downloadButton;
    private readonly CancellationTokenSource _cts;

    // Convergence-event tracking (read/written only on the UI thread via Application.Invoke).
    private int _currentBestMakespan;
    private int _eventCount;
    private readonly List<ConvergencePoint> _convergenceLog = [];

    /// <summary>
    /// Initialises the view, wires the convergence handler, and immediately starts
    /// the algorithm on a background <see cref="Task"/>.
    /// </summary>
    public OptimisingPage(ViewManager manager, FlowContext context) : base()
    {
        Title = $"Optimising — {context.SelectedAlgorithmName ?? "Unknown"}";
        X = 0;
        Y = 0;
        Width = Dim.Fill();
        Height = Dim.Fill();

        _cts = new CancellationTokenSource(TimeSpan.FromSeconds(UiTimeoutSeconds));
        _currentBestMakespan = int.MaxValue;

        var runConfig = new AlgorithmConfig
        {
            PopulationSize = UiPopulationSize,
            Generations = UiGenerations,
            Repetitions = UiRepetitions,
            EliteCount = 2,
            MutationRate = 0.02,
            TournamentSize = 5,
            CancellationToken = _cts.Token,
        };

        // --- Header ---
        var headerLabel = new Label
        {
            Text = $"Algorithm: {context.SelectedAlgorithmName ?? "?"}   " +
                   $"Jobs: {context.Jobs?.Count ?? 0}   " +
                   $"Pop: {runConfig.PopulationSize}   " +
                   $"Gen: {runConfig.Generations}   " +
                   $"Reps: {runConfig.Repetitions}   " +
                   $"Timeout: {UiTimeoutSeconds}s",
            X = 1,
            Y = 1,
        };

        // --- Live status line ---
        _statusLabel = new Label
        {
            Text = "Status: Starting...",
            X = 1,
            Y = Pos.Bottom(headerLabel) + 1,
        };

        // --- Convergence log (left 50 %) — logs improvements as text lines.
        var logFrame = new FrameView
        {
            Title = "Log  (improvements only)",
            X = 0,
            Y = Pos.Bottom(_statusLabel) + 1,
            Width = Dim.Percent(50),
            Height = Dim.Fill(9),
        };
        _logLines = new ObservableCollection<string>();
        _logView = new ListView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            CanFocus = false,
        };
        _logView.SetSource(_logLines);
        logFrame.ColorScheme = AppTheme.Log;
        logFrame.Add(_logView);

        // --- Convergence graph (right 50 %) — live ASCII line chart.
        var graphFrame = new FrameView
        {
            Title = "Convergence Graph",
            X = Pos.Percent(50),
            Y = Pos.Bottom(_statusLabel) + 1,
            Width = Dim.Fill(),
            Height = Dim.Fill(9),
            ColorScheme = AppTheme.Graph,
        };
        _graphView = new ConvergenceGraphView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ColorScheme = AppTheme.Graph,
        };
        graphFrame.Add(_graphView);

        // --- Results summary — pinned 8 rows from the bottom, fixed 4-line height.
        //     Visible = false until the run completes. Hidden views still reserve
        //     their layout space in Terminal.Gui, so buttons stay stable while running.
        _resultsLabel = new Label
        {
            Text = string.Empty,
            X = 1,
            Y = Pos.AnchorEnd(8),
            Width = Dim.Fill(2),
            Height = 4,
            Visible = false,
        };

        // --- Buttons — all pinned from the bottom so they are always visible. ---
        _runAgainButton = new ActionButton
        {
            Text = "Run Again",
            X = Pos.Center() - 10,
            Y = Pos.AnchorEnd(2),
            Enabled = false,
        };
        _newFileButton = new ActionButton
        {
            Text = "New File",
            X = Pos.Center() + 2,
            Y = Pos.AnchorEnd(2),
            Enabled = false,
        };

        _downloadButton = new ActionButton
        {
            Text = "Export Results",
            X = Pos.Center(),
            Y = Pos.AnchorEnd(4),
            Enabled = false,
        };

        _runAgainButton.OnClickAction = () => manager.ShowAlgorithmSelectionView();
        _newFileButton.OnClickAction = () => manager.ShowFileSelectionView();
        _downloadButton.OnClickAction = () =>
        {
            if (context.Jobs is null || context.BestSchedule is null)
            {
                MessageBox.ErrorQuery("Export Error", "No schedule available to export.", "OK");
                return;
            }

            int? format = PromptForFormat();
            if (format is null)
                return;

            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string baseDir = AppContext.BaseDirectory;
            string path = format.Value switch
            {
                0 => System.IO.Path.Combine(baseDir, $"gantt_{stamp}.xlsx"),
                1 => System.IO.Path.Combine(baseDir, $"schedule_{stamp}.csv"),
                _ => System.IO.Path.Combine(baseDir, $"schedule_{stamp}.json"),
            };

            try
            {
                switch (format.Value)
                {
                    case 0:
                        GanttChartExporter.Export(context.Jobs, context.BestSchedule, path);
                        break;
                    case 1:
                        ResultsExporter.ExportCsvAsync(
                            context.LastStats ?? new AlgorithmStats(),
                            context.BestSchedule,
                            context.Jobs,
                            path).GetAwaiter().GetResult();
                        break;
                    default:
                        ResultsExporter.ExportJsonAsync(
                            context.LastStats ?? new AlgorithmStats(),
                            context.BestSchedule,
                            context.Jobs,
                            path).GetAwaiter().GetResult();
                        break;
                }
                MessageBox.Query("Exported", $"Saved to:\n{path}", "OK");
            }
            catch (Exception ex)
            {
                MessageBox.ErrorQuery("Export Error", ex.Message, "OK");
            }
        };

        Add(headerLabel, _statusLabel, logFrame, graphFrame, _resultsLabel,
            _runAgainButton, _newFileButton, _downloadButton);

        // Guard: validate context before attempting to run.
        if (context.Jobs is null || context.Jobs.Count == 0)
        {
            _statusLabel.Text = "Error: no jobs loaded. Use \"New File\" to go back.";
            _runAgainButton.Enabled = true;
            _newFileButton.Enabled = true;
            return;
        }

        if (context.SelectedAlgorithmName is null)
        {
            _statusLabel.Text = "Error: no algorithm selected. Use \"Run Again\" to go back.";
            _runAgainButton.Enabled = true;
            _newFileButton.Enabled = true;
            return;
        }

        // Create algorithm instance — show error if factory rejects the name.
        IAlgorithm algorithm;
        try
        {
            algorithm = AlgorithmFactory.Create(context.SelectedAlgorithmName);
        }
        catch (ArgumentException ex)
        {
            _statusLabel.Text = $"Error: {ex.Message}";
            _runAgainButton.Enabled = true;
            _newFileButton.Enabled = true;
            return;
        }

        // Subscribe to convergence events — handler must marshal to UI thread.
        algorithm.OnConvergence += (_, args) =>
            Application.Invoke(() => UpdateProgress(args, runConfig));

        // Capture locals for the lambda (avoids closure over mutable 'algorithm' field).
        var capturedAlgorithm = algorithm;
        var capturedJobs = context.Jobs;

        Task.Run(() => capturedAlgorithm.Solve(capturedJobs, runConfig))
            .ContinueWith(t =>
                Application.Invoke(() =>
                    HandleCompletion(t, capturedAlgorithm, context, manager)));
    }

    // -------------------------------------------------------------------------
    // Background-to-UI callbacks (always called via Application.Invoke)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Updates the status label and logs improvements. Called on the UI thread
    /// via <see cref="Application.Invoke"/> from the <c>OnConvergence</c> handler.
    /// </summary>
    private void UpdateProgress(ConvergenceEventArgs args, AlgorithmConfig config)
    {
        _eventCount++;

        bool isBounded = args.TotalIterations > 0;
        int currentRep = isBounded ? (_eventCount - 1) / config.Generations + 1 : 1;

        string progressText = isBounded
            ? $"Rep {currentRep}/{config.Repetitions}  Gen {args.Generation}/{args.TotalIterations}"
            : $"Improvement #{args.Generation}";

        _statusLabel.Text = $"Running...  {progressText}  Best: {args.BestMakespan}";

        // Record every generation for the convergence file and update the graph.
        // Use _eventCount (monotonically increasing across all repetitions) rather than
        // args.Generation (which resets to 1 at the start of each repetition) to prevent
        // Bresenham segments from jumping back to column 0 between reps.
        _convergenceLog.Add(new ConvergencePoint(args.Generation, args.BestMakespan));
        _graphView.AddPoint(_eventCount, args.BestMakespan);

        // Only append to the UI log when the global best improves.
        if (args.BestMakespan < _currentBestMakespan)
        {
            _currentBestMakespan = args.BestMakespan;
            string logEntry = isBounded
                ? $"  Rep {currentRep,2}  Gen {args.Generation,3}  →  makespan {args.BestMakespan}"
                : $"  Improvement #{args.Generation,3}  →  makespan {args.BestMakespan}";
            _logLines.Add(logEntry);

            // Scroll to the newest entry.
            _logView.SelectedItem = _logLines.Count - 1;
        }
    }

    /// <summary>
    /// Called on the UI thread when the background Task finishes (success, fault,
    /// or cancellation). Writes results to <see cref="FlowContext"/> and enables
    /// the navigation buttons.
    /// </summary>
    private void HandleCompletion(
        Task<Schedule> task,
        IAlgorithm algorithm,
        FlowContext context,
        ViewManager manager)
    {
        if (task.IsFaulted)
        {
            string msg = task.Exception?.InnerException?.Message ?? "Unknown error";
            _statusLabel.Text = $"Error during run.";
            MessageBox.ErrorQuery(
                "Algorithm Error",
                $"{msg}\n\nThis algorithm may not yet be implemented.\n\nUse Run Again to choose another.",
                "OK");
            _runAgainButton.Enabled = true;
            _newFileButton.Enabled = true;
            return;
        }

        context.BestSchedule = task.Result;
        context.LastStats = algorithm.GetStats();

        _statusLabel.Text = _cts.IsCancellationRequested
            ? $"Timed out after {UiTimeoutSeconds}s — best result found is shown below."
            : "Complete!";

        var stats = context.LastStats;
        if (stats is not null)
        {
            string elapsed = stats.ElapsedTime.TotalSeconds >= 60
                ? stats.ElapsedTime.ToString(@"m\:ss\.ff")
                : stats.ElapsedTime.ToString(@"s\.ff") + "s";

            _resultsLabel.Text =
                $"Results\n" +
                $"  Best makespan : {stats.BestMakespan}   " +
                $"Elapsed: {elapsed}\n" +
                $"  Mean makespan : {stats.MeanMakespan:F1}   Std dev: {stats.StdDev:F1}\n" +
                $"  Conv. gen     : {stats.ConvergenceGeneration}";
            _resultsLabel.Visible = true;
        }

        _runAgainButton.Enabled = true;
        _newFileButton.Enabled = true;
        _downloadButton.Enabled = true;
    }

    /// <summary>
    /// Shows a modal format-selection dialog and returns the chosen format index:
    /// 0 = Excel Gantt (.xlsx), 1 = CSV (.csv), 2 = JSON (.json),
    /// or <see langword="null"/> if the user cancelled.
    /// The file is saved automatically to <see cref="AppContext.BaseDirectory"/>
    /// with a timestamp name; no filename prompt is shown.
    /// </summary>
    private static int? PromptForFormat()
    {
        int? selected = null;

        var dlg = new Dialog
        {
            Title = "Export Results",
            Width = 46,
            Height = 12,
        };

        var promptLabel = new Label
        {
            Text = "Select export format:",
            X = 1,
            Y = 1,
        };

        var radioGroup = new RadioGroup
        {
            RadioLabels = new[]
            {
                "Excel Gantt Chart  (.xlsx)",
                "Schedule & Stats   (.csv) ",
                "Schedule & Stats   (.json)",
            },
            X = 1,
            Y = 3,
            SelectedItem = 0,
        };

        // Anchor buttons to the last content row so they are always visible
        // regardless of how TG 2.0 calculates the dialog's inner bounds.
        var exportBtn = new Button
        {
            Text = "Export",
            X = Pos.Center() - 7,
            Y = Pos.AnchorEnd(1),
        };

        var cancelBtn = new Button
        {
            Text = "Cancel",
            X = Pos.Center() + 2,
            Y = Pos.AnchorEnd(1),
        };

        exportBtn.Accepting += (_, _) =>
        {
            selected = radioGroup.SelectedItem;
            Application.RequestStop();
        };
        cancelBtn.Accepting += (_, _) => Application.RequestStop();

        dlg.Add(promptLabel, radioGroup, exportBtn, cancelBtn);
        Application.Run(dlg);
        dlg.Dispose();

        return selected;
    }
}
