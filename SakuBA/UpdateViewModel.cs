using System;
using System.ComponentModel;
using System.Windows.Input;
using WixToolset.BootstrapperApplicationApi;

namespace SakuBA;

public enum UpdateState
{
    Unknown,
    Initializing,
    Checking,
    Current,
    Available,
    Failed,
}

public class UpdateViewModel : PropertyNotifyBase
{
    private readonly RootViewModel _root;

    public UpdateViewModel(RootViewModel root)
    {
        this._root = root;
        SakuBA_App.Model?.Bootstrapper.DetectUpdateBegin += DetectUpdateBegin;
        SakuBA_App.Model?.Bootstrapper.DetectUpdate += DetectUpdate;
        SakuBA_App.Model?.Bootstrapper.DetectUpdateComplete += DetectUpdateComplete;
        SakuBA_App.Model?.Bootstrapper.DetectComplete += DetectComplete;
        this._root.PropertyChanged += new PropertyChangedEventHandler(RootPropertyChanged);
        State = UpdateState.Initializing;
    }

    void RootPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "InstallState")
        {
            base.OnPropertyChanged("CanUpdate");
        }
    }

    public bool CheckingEnabled => State is UpdateState.Initializing or UpdateState.Checking;

    public bool CanUpdate
    {
        get
        {
            switch (_root.InstallState)
            {
                case InstallationState.Waiting:
                case InstallationState.Applied:
                case InstallationState.Failed:
                    return IsUpdateAvailable;
                default:
                    return false;
            }
        }
    }

    public ICommand UpdateCommand
    {
        get
        {
            field ??= new RelayCommand(_ => SakuBA_App.Plan(LaunchAction.UpdateReplace), _ => CanUpdate);
            return field;
        }
    }

    public bool IsUpdateAvailable => State == UpdateState.Available;

    public UpdateState State
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                base.OnPropertyChanged("State");
                base.OnPropertyChanged("CanUpdate");
                base.OnPropertyChanged("CheckingEnabled");
                base.OnPropertyChanged("IsUpdateAvailable");
            }
        }
    }

    public string UpdateVersion
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                base.OnPropertyChanged("UpdateVersion");
            }
        }
    } = string.Empty;

    public string UpdateChanges
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                base.OnPropertyChanged("UpdateChanges");
            }
        }
    } = string.Empty;

    private void DetectUpdateBegin(object? sender, DetectUpdateBeginEventArgs e)
    {
        if (UpdateState.Failed != State && LaunchAction.Uninstall != SakuBA_App.Model?.Command?.Action && Display.Full == SakuBA_App.Model?.Command?.Display)
        {
            State = UpdateState.Checking;
            e.Skip = false;
        }
    }

    private void DetectUpdate(object? sender, DetectUpdateEventArgs e)
    {
        if (SakuBA_App.Model?.Engine?.CompareVersions(e.Version, SakuBA_App.Model.Version) > 0)
        {
            var updatePackageId = Guid.NewGuid().ToString("N");
            SakuBA_App.Model.Engine.SetUpdate(null, e.UpdateLocation, e.Size, UpdateHashType.None, null, updatePackageId);

            if (SakuBA_App.Model.BootstrapManifest?.Bundle.Packages.ContainsKey(updatePackageId) == false)
            {
                SakuBA_App.Model.BootstrapManifest?.Bundle.AddUpdateBundleAsPackage(updatePackageId);
            }

            UpdateVersion = "v" + e.Version;
            UpdateChanges = $"<body style='overflow: auto;'>{e.Content}</body>";
            State = UpdateState.Available;
        }
        else
        {
            State = UpdateState.Current;
        }
        e.StopProcessingUpdates = true;
    }

    private void DetectUpdateComplete(object? sender, DetectUpdateCompleteEventArgs e)
    {
        if (UpdateState.Failed != State && !Hresult.Succeeded(e.Status))
        {
            State = UpdateState.Failed;
            e.IgnoreError = true;
        }
        else if (LaunchAction.Uninstall == SakuBA_App.Model?.Command?.Action || UpdateState.Initializing == State || UpdateState.Checking == State)
        {
            State = UpdateState.Unknown;
        }
    }

    private void DetectComplete(object? sender, DetectCompleteEventArgs e)
    {
        if (State is UpdateState.Initializing or UpdateState.Checking)
        {
            State = UpdateState.Unknown;
        }
    }
}