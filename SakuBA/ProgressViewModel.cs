using System.Collections.Generic;
using System.ComponentModel;
using WixToolset.BootstrapperApplicationApi;

namespace SakuBA;

public class ProgressViewModel : PropertyNotifyBase
{
    private static readonly Dictionary<string, int> ExecutingPackageOrderIndex = new();

    private readonly RootViewModel root;
    private int progressPhases;
    private int progress;
    private int cacheProgress;
    private int executeProgress;
    private string package = string.Empty;
    private string message = string.Empty;

    public ProgressViewModel(RootViewModel root)
    {
        this.root = root;
        this.root.PropertyChanged += RootPropertyChanged;

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

    public bool ProgressEnabled => root.InstallState == InstallationState.Applying;

    public int Progress
    {
        get => progress;
        set
        {
            if (progress != value)
            {
                progress = value;
                base.OnPropertyChanged("Progress");
            }
        }
    }

    public string Package
    {
        get => package;
        set
        {
            if (package != value)
            {
                package = value;
                base.OnPropertyChanged("Package");
            }
        }
    }

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
            e.Cancel = root.Canceled;
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
        progressPhases = e.PhaseCount;
    }

    private void ApplyProgress(object? sender, ProgressEventArgs e)
    {
        lock (this)
        {
            e.Cancel = root.Canceled;
        }
    }

    private void CacheAcquireProgress(object? sender, CacheAcquireProgressEventArgs e)
    {
        lock (this)
        {
            cacheProgress = e.OverallPercentage;
            Progress = (cacheProgress + executeProgress) / progressPhases;
            e.Cancel = root.Canceled;
        }
    }

    private void CacheContainerOrPayloadVerifyProgress(object? sender, CacheContainerOrPayloadVerifyProgressEventArgs e)
    {
        lock (this)
        {
            cacheProgress = e.OverallPercentage;
            Progress = (cacheProgress + executeProgress) / progressPhases;
            e.Cancel = root.Canceled;
        }
    }

    private void CachePayloadExtractProgress(object? sender, CachePayloadExtractProgressEventArgs e)
    {
        lock (this)
        {
            cacheProgress = e.OverallPercentage;
            Progress = (cacheProgress + executeProgress) / progressPhases;
            e.Cancel = root.Canceled;
        }
    }

    private void CacheVerifyProgress(object? sender, CacheVerifyProgressEventArgs e)
    {
        lock (this)
        {
            cacheProgress = e.OverallPercentage;
            Progress = (cacheProgress + executeProgress) / progressPhases;
            e.Cancel = root.Canceled;
        }
    }

    private void CacheComplete(object? sender, CacheCompleteEventArgs e)
    {
        lock (this)
        {
            cacheProgress = 100;
            Progress = (cacheProgress + executeProgress) / progressPhases;
        }
    }

    private void ApplyExecuteProgress(object? sender, ExecuteProgressEventArgs e)
    {
        lock (this)
        {
            executeProgress = e.OverallPercentage;
            Progress = (cacheProgress + executeProgress) / progressPhases;

            if (SakuBA_App.Model?.Command?.Display == Display.Embedded)
            {
                SakuBA_App.Model.Engine?.SendEmbeddedProgress(e.ProgressPercentage, Progress);
            }

            e.Cancel = root.Canceled;
        }
    }
}