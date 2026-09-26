namespace MuteMic;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        // The WPF Application (PresentationFramework/Core, milcore, ...) is only needed
        // when Settings opens (see TrayApp.Full.cs), so it's never constructed here.
        Application.Run(new TrayApp());
    }
}
