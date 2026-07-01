using WixToolset.BootstrapperApplicationApi;

namespace SakuBA;

internal static class Program
{
    private static int Main()
    {
        var application = new SakuBA_App();
        ManagedBootstrapperApplication.Run(application);
        return 0;
    }
}