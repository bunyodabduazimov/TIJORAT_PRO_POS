using System.Windows;
using System.Text.Json;
using FFPOS.Models;
using FFPOS.Services;
using FFPOS.Views;

namespace FFPOS
{
    public partial class MainWindow : Window
    {
        private System.Windows.Threading.DispatcherTimer? _syncTimer;
        private bool _isAutoSyncRunning;
        private readonly AppActivationSettings _settings;

        public MainWindow()
            : this(new AppSettingsService().Load())
        {
        }

        public MainWindow(AppActivationSettings settings)
        {
            _settings = settings;
            InitializeComponent();
            MainContent.Content = new SalesView();
            StartAutoSync();
        }

        private void StartAutoSync()
        {
            var userSettings = ParseCurrentUserSettings();
            if (!userSettings.AutoSync)
            {
                return;
            }

            _syncTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(Math.Clamp(userSettings.SyncIntervalMinutes, 5, 1440))
            };
            _syncTimer.Tick += async (_, _) => await SyncPendingSalesSilentlyAsync();
            _syncTimer.Start();
        }

        private async Task SyncPendingSalesSilentlyAsync()
        {
            if (_isAutoSyncRunning)
            {
                return;
            }

            _isAutoSyncRunning = true;
            try
            {
                await new SyncService(_settings).SyncPendingSalesAsync();
            }
            catch
            {
            }
            finally
            {
                _isAutoSyncRunning = false;
            }
        }

        private static UserSettings ParseCurrentUserSettings()
        {
            var json = App.CurrentUser?.Settings;
            if (string.IsNullOrWhiteSpace(json))
            {
                return new UserSettings();
            }

            try
            {
                return JsonSerializer.Deserialize<UserSettings>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new UserSettings();
            }
            catch
            {
                return new UserSettings();
            }
        }
    }
}
