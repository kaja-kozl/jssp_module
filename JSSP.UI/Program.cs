using Terminal.Gui;
using JSSP.UI.Views;

namespace JSSP.UI;

/// <summary>Application entry point.</summary>
internal class Program
{
    static void Main(string[] args)
    {
        Application.Run<MainWindow>();
        Application.Shutdown();
    }
}
