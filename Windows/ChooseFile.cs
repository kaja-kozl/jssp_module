using System.Collections.ObjectModel;
using Terminal.Gui;



// Defines a top-level window with border and title
public class ChooseFileWindow : Window {
    public ListView filesView { get; internal set; }
    public Button confirmButton { get; internal set; }

    public static ObservableCollection<string> fetchFiles()
    {
        string?[] files = Directory.GetFiles("bin\\Debug\\net10.0", "*.csv")
                                    .Select(Path.GetFileName)
                                    .ToArray();
        return new ObservableCollection<string>(files!);
    }
    public ChooseFileWindow ()
    {
        Title = "JSSP Optimizer";

        // ListView displaying all the files that fit the pattern bin\Debug\net10.01\*.csv
         filesView = new ListView () {
            X = 0,
            Y = 0,
            Width = Dim.Fill (),
            Height = Dim.Fill (),
        };

        filesView.SetSource(fetchFiles());
        filesView.AllowsMarking = true;

        Add (filesView);

        // Button to confirm the selection
        confirmButton = new Button () {
            Text = "Confirm",
            X = Pos.Center (),
            Y = Pos.Bottom (filesView) + 1,
        };
    }
}