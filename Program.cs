namespace MuteMic;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        // System.Windows.Application can only ever be constructed once per process,
        // so it's created here, up front, rather than lazily when Settings first opens.
        _ = new App();

        Application.Run(new TrayApp());
    }
}
