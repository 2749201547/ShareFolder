using Stylet;
using SyncTrayzor.Pages.BarAlerts;
using SyncTrayzor.Pages.ConflictResolution;
using SyncTrayzor.Pages.Settings;
using SyncTrayzor.Properties;
using SyncTrayzor.Services;
using SyncTrayzor.Services.Config;
using SyncTrayzor.Syncthing;
using SyncTrayzor.Utils;
using System;
using System.Reactive.Subjects;
using System.Windows;

namespace SyncTrayzor.Pages
{
    public class ShellViewModel : Screen, IDisposable
    {
        private readonly IWindowManager windowManager;
        private readonly ISyncthingManager syncthingManager;
        private readonly IApplicationState application;
        private readonly IConfigurationProvider configurationProvider;
        private readonly Func<SettingsViewModel> settingsViewModelFactory;
        private readonly Func<AboutViewModel> aboutViewModelFactory;
        private readonly Func<ConflictResolutionViewModel> confictResolutionViewModelFactory;
        private readonly IProcessStartProvider processStartProvider;

        public bool ShowConsole { get; set; }
        public bool ShowThisDevice { get; set; }
        public double ConsoleHeight { get; set; }
        public WindowPlacement Placement { get; set; }
        public IDonationManager DonationManager { get; }

        private readonly Subject<bool> _activateObservable = new();
        public IObservable<bool> ActivateObservable => _activateObservable;
        public ConsoleViewModel Console { get; }
        public ViewerViewModel Viewer { get; }
        public BarAlertsViewModel BarAlerts { get; }

        public SyncthingState SyncthingState { get; private set; }

        public ShellViewModel(
            IWindowManager windowManager,
            ISyncthingManager syncthingManager,
            IApplicationState application,
            IConfigurationProvider configurationProvider,
            ConsoleViewModel console,
            ViewerViewModel viewer,
            BarAlertsViewModel barAlerts,
            Func<SettingsViewModel> settingsViewModelFactory,
            Func<AboutViewModel> aboutViewModelFactory,
            Func<ConflictResolutionViewModel> confictResolutionViewModelFactory,
            IProcessStartProvider processStartProvider,
            IDonationManager donationManager)
        {
            this.windowManager = windowManager;
            this.syncthingManager = syncthingManager;
            this.application = application;
            this.configurationProvider = configurationProvider;
            Console = console;
            Viewer = viewer;
            BarAlerts = barAlerts;
            this.settingsViewModelFactory = settingsViewModelFactory;
            this.aboutViewModelFactory = aboutViewModelFactory;
            this.confictResolutionViewModelFactory = confictResolutionViewModelFactory;
            this.processStartProvider = processStartProvider;
            DonationManager = donationManager;

            var configuration = this.configurationProvider.Load();

            Console.ConductWith(this);
            Viewer.ConductWith(this);
            BarAlerts.ConductWith(this);

            this.syncthingManager.StateChanged += (o, e) => SyncthingState = e.NewState;
            this.syncthingManager.ProcessExitedWithError += (o, e) => ShowExitedWithError();

            ConsoleHeight = configuration.SyncthingConsoleHeight;
            this.Bind(s => s.ConsoleHeight, (o, e) => this.configurationProvider.AtomicLoadAndSave(c => c.SyncthingConsoleHeight = e.NewValue));

            ShowConsole = configuration.SyncthingConsoleHeight > 0;
            this.Bind(s => s.ShowConsole, (o, e) =>
            {
                ConsoleHeight = e.NewValue ? Configuration.DefaultSyncthingConsoleHeight : 0.0;
            });

            ShowThisDevice = !configuration.HideThisDevice;
            Viewer.ApplyShowThisDevice(ShowThisDevice);
            this.Bind(s => s.ShowThisDevice, (o, e) =>
            {
                configurationProvider.AtomicLoadAndSave(c => c.HideThisDevice = !e.NewValue);
                Viewer.ApplyShowThisDevice(e.NewValue);
            });

            Placement = configuration.WindowPlacement;
            this.Bind(s => s.Placement, (o, e) => this.configurationProvider.AtomicLoadAndSave(c => c.WindowPlacement = e.NewValue));
        }

        public bool CanStart => SyncthingState == SyncthingState.Stopped;
        public async void Start()
        {
            await syncthingManager.StartWithErrorDialogAsync(windowManager);
        }

        public bool CanStop => SyncthingState == SyncthingState.Running;
        public async void Stop()
        {
            await syncthingManager.StopAsync();
        }

        public bool CanRestart => SyncthingState == SyncthingState.Running;
        public async void Restart()
        {
            await syncthingManager.RestartAsync();
        }

        public bool CanRefreshBrowser => SyncthingState == SyncthingState.Running;
        public void RefreshBrowser()
        {
            Viewer.RefreshBrowserNukeCache();
        }

        public bool CanOpenBrowser => SyncthingState == SyncthingState.Running;
        public void OpenBrowser()
        {
            processStartProvider.StartDetached(syncthingManager.Address.NormalizeZeroHost().ToString());
        }

        public void KillAllSyncthingProcesses()
        {
            if (windowManager.ShowMessageBox(
                    Resources.Dialog_ConfirmKillAllProcesses_Message,
                    Resources.Dialog_ConfirmKillAllProcesses_Title,
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                syncthingManager.KillAllSyncthingProcesses();
        }

        public void ShowSettings()
        {
            var vm = settingsViewModelFactory();
            windowManager.ShowDialog(vm);
        }

        public void ShowConflictResolver()
        {
            var vm = confictResolutionViewModelFactory();
            windowManager.ShowDialog(vm);
        }

        public bool CanZoomBrowser => SyncthingState == SyncthingState.Running;

        public void BrowserZoomIn()
        {
            Viewer.ZoomIn();
        }

        public void BrowserZoomOut()
        {
            Viewer.ZoomOut();
        }

        public void BrowserZoomReset()
        {
            Viewer.ZoomReset();
        }

        public void ShowAbout()
        {
            var vm = aboutViewModelFactory();
            windowManager.ShowDialog(vm);
        }

        public void ShowExitedWithError()
        {
            windowManager.ShowMessageBox(
                Resources.Dialog_FailedToStartSyncthing_Message,
                Resources.Dialog_FailedToStartSyncthing_Title,
                icon: MessageBoxImage.Error);
        }

        public void CloseToTray()
        {
            RequestClose();
        }

        public void Shutdown()
        {
            application.Shutdown();
        }

        public void EnsureInForeground()
        {
            if (!application.HasMainWindow)
                windowManager.ShowWindow(this);

            _activateObservable.OnNext(true);
        }

        public void Dispose()
        {
            Viewer.Dispose();
            Console.Dispose();
        }
    }
}
