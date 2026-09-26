namespace MuteMic;

// Program.cs (WinForms Application.Run) drives the real message loop.
// This exists only so WPF windows have an Application.Current with the
// WPF-UI fluent/dark resource dictionaries merged in.
public partial class App : System.Windows.Application
{
    public App() => InitializeComponent();
}
