namespace SakuBA
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Security.Cryptography.X509Certificates;
    using System.Windows;
    using System.Windows.Input;
    using System.Windows.Threading;
    using WixToolset.BootstrapperApplicationApi;

    public enum Error
    {
        UserCancelled = 1223,
    }

    public enum DetectionState
    {
        Absent,
        Present,
    }

    public enum UpgradeDetectionState
    {
        None,
        Older,
        Newer,
    }

    public enum InstallationState
    {
        Initializing,
        Detecting,
        Waiting,
        Planning,
        Applying,
        Applied,
        Failed,
    }

    public enum InstallerScenario
    {
        FreshInstall,
        AlreadyInstalled,
        Uninstall,
        Success
    }

    public class RootViewModel : PropertyNotifyBase
    {
        // Хэндл сертификата для сверки источника (замени на свой рабочий отпечаток без пробелов)
        private const string ExpectedThumbprint = "71D4DBA8A8A89EE9EB0000000000000000000000"; 
        private const string TargetExeName = "Saku Overclock.exe";

        private ICommand? cancelCommand;
        private ICommand? closeCommand;
        private ICommand? primaryActionCommand;
        private ICommand? secondaryActionCommand;
        private ICommand? launchGitHubCommand;

        private bool canceled;
        private bool isEnglish;
        private InstallationState installState;
        private DetectionState detectState;
        private UpgradeDetectionState upgradeDetectState;

        public RootViewModel()
        {
            // Авто-определение языка системы (если не EN, то по дефолту RU)
            isEnglish = !CultureInfo.InstalledUICulture.Name.StartsWith("ru", StringComparison.OrdinalIgnoreCase);
            
            _uninstalling = SakuBA_App.Model?.Command?.Action == LaunchAction.Uninstall;
            // Если у тебя в прогрессе или апдейте осталась тяжелая логика — 
            // передаем им ссылку на этот же корневой контекст
            ProgressViewModel = new ProgressViewModel(this); 
            UpdateViewModel = new UpdateViewModel(this);
            InstallationViewModel = new InstallationViewModel(this);
        }

        #region Sub-ViewModels & System Properties
        public ProgressViewModel ProgressViewModel { get; private set; }
        public UpdateViewModel UpdateViewModel { get; private set; }
        public InstallationViewModel InstallationViewModel { get; private set; }
        public Dispatcher? Dispatcher { get; set; }
        public IntPtr ViewWindowHandle { get; set; }
        public bool AutoClose { get; set; }
        public InstallationState PreApplyState { get; set; }
        public string Title => "Saku Overclock";

        private readonly bool _uninstalling;
        #endregion

        #region Localization & Scenario Core Logic
        public bool IsEnglish
        {
            get => isEnglish;
            set
            {
                if (isEnglish != value)
                {
                    isEnglish = value;
                    NotifyAllTextProperties();
                }
            }
        }

        public InstallerScenario CurrentScenario
        {
            get
            {
                if (InstallState == InstallationState.Applied)
                    return InstallerScenario.Success;

                // Если запуск из "Установки и удаления программ" с флагом удаления
                if (SakuBA_App.Model?.Command?.Action == LaunchAction.Uninstall)
                    return InstallerScenario.Uninstall;
                
                if (DetectState == DetectionState.Present)
                    return InstallerScenario.AlreadyInstalled;

                return InstallerScenario.FreshInstall;
            }
        }

        public bool IsProgressState => InstallState == InstallationState.Planning || InstallState == InstallationState.Applying;
        public bool IsNotProgressState => !IsProgressState;
        public bool CancelAvailable => InstallState == InstallationState.Applying;
        #endregion

        #region Dynamic Data (Version & Digital Signature)
        public string DisplayVersion
        {
            get
            {
                // Ищем собранный бинарь в директории публикации / запуска
                string targetPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, TargetExeName);
                if (File.Exists(targetPath))
                {
                    var versionInfo = FileVersionInfo.GetVersionInfo(targetPath);
                    if (!string.IsNullOrEmpty(versionInfo.FileVersion))
                    {
                        return versionInfo.FileVersion;
                    }
                }
                return "1.0.0.0"; 
            }
        }

        public string DisplaySource
        {
            get
            {
                string targetPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, TargetExeName);
                if (File.Exists(targetPath))
                {
                    try
                    {
                        using var cert = X509Certificate2.CreateFromSignedFile(targetPath);
                        if (cert.GetCertHashString().Equals(ExpectedThumbprint, StringComparison.OrdinalIgnoreCase))
                        {
                            return "github.com";
                        }
                    }
                    catch
                    {
                        // Файл не подписан или ошибка чтения структуры
                    }
                }
                return IsEnglish ? "file" : "файл";
            }
        }
        #endregion

        #region Localized UI Strings
        public string LocalizedStatusTitle => CurrentScenario switch
        {
            InstallerScenario.FreshInstall => IsEnglish ? "Install Saku Overclock?" : "Установить Saku Overclock?",
            InstallerScenario.AlreadyInstalled => IsEnglish ? "Saku Overclock is already installed" : "Saku Overclock уже установлено",
            InstallerScenario.Uninstall => IsEnglish ? "Uninstall Saku Overclock?" : "Удалить Saku Overclock?",
            InstallerScenario.Success => IsEnglish ? (_uninstalling ? "Saku Overclock successfully uninstalled" : "Installation Completed Successfully!") 
            : (_uninstalling ? "Saku Overclock успешно удалено" : "Установка успешно завершена!"),
            _ => string.Empty
        };

        public string LocalizedPrimaryActionText => CurrentScenario switch
        {
            InstallerScenario.FreshInstall => IsEnglish ? "Install" : "Установить",
            InstallerScenario.AlreadyInstalled => IsEnglish ? "Reinstall" : "Переустановить",
            InstallerScenario.Uninstall => IsEnglish ? "Delete" : "Удалить",
            InstallerScenario.Success => IsEnglish ? "Launch" : "Запустить",
            _ => string.Empty
        };

        public string LocalizedSecondaryActionText => CurrentScenario switch
        {
            InstallerScenario.FreshInstall => IsEnglish ? "Cancel" : "Отмена",
            InstallerScenario.AlreadyInstalled => IsEnglish ? "Launch" : "Запустить",
            InstallerScenario.Uninstall => IsEnglish ? "Reinstall" : "Переустановить",
            InstallerScenario.Success => IsEnglish ? "Exit" : "Выйти",
            _ => string.Empty
        };

        public string LocalizedPublisherLabel => IsEnglish ? "Publisher: " : "Издатель: ";
        public string LocalizedVersionLabel => IsEnglish ? "Version: " : "Версия: ";
        public string LocalizedSourceLabel => IsEnglish ? "Source: " : "Источник: ";
        public string LocalizedFeaturesLabel => IsEnglish ? "Features:" : "Возможности:";

        public List<string> LocalizedFeaturesList => IsEnglish
            ? new List<string> { "Uses all system resources", "Run as administrator", "Install service on this computer" }
            : new List<string> { "Использует все системные ресурсы", "Запуск от имени администратора", "Установить службу на этом компьютере" };
        
        public Visibility PrimaryButtonVisibility => CurrentScenario == InstallerScenario.Success && _uninstalling == true 
            ? Visibility.Collapsed 
            : Visibility.Visible;


        #endregion

        #region Commands Map
        public ICommand PrimaryActionCommand => primaryActionCommand ??= new RelayCommand(_ =>
        {
            switch (CurrentScenario)
            {
                case InstallerScenario.FreshInstall:
                    // Запуск планирования стандартной установки через движок WiX
                    SakuBA_App.Plan(LaunchAction.Install);
                    break;
                case InstallerScenario.AlreadyInstalled:
                case InstallerScenario.Uninstall:
                    // Сценарий Удаления (Uninstall)
                    SakuBA_App.Plan(LaunchAction.Uninstall);
                    break;
                case InstallerScenario.Success:
                    LaunchTargetApplication();
                    SakuBA_App.View?.Close();
                    break;
            }
        });

        public ICommand SecondaryActionCommand => secondaryActionCommand ??= new RelayCommand(_ =>
        {
            switch (CurrentScenario)
            {
                case InstallerScenario.FreshInstall:
                    CancelButton_Click();
                    break;
                case InstallerScenario.AlreadyInstalled:
                    LaunchTargetApplication();
                    SakuBA_App.View?.Close();
                    break;
                case InstallerScenario.Uninstall:
                    // Сценарий Переустановки (Repair)
                    SakuBA_App.Plan(LaunchAction.Repair);
                    break;
                case InstallerScenario.Success:
                    SakuBA_App.View?.Close();
                    break;
            }
        });

        public ICommand LaunchGitHubCommand => launchGitHubCommand ??= new RelayCommand(_ =>
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/Erruar/Saku-Overclock",
                    UseShellExecute = true
                });
            }
            catch { /* Фоллбэк на случай проблем с дефолтным браузером в системе */ }
        });

        public ICommand CloseCommand => closeCommand ??= new RelayCommand(_ => SakuBA_App.View?.Close());

        public ICommand CancelCommand => cancelCommand ??= new RelayCommand(_ => CancelButton_Click(), _ => !Canceled);
        #endregion

        #region State Properties Window Setters
        public bool Canceled
        {
            get => canceled;
            set
            {
                if (canceled != value)
                {
                    canceled = value;
                    base.OnPropertyChanged(nameof(Canceled));
                }
            }
        }

        public DetectionState DetectState
        {
            get => detectState;
            set
            {
                if (detectState != value)
                {
                    detectState = value;
                    base.OnPropertyChanged(nameof(DetectState));
                    NotifyAllTextProperties();
                }
            }
        }

        public UpgradeDetectionState UpgradeDetectState
        {
            get => upgradeDetectState;
            set
            {
                if (upgradeDetectState != value)
                {
                    upgradeDetectState = value;
                    base.OnPropertyChanged(nameof(UpgradeDetectState));
                }
            }
        }

        public InstallationState InstallState
        {
            get => installState;
            set
            {
                if (installState != value)
                {
                    installState = value;
                    base.OnPropertyChanged(nameof(InstallState));
                    base.OnPropertyChanged(nameof(CancelAvailable));
                    base.OnPropertyChanged(nameof(IsProgressState));
                    base.OnPropertyChanged(nameof(IsNotProgressState));
                    NotifyAllTextProperties();
                }
            }
        }

        public string InstallDirectory
        {
            get => SakuBA_App.Model?.InstallDirectory ?? string.Empty;
            set
            {
                if (SakuBA_App.Model?.InstallDirectory != value)
                {
                    SakuBA_App.Model?.InstallDirectory = value;
                    base.OnPropertyChanged(nameof(InstallDirectory));
                }
            }
        }
        #endregion

        #region Helper Methods
        public void CancelButton_Click()
        {
            if (Canceled) return;

            if (Display.Full == SakuBA_App.Model?.Command?.Display)
            {
                string message = IsEnglish ? "Are you sure you want to cancel?" : "Вы уверены, что хотите отменить установку?";
                string caption = Title;

                var result = MessageBox.Show(SakuBA_App.View!, message, caption, MessageBoxButton.YesNo, MessageBoxImage.Warning);
                Canceled = (result == MessageBoxResult.Yes);
            }
            else
            {
                Canceled = true;
            }

            if (Canceled && InstallState != InstallationState.Applying)
            {
                SakuBA_App.View?.Close();
            }
        }

        private void LaunchTargetApplication()
        {
            try
            {
                string targetPath = Path.Combine("C:\\Program Files\\Saku Overclock\\", TargetExeName);
                if (!File.Exists(targetPath)) 
                    targetPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, TargetExeName);

                if (File.Exists(targetPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = targetPath,
                        UseShellExecute = true
                    });
                }
            }
            catch { /* Игнорируем если запуск отменили интерсептом */ }
        }

        private void NotifyAllTextProperties()
        {
            base.OnPropertyChanged(nameof(CurrentScenario));
            base.OnPropertyChanged(nameof(LocalizedStatusTitle));
            base.OnPropertyChanged(nameof(LocalizedPrimaryActionText));
            base.OnPropertyChanged(nameof(LocalizedSecondaryActionText));
            base.OnPropertyChanged(nameof(LocalizedPublisherLabel));
            base.OnPropertyChanged(nameof(LocalizedVersionLabel));
            base.OnPropertyChanged(nameof(LocalizedSourceLabel));
            base.OnPropertyChanged(nameof(LocalizedFeaturesLabel));
            base.OnPropertyChanged(nameof(LocalizedFeaturesList));
            base.OnPropertyChanged(nameof(PrimaryButtonVisibility));
            base.OnPropertyChanged(nameof(DisplaySource));
        }
        
        private ICommand? minimizeCommand;
        private ICommand? maximizeCommand;

        public ICommand MinimizeCommand => minimizeCommand ??= new RelayCommand(_ =>
        {
            if (SakuBA_App.View != null)
            {
                SakuBA_App.View.WindowState = WindowState.Minimized;
            }
        });

        public ICommand MaximizeCommand => maximizeCommand ??= new RelayCommand(_ =>
        {
            if (SakuBA_App.View != null)
            {
                SakuBA_App.View.WindowState = SakuBA_App.View.WindowState == WindowState.Maximized 
                    ? WindowState.Normal 
                    : WindowState.Maximized;
            }
        });
        #endregion
    }
}