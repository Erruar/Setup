using System.Collections.Generic;
using System.ComponentModel;
using WixToolset.BootstrapperApplicationApi;

namespace SakuBA;

public class ProgressViewModel : PropertyNotifyBase
{
    private static readonly Dictionary<string, int> ExecutingPackageOrderIndex = new();

    private readonly RootViewModel _root;
    private int _progressPhases;
    private int _cacheProgress;
    private int _executeProgress;

    public ProgressViewModel(RootViewModel root)
    {
        _root = root;
        _root.PropertyChanged += RootPropertyChanged;

        SakuBA_App.Model?.Bootstrapper.ExecutePackageBegin += ExecutePackageBegin;
        SakuBA_App.Model?.Bootstrapper.ExecutePackageComplete += ExecutePackageComplete;
        SakuBA_App.Model?.Bootstrapper.ExecuteProgress += ApplyExecuteProgress;
        SakuBA_App.Model?.Bootstrapper.PlanBegin += PlanBegin;
        SakuBA_App.Model?.Bootstrapper.PlannedPackage += PlannedPackage;
        SakuBA_App.Model?.Bootstrapper.ApplyBegin += ApplyBegin;
        SakuBA_App.Model?.Bootstrapper.Progress += ApplyProgress;
        SakuBA_App.Model?.Bootstrapper.CacheAcquireProgress += CacheAcquireProgress;
        SakuBA_App.Model?.Bootstrapper.CacheContainerOrPayloadVerifyProgress += CacheContainerOrPayloadVerifyProgress;
        SakuBA_App.Model?.Bootstrapper.CachePayloadExtractProgress += CachePayloadExtractProgress;
        SakuBA_App.Model?.Bootstrapper.CacheVerifyProgress += CacheVerifyProgress;
        SakuBA_App.Model?.Bootstrapper.CacheComplete += CacheComplete;
    }

    public bool ProgressEnabled => _root.InstallState == InstallationState.Applying;

    public int Progress
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                base.OnPropertyChanged("Progress");
            }
        }
    }

    public string Package
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                base.OnPropertyChanged("Package");
            }
        }
    } = string.Empty;

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

    void RootPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "InstallState")
        {
            base.OnPropertyChanged("ProgressEnabled");
        }
    }

    private void PlanBegin(object? sender, PlanBeginEventArgs e)
    {
        lock (ExecutingPackageOrderIndex)
        {
            ExecutingPackageOrderIndex.Clear();
        }
    }

    private void PlannedPackage(object? sender, PlannedPackageEventArgs e)
    {
        if (ActionState.None != e.Execute)
        {
            lock (ExecutingPackageOrderIndex)
            {
                ExecutingPackageOrderIndex.Add(e.PackageId, ExecutingPackageOrderIndex.Count);
            }
        }
    }

    private void ExecutePackageBegin(object? sender, ExecutePackageBeginEventArgs e)
    {
        lock (this)
        {
            Package = SakuBA_App.Model?.GetPackageName(e.PackageId) ?? string.Empty;
            Message = $"Processing: {Package}";
            e.Cancel = _root.Canceled;
        }
    }

    private void ExecutePackageComplete(object? sender, ExecutePackageCompleteEventArgs e)
    {
        lock (this)
        {
            Message = string.Empty;
        }
    }

    private void ApplyBegin(object? sender, ApplyBeginEventArgs e)
    {
        _progressPhases = e.PhaseCount;
    }

    private void ApplyProgress(object? sender, ProgressEventArgs e)
    {
        lock (this)
        {
            e.Cancel = _root.Canceled;
        }
    }

    private void CacheAcquireProgress(object? sender, CacheAcquireProgressEventArgs e)
    {
        lock (this)
        {
            _cacheProgress = e.OverallPercentage;
            Progress = (_cacheProgress + _executeProgress) / _progressPhases;
            e.Cancel = _root.Canceled;
        }
    }

    private void CacheContainerOrPayloadVerifyProgress(object? sender, CacheContainerOrPayloadVerifyProgressEventArgs e)
    {
        lock (this)
        {
            _cacheProgress = e.OverallPercentage;
            Progress = (_cacheProgress + _executeProgress) / _progressPhases;
            e.Cancel = _root.Canceled;
        }
    }

    private void CachePayloadExtractProgress(object? sender, CachePayloadExtractProgressEventArgs e)
    {
        lock (this)
        {
            _cacheProgress = e.OverallPercentage;
            Progress = (_cacheProgress + _executeProgress) / _progressPhases;
            e.Cancel = _root.Canceled;
        }
    }

    private void CacheVerifyProgress(object? sender, CacheVerifyProgressEventArgs e)
    {
        lock (this)
        {
            _cacheProgress = e.OverallPercentage;
            Progress = (_cacheProgress + _executeProgress) / _progressPhases;
            e.Cancel = _root.Canceled;
        }
    }

    private void CacheComplete(object? sender, CacheCompleteEventArgs e)
    {
        lock (this)
        {
            _cacheProgress = 100;
            Progress = (_cacheProgress + _executeProgress) / _progressPhases;
        }
    }

    private void ApplyExecuteProgress(object? sender, ExecuteProgressEventArgs e)
    {
        lock (this)
        {
            _executeProgress = e.OverallPercentage;
            Progress = (_cacheProgress + _executeProgress) / _progressPhases;

            if (SakuBA_App.Model?.Command?.Display == Display.Embedded)
            {
                SakuBA_App.Model.Engine?.SendEmbeddedProgress(e.ProgressPercentage, Progress);
            }

            e.Cancel = _root.Canceled;
        }
    }
}