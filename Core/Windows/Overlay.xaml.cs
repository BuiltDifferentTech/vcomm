namespace VComm.Core.Windows
{
    /// <summary>
    /// Interaction logic for Overlay.xaml.
    /// </summary>
    public partial class Overlay : Window
    {
        internal VoiceEngine voiceEngine = new VoiceEngine();
        private int speechVersion;

        public int Volume
        {
            set
            {
                if (Dispatcher.CheckAccess())
                    VolumeBar.SetPercent(value);
                else
                    Dispatcher.Invoke(() => VolumeBar.SetPercent(value));
            }
        }

        public async Task SetSpeechText(string text, bool hypothetical = false)
        {
            int currentVersion = Interlocked.Increment(ref speechVersion);
            await Dispatcher.InvokeAsync(() =>
            {
                RecognizedText.Foreground = hypothetical
                    ? new SolidColorBrush(System.Windows.Media.Color.FromArgb(100, 255, 175, 0))
                    : new SolidColorBrush(System.Windows.Media.Color.FromArgb(100, 6, 176, 37));
                RecognizedText.Text = text;
            });

            _ = ClearSpeechTextAfterDelay(currentVersion);
        }

        private async Task ClearSpeechTextAfterDelay(int version)
        {
            try
            {
                await Task.Delay(3000);
                if (version == Volatile.Read(ref speechVersion))
                    await Dispatcher.InvokeAsync(() => RecognizedText.Text = string.Empty);
            }
            catch (TaskCanceledException)
            {
                // The application is shutting down.
            }
        }

        public Overlay()
        {
            InitializeComponent();
            ApplySettings();
        }

        public void ApplySettings()
        {
            OverlayGrid.Visibility = Variables.OverlayVisable ? Visibility.Visible : Visibility.Hidden;
            OverlayGrid.Opacity = Variables.OverlayOpacity;
            if (!Variables.ShowRecognizedText)
                RecognizedText.Text = string.Empty;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Maximized;

            try
            {
                await voiceEngine.StartEngine();
                if (Variables.FirstRun)
                    ShowSettings();
            }
            catch (Exception ex)
            {
                await this.Log($"Voice engine startup failed: {ex.Message}", error: true);
                System.Windows.MessageBox.Show(
                    $"VComm could not start the voice engine.\n\n{ex.Message}",
                    "VComm startup error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        public ICommand ShowSettingsCommand => new DelegateCommand
        {
            CommandAction = ShowSettings
        };

        private void OpenSettings(object sender, RoutedEventArgs e)
        {
            ShowSettings();
        }

        private static void ShowSettings()
        {
            Settings? existing = System.Windows.Application.Current.Windows
                .OfType<Settings>()
                .FirstOrDefault();

            if (existing is not null)
            {
                existing.Activate();
                return;
            }

            new Settings().Show();
        }

        private async void ExitApp(object sender, RoutedEventArgs e)
        {
            await voiceEngine.StopEngine();
            System.Windows.Application.Current.Shutdown();
        }
    }
}
