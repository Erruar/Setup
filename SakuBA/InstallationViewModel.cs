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
    private readonly RootViewModel _root;
    private readonly Dictionary<string, int> _downloadRetries;

    public InstallationViewModel(RootViewModel root)
    {
        _root = root;
        _downloadRetries = new Dictionary<string, int>();

        _root.PropertyChanged += new PropertyChangedEventHandler(RootPropertyChanged);

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
        get;
        set
        {
            if (field != value)
            {
                field = value;
                base.OnPropertyChanged("Message");
            }
        }
    } = string.Empty;

    public bool Downgrade
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                base.OnPropertyChanged("Downgrade");
            }
        }
    }

    public string DowngradeMessage
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                base.OnPropertyChanged("DowngradeMessage");
            }
        }
    } = string.Empty;

    public ICommand LaunchGitHubCommand
    {
        get
        {
            field ??= new RelayCommand(_ => SakuBA_App.LaunchUrl(GitHubUrl), _ => true);
            return field;
        }
    }

    public ICommand CloseCommand => _root.CloseCommand;

    public bool IsComplete => IsSuccessfulCompletion || IsFailedCompletion;
    public bool IsSuccessfulCompletion => _root.InstallState == InstallationState.Applied;
    public bool IsFailedCompletion => _root.InstallState == InstallationState.Failed;

    public ICommand InstallCommand
    {
        get
        {
            field ??= new RelayCommand(
                _ => SakuBA_App.Plan(LaunchAction.Install),
                _ => _root.DetectState == DetectionState.Absent && _root.UpgradeDetectState != UpgradeDetectionState.Newer &&
                     _root.InstallState == InstallationState.Waiting);
            return field;
        }
    }

    public bool InstallEnabled => InstallCommand.CanExecute(null);

    public ICommand RepairCommand
    {
        get
        {
            if (field == null)
            {
                field = new RelayCommand(_ => SakuBA_App.Plan(LaunchAction.Repair), _ => _root.DetectState == DetectionState.Present && _root.InstallState == InstallationState.Waiting);
            }
            return field;
        }
    }

    public bool RepairEnabled => RepairCommand.CanExecute(null);

    public ICommand UninstallCommand
    {
        get
        {
            if (field == null)
            {
                field = new RelayCommand(_ => SakuBA_App.Plan(LaunchAction.Uninstall), _ => _root.DetectState == DetectionState.Present && _root.InstallState == InstallationState.Waiting);
            }
            return field;
        }
    }

    public bool UninstallEnabled => UninstallCommand.CanExecute(null);

    public ICommand OpenLogCommand
    {
        get
        {
            if (field == null)
            {
                field = new RelayCommand(_ => SakuBA_App.OpenLog(SakuBA_App.Model?.Engine?.GetVariableString("WixBundleLog") ?? string.Empty));
            }
            return field;
        }
    }

    public ICommand OpenLogFolderCommand
    {
        get
        {
            if (field == null)
            {
                var logFolder = Path.GetDirectoryName(SakuBA_App.Model?.Engine?.GetVariableString("WixBundleLog") ?? "");
                field = new RelayCommand(_ => SakuBA_App.OpenLog(logFolder ?? ""));
            }
            return field;
        }
    }

    public ICommand TryAgainCommand
    {
        get
        {
            field ??= new RelayCommand(_ =>
            {
                _root.Canceled = false;
                if (SakuBA_App.Model != null)
                    SakuBA_App.Plan(SakuBA_App.Model.PlannedAction);
            }, _ => IsFailedCompletion);
            return field;
        }
    }

    public string StatusText
    {
        get
        {
            switch (_root.InstallState)
            {
                case InstallationState.Applied: return "Complete";
                case InstallationState.Failed: return _root.Canceled ? "Cancelled" : "Failed";
                default: return "Unknown";
            }
        }
    }

    private void DetectBegin(object? sender, DetectBeginEventArgs e)
    {
        _root.DetectState = RegistrationType.Full == e.RegistrationType ? DetectionState.Present : DetectionState.Absent;
        SakuBA_App.Model?.PlannedAction = LaunchAction.Unknown;
    }

    private void DetectedRelatedBundle(object? sender, DetectRelatedBundleEventArgs e)
    {
        if (e.RelationType == RelationType.Upgrade)
        {
            if (SakuBA_App.Model?.Engine?.CompareVersions(Version, e.Version) > 0)
            {
                if (_root.UpgradeDetectState == UpgradeDetectionState.None)
                {
                    _root.UpgradeDetectState = UpgradeDetectionState.Older;
                }
            }
            else
            {
                _root.UpgradeDetectState = UpgradeDetectionState.Newer;
            }
        }

        if (SakuBA_App.Model?.BootstrapManifest?.Bundle.Packages.ContainsKey(e.ProductCode) == false)
        {
            SakuBA_App.Model.BootstrapManifest.Bundle.AddRelatedBundleAsPackage(e.ProductCode, e.RelationType, e.PerMachine, e.Version);
        }
    }

    private void DetectComplete(object? sender, DetectCompleteEventArgs e)
    {
        _root.InstallState = InstallationState.Waiting;

        if (SakuBA_App.Model != null && SakuBA_App.Model.Command != null)
        {
            if (LaunchAction.Uninstall == SakuBA_App.Model.Command.Action && ResumeType.Arp != SakuBA_App.Model.Command.Resume)
            {
                SakuBA_App.Model.Engine?.Log(LogLevel.Verbose, "Invoking automatic plan for uninstall");
                SakuBA_App.Plan(LaunchAction.Uninstall);
            }
            else if (Hresult.Succeeded(e.Status))
            {
                if (_root.UpgradeDetectState == UpgradeDetectionState.Newer)
                {
                    Downgrade = true;
                    DowngradeMessage = "There is already a newer version of Saku Overclock installed on this machine.";
                }

                if (LaunchAction.Layout == SakuBA_App.Model.Command.Action)
                {
                    // Layout handled separately
                }
                else if (SakuBA_App.Model.Command.Display != Display.Full && SakuBA_App.Model.Engine != null)
                {
                    SakuBA_App.Model.Engine.Log(LogLevel.Verbose, "Invoking automatic plan for non-interactive mode.");
                    SakuBA_App.Plan(SakuBA_App.Model.Command.Action);
                }
            }
            else
            {
                _root.InstallState = InstallationState.Failed;
            }
        }
        _root.Dispatcher?.Invoke(CommandManager.InvalidateRequerySuggested);
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
            _root.PreApplyState = _root.InstallState;
            _root.InstallState = InstallationState.Applying;
            SakuBA_App.Model?.Engine?.Apply(_root.ViewWindowHandle);
        }
        else
        {
            _root.InstallState = InstallationState.Failed;
        }
    }

    private void ApplyBegin(object? sender, ApplyBeginEventArgs e)
    {
        _downloadRetries.Clear();
    }

    private void CacheAcquireBegin(object? sender, CacheAcquireBeginEventArgs e) { }

    private void CacheAcquireResolving(object? sender, CacheAcquireResolvingEventArgs e)
    {
        if (e.Action == CacheResolveOperation.Download && !_downloadRetries.ContainsKey(e.PackageOrContainerId ?? string.Empty))
        {
            _downloadRetries.Add(e.PackageOrContainerId!, 0);
        }
    }

    private void CacheAcquireComplete(object? sender, CacheAcquireCompleteEventArgs e)
    {
        if (e.Status < 0 && _downloadRetries.TryGetValue(e.PackageOrContainerId ?? string.Empty, out var retries) && retries < 3)
        {
            _downloadRetries[e.PackageOrContainerId!] = retries + 1;
            switch (e.Status)
            {
                case -2147023294: // ERROR_INSTALL_USER_EXIT
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
        if (!_root.Canceled)
        {
            if (InstallationState.Applying == _root.InstallState && 1223 == e.ErrorCode)
            {
                _root.InstallState = _root.PreApplyState;
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

        if (_root.InstallState != _root.PreApplyState)
        {
            _root.InstallState = Hresult.Succeeded(e.Status) ? InstallationState.Applied : InstallationState.Failed;
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

        if (Hresult.Succeeded(e.Status) && LaunchAction.UpdateReplace == SakuBA_App.Model.PlannedAction || _root.AutoClose)
        {
            if (SakuBA_App.View != null)
                SakuBA_App.Dispatcher?.BeginInvoke(new Action(SakuBA_App.View.Close));
            return;
        }

        _root.Dispatcher?.Invoke(CommandManager.InvalidateRequerySuggested);
    }
}