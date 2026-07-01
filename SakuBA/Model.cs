using WixToolset.BootstrapperApplicationApi;

namespace SakuBA;

public class Model
{
    private const string BurnInstallDirectoryVariable = "InstallFolder";
    private const string BurnVersionVariable = "WixBundleVersion";

    public Model(SakuBA_App bootstrapper)
    {
        BootstrapManifest = bootstrapper.BootstrapManifest;
        Bootstrapper = bootstrapper;
        Command = bootstrapper.Command;
        Engine = bootstrapper.Engine;
        Version = Engine?.GetVariableVersion(BurnVersionVariable) ?? "";
    }

    public IBootstrapperApplicationData? BootstrapManifest { get; }
    public IDefaultBootstrapperApplication Bootstrapper { get; }
    public IBootstrapperCommand? Command { get; }
    public IEngine? Engine { get; }
    public int Result { get; set; }
    public string Version { get; private set; }
    public LaunchAction PlannedAction { get; set; }

    public string InstallDirectory
    {
        get
        {
            if (Engine?.ContainsVariable(BurnInstallDirectoryVariable) == false)
                return string.Empty;
            return Engine?.GetVariableString(BurnInstallDirectoryVariable) ?? string.Empty;
        }
        set => Engine?.SetVariableString(BurnInstallDirectoryVariable, value, false);
    }

    public string GetPackageName(string packageId)
    {
        return BootstrapManifest?.Bundle.Packages.TryGetValue(packageId, out var package) == true ? package.DisplayName : packageId;
    }
}