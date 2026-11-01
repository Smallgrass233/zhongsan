using ZhongSanEditor.UI;

namespace ZhongSanEditor;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length > 0 && args[0].StartsWith("-"))
            return Cli.Run(args);

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        var startup = args.FirstOrDefault(a => !a.StartsWith("-"));
        Application.Run(new MainForm(startup));
        return 0;
    }
}
