using System.Collections.ObjectModel;
using System.Reflection;
using Terminal.Gui;

// Defines a top-level window with border and title
public class ChooseAlgorithmWindow : Window {
    public ListView algorithmsView { get; internal set; }

    public static ObservableCollection<string> fetchAlgorithms()
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        IEnumerable<Type> algorithmTypes = assembly.GetTypes()
                                            .Where(t => typeof(IAlgorithm).IsAssignableFrom(t) 
                                            && !t.IsInterface && !t.IsAbstract);
        string[] algorithms = algorithmTypes.Select(t => t.Name).ToArray();
        algorithms[algorithms.Length - 1] = "All Algorithms (For Comparison)";
        foreach (string algorithm in algorithms)
        {
            Console.WriteLine(algorithm);
        }
        return new ObservableCollection<string>(algorithms);
    }

    public ChooseAlgorithmWindow ()
    {
        Title = "JSSP Optimizer";

        // ListView displaying all the algorithms
         algorithmsView = new ListView () {
            X = 0,
            Y = 0,
            Width = Dim.Fill (),
            Height = Dim.Fill ()
        };

        algorithmsView.SetSource(fetchAlgorithms());

        Add (algorithmsView);
    }
}