using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using WixToolset.BootstrapperApplicationApi;
using ErrorEventArgs = WixToolset.BootstrapperApplicationApi.ErrorEventArgs;

namespace SakuBA;

public class InstallationViewModel : PropertyNotifyBase
{
    private readonly RootViewModel root;
    private readonly Dictionary<string, int> downloadRetries;
    private bool downgrade;
    private string downgradeMessage = string.Empty;

    private ICommand? installCommand;
    private ICommand? repairCommand;
    private ICommand? uninstallCommand;
    private ICommand? openLogCommand;
    private ICommand? openLogFolderCommand;
    private ICommand? tryAgainCommand;
    private ICommand? launchGitHubCommand;

    private string message = string.Empty;

    public InstallationViewModel(RootViewModel root)
    {
        this.root = root;
        downloadRetries = new Dictionary<string, int>();

        this.root.PropertyChanged += new PropertyChangedEventHandler(RootPropertyChanged);

        SakuBA_App.Model?.Bootstrapper.DetectBegin += DetectBegin;
        SakuBA_App.Model?.Bootstrapper.DetectRelatedBundle += DetectedRelatedBundle;
        SakuBA_App.Model?.Bootstrapper.DetectComplete += DetectComplete;
        SakuBA_App.Model?.Bootstrapper.PlanPackageBegin += PlanPackageBegin;
        SakuBA_App.Model?.Bootstrapper.PlanComplete += PlanComplete;
        SakuBA_App.Model?.Bootstrapper.ApplyBegin += ApplyBegin;
        SakuBA_App.Model?.Bootstrapper.CacheAcquireBegin += CacheAcquireBegin;
        SakuBA_App.Model?.Bootstrapper.CacheAcquireResolving += CacheAcquireResolving;
        SakuBA_App.Model?.Bootstrapper.CacheAcquireComplete += CacheAcquireComplete;
        SakuBA_App.Model?.Bootstrapper.ExecutePackageBegin += ExecutePackageBegin;
        SakuBA_App.Model?.Bootstrapper.ExecutePackageComplete += ExecutePackageComplete;
        SakuBA_App.Model?.Bootstrapper.Error += ExecuteError;
        SakuBA_App.Model?.Bootstrapper.ApplyComplete += ApplyComplete;
    }

    void RootPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "DetectState" or "UpgradeDetectState" or "InstallState")
        {
            base.OnPropertyChanged("RepairEnabled");
            base.OnPropertyChanged("InstallEnabled");
            base.OnPropertyChanged("IsComplete");
            base.OnPropertyChanged("IsSuccessfulCompletion");
            base.OnPropertyChanged("IsFailedCompletion");
            base.OnPropertyChanged("StatusText");
            base.OnPropertyChanged("UninstallEnabled");
        }
    }

    public string Version => "v" + SakuBA_App.Model?.Version;
    public string Publisher => "Saku Labs Inc.";
    public string SupportUrl => "https://github.com/Erruar/Saku-Overclock";
    public string GitHubUrl => "https://github.com/Erruar/Saku-Overclock";

    public string Message
    {
        get => message;
        set
        {
            if (message != value)
            {
                message = value;
                base.OnPropertyChanged("Message");
            }
        }
    }

    public bool Downgrade
    {
        get => downgrade;
        set
        {
            if (downgrade != value)
            {
                downgrade = value;
                base.OnPropertyChanged("Downgrade");
            }
        }
    }

    public string DowngradeMessage
    {
        get => downgradeMessage;
        set
        {
            if (downgradeMessage != value)
            {
                downgradeMessage = value;
                base.OnPropertyChanged("DowngradeMessage");
            }
        }
    }

    public ICommand LaunchGitHubCommand
    {
        get
        {
            launchGitHubCommand ??= new RelayCommand(_ => SakuBA_App.LaunchUrl(GitHubUrl), _ => true);
            return launchGitHubCommand;
        }
    }

    public ICommand CloseCommand => root.CloseCommand;

    public bool IsComplete => IsSuccessfulCompletion || IsFailedCompletion;
    public bool IsSuccessfulCompletion => root.InstallState == InstallationState.Applied;
    public bool IsFailedCompletion => root.InstallState == InstallationState.Failed;

    public ICommand InstallCommand
    {
        get
        {
            installCommand ??= new RelayCommand(
                _ => SakuBA_App.Plan(LaunchAction.Install),
                _ => root.DetectState == DetectionState.Absent && root.UpgradeDetectState != UpgradeDetectionState.Newer &&
                     root.InstallState == InstallationState.Waiting);
            return installCommand;
        }
    }

    public bool InstallEnabled => InstallCommand.CanExecute(null);

    public ICommand RepairCommand
    {
        get
        {
            if (repairCommand == null)
            {
                repairCommand = new RelayCommand(_ => SakuBA_App.Plan(LaunchAction.Repair), _ => root.DetectState == DetectionState.Present && root.InstallState == InstallationState.Waiting);
            }
            return repairCommand;
        }
    }

    public bool RepairEnabled => RepairCommand.CanExecute(null);

    public ICommand UninstallCommand
    {
        get
        {
            if (uninstallCommand == null)
            {
                uninstallCommand = new RelayCommand(_ => SakuBA_App.Plan(LaunchAction.Uninstall), _ => root.DetectState == DetectionState.Present && root.InstallState == InstallationState.Waiting);
            }
            return uninstallCommand;
        }
    }

    public bool UninstallEnabled => UninstallCommand.CanExecute(null);

    public ICommand OpenLogCommand
    {
        get
        {
            if (openLogCommand == null)
            {
                openLogCommand = new RelayCommand(_ => SakuBA_App.OpenLog(SakuBA_App.Model?.Engine?.GetVariableString("WixBundleLog") ?? string.Empty));
            }
            return openLogCommand;
        }
    }

    public ICommand OpenLogFolderCommand
    {
        get
        {
            if (openLogFolderCommand == null)
            {
                var logFolder = Path.GetDirectoryName(SakuBA_App.Model?.Engine?.GetVariableString("WixBundleLog") ?? "");
                openLogFolderCommand = new RelayCommand(_ => SakuBA_App.OpenLog(logFolder ?? ""));
            }
            return openLogFolderCommand;
        }
    }

    public ICommand TryAgainCommand
    {
        get
        {
            tryAgainCommand ??= new RelayCommand(_ =>
            {
                root.Canceled = false;
                if (SakuBA_App.Model != null)
                    SakuBA_App.Plan(SakuBA_App.Model.PlannedAction);
            }, _ => IsFailedCompletion);
            return tryAgainCommand;
        }
    }

    public string StatusText
    {
        get
        {
            switch (root.InstallState)
            {
                case InstallationState.Applied: return "Complete";
                case InstallationState.Failed: return root.Canceled ? "Cancelled" : "Failed";
                default: return "Unknown";
            }
        }
    }

    private void DetectBegin(object? sender, DetectBeginEventArgs e)
    {
        root.DetectState = RegistrationType.Full == e.RegistrationType ? DetectionState.Present : DetectionState.Absent;
        SakuBA_App.Model?.PlannedAction = LaunchAction.Unknown;
    }

    private void DetectedRelatedBundle(object? sender, DetectRelatedBundleEventArgs e)
    {
        if (e.RelationType == RelationType.Upgrade)
        {
            if (SakuBA_App.Model?.Engine?.CompareVersions(Version, e.Version) > 0)
            {
                if (root.UpgradeDetectState == UpgradeDetectionState.None)
                {
                    root.UpgradeDetectState = UpgradeDetectionState.Older;
                }
            }
            else
            {
                root.UpgradeDetectState = UpgradeDetectionState.Newer;
            }
        }

        if (SakuBA_App.Model?.BootstrapManifest?.Bundle.Packages.ContainsKey(e.ProductCode) == false)
        {
            SakuBA_App.Model.BootstrapManifest.Bundle.AddRelatedBundleAsPackage(e.ProductCode, e.RelationType, e.PerMachine, e.Version);
        }
    }

    private void DetectComplete(object? sender, DetectCompleteEventArgs e)
    {
        root.InstallState = InstallationState.Waiting;

        if (SakuBA_App.Model != null && SakuBA_App.Model.Command != null)
        {
            if (LaunchAction.Uninstall == SakuBA_App.Model.Command.Action && ResumeType.Arp != SakuBA_App.Model.Command.Resume)
            {
                SakuBA_App.Model.Engine?.Log(LogLevel.Verbose, "Invoking automatic plan for uninstall");
                SakuBA_App.Plan(LaunchAction.Uninstall);
            }
            else if (Hresult.Succeeded(e.Status))
            {
                if (root.UpgradeDetectState == UpgradeDetectionState.Newer)
                {
                    Downgrade = true;
                    DowngradeMessage = "There is already a newer version of Saku Overclock installed on this machine.";
                }

                if (LaunchAction.Layout == SakuBA_App.Model.Command.Action)
                {
                    // Layout handled separately
                }
                else if (SakuBA_App.Model?.Command.Display != Display.Full && SakuBA_App.Model?.Engine != null)
                {
                    SakuBA_App.Model.Engine.Log(LogLevel.Verbose, "Invoking automatic plan for non-interactive mode.");
                    SakuBA_App.Plan(SakuBA_App.Model.Command.Action);
                }
            }
            else
            {
                root.InstallState = InstallationState.Failed;
            }
        }
        root.Dispatcher?.Invoke(CommandManager.InvalidateRequerySuggested);
    }

    private void PlanPackageBegin(object? sender, PlanPackageBeginEventArgs e)
    {
        if (e.PackageId.StartsWith("NetFx4", StringComparison.OrdinalIgnoreCase) || e.PackageId.StartsWith("DesktopNetCoreRuntime", StringComparison.OrdinalIgnoreCase))
        {
            e.State = RequestState.None;
        }
    }

    private void PlanComplete(object? sender, PlanCompleteEventArgs e)
    {
        if (Hresult.Succeeded(e.Status))
        {
            root.PreApplyState = root.InstallState;
            root.InstallState = InstallationState.Applying;
            SakuBA_App.Model?.Engine?.Apply(root.ViewWindowHandle);
        }
        else
        {
            root.InstallState = InstallationState.Failed;
        }
    }

    private void ApplyBegin(object? sender, ApplyBeginEventArgs e)
    {
        downloadRetries.Clear();
    }

    private void CacheAcquireBegin(object? sender, CacheAcquireBeginEventArgs e) { }

    private void CacheAcquireResolving(object? sender, CacheAcquireResolvingEventArgs e)
    {
        if (e.Action == CacheResolveOperation.Download && !downloadRetries.ContainsKey(e.PackageOrContainerId ?? string.Empty))
        {
            downloadRetries.Add(e.PackageOrContainerId!, 0);
        }
    }

    private void CacheAcquireComplete(object? sender, CacheAcquireCompleteEventArgs e)
    {
        if (e.Status < 0 && downloadRetries.TryGetValue(e.PackageOrContainerId ?? string.Empty, out var retries) && retries < 3)
        {
            downloadRetries[e.PackageOrContainerId!] = retries + 1;
            switch (e.Status)
            {
                case -2147023294: // ERROR_INSTALL_USEREXIT
                case -2147024894: // ERROR_FILE_NOT_FOUND
                case -2147012889: // ERROR_INTERNET_NAME_NOT_RESOLVED
                    break;
                default:
                    e.Action = BOOTSTRAPPER_CACHEACQUIRECOMPLETE_ACTION.Retry;
                    break;
            }
        }
    }

    private void ExecutePackageBegin(object? sender, ExecutePackageBeginEventArgs e) { }
    private void ExecutePackageComplete(object? sender, ExecutePackageCompleteEventArgs e) { }

    private void ExecuteError(object? sender, ErrorEventArgs e)
    {
        if (!root.Canceled)
        {
            if (InstallationState.Applying == root.InstallState && 1223 == e.ErrorCode)
            {
                root.InstallState = root.PreApplyState;
            }
            else
            {
                Message = e.ErrorMessage;

                if (Display.Full == SakuBA_App.Model?.Command?.Display)
                {
                    if (e.ErrorType is ErrorType.HttpServerAuthentication or ErrorType.HttpProxyAuthentication)
                    {
                        e.Result = Result.TryAgain;
                    }
                    else
                    {
                        var messageBox = MessageBoxButton.OK;
                        switch (e.UIHint & 0xF)
                        {
                            case 0: messageBox = MessageBoxButton.OK; break;
                            case 1: messageBox = MessageBoxButton.OKCancel; break;
                            case 3: messageBox = MessageBoxButton.YesNoCancel; break;
                            case 4: messageBox = MessageBoxButton.YesNo; break;
                        }

                        var result = MessageBoxResult.None;
                        SakuBA_App.View?.Dispatcher.Invoke((Action)(() =>
                                result = MessageBox.Show(SakuBA_App.View, e.ErrorMessage, "Saku Overclock", messageBox, MessageBoxImage.Error)
                            ));

                        if ((e.UIHint & 0xF) == (int)messageBox)
                        {
                            e.Result = (Result)result;
                        }
                    }
                }
            }
        }
        else
        {
            e.Result = Result.Cancel;
        }
    }

    private void ApplyComplete(object? sender, ApplyCompleteEventArgs e)
    {
        SakuBA_App.Model?.Result = e.Status;

        if (root.InstallState != root.PreApplyState)
        {
            root.InstallState = Hresult.Succeeded(e.Status) ? InstallationState.Applied : InstallationState.Failed;
        }

        if (Display.Full != SakuBA_App.Model?.Command?.Display)
        {
            if (Display.Passive == SakuBA_App.Model?.Command?.Display)
            {
                if (SakuBA_App.View != null)
                    SakuBA_App.Dispatcher?.BeginInvoke(new Action(SakuBA_App.View.Close));
            }
            else
            {
                SakuBA_App.Dispatcher?.InvokeShutdown();
            }
            return;
        }

        if (Hresult.Succeeded(e.Status) && LaunchAction.UpdateReplace == SakuBA_App.Model.PlannedAction || root.AutoClose)
        {
            if (SakuBA_App.View != null)
                SakuBA_App.Dispatcher?.BeginInvoke(new Action(SakuBA_App.View.Close));
            return;
        }

        root.Dispatcher?.Invoke(CommandManager.InvalidateRequerySuggested);
    }
}