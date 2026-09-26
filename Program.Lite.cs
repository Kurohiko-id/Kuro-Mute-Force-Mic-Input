namespace MuteMic;

static class ProgramLite
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApp());
    }
}
