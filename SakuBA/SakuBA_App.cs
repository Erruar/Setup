using System.Diagnostics;
using WixToolset.BootstrapperApplicationApi;
using Threading = System.Windows.Threading;

namespace SakuBA;

public class SakuBA_App : BootstrapperApplication
{
    internal IBootstrapperApplicationData? BootstrapManifest { get; private set; }
    internal IBootstrapperCommand? Command { get; private set; }
    internal IEngine Engine => engine;

    public static Model? Model { get; private set; }
    public static RootView? View { get; private set; }
    public static Threading.Dispatcher? Dispatcher { get; private set; }

    public static void LaunchUrl(string uri)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = uri,
            UseShellExecute = true
        });
    }

    public static void OpenLog(string logPath)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = logPath,
            UseShellExecute = true
        });
    }

    public static void Plan(LaunchAction action)
    {
        Model?.PlannedAction = action;
        Model?.Engine?.Plan(action, BundleScope.Default);
    }

    protected override void Run()
    {
        Model = new Model(this);
        Dispatcher = Threading.Dispatcher.CurrentDispatcher;

        var viewModel = new RootViewModel();
        View = new RootView(viewModel);

        if (Model.Command?.Display is Display.Passive or Display.Full)
        {
            View.Show();
        }

        Engine.Detect();

        Threading.Dispatcher.Run();

        var exitCode = Model.Result;
        if ((exitCode & 0xFFFF0000) == 0x80070000)
        {
            exitCode &= 0xFFFF;
        }
        
        Engine.Quit(exitCode);
    }

    protected override void OnCreate(CreateEventArgs args)
    {
        base.OnCreate(args);
        Command = args.Command;
        BootstrapManifest = new BootstrapperApplicationData();
    }
}
