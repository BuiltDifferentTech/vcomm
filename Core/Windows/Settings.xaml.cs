namespace VComm.Core.Windows
{
    /// <summary>
    /// Interaction logic for Settings.xaml
    /// </summary>
    public partial class Settings : Window
    {
        private bool isInitializing = true;
        private bool isCapturingPTTInput;

        public Settings()
        {
            InitializeComponent();
            isInitializing = true;
            try
            {
                SetupSettings();
            }
            finally
            {
                isInitializing = false;
            }

            UpdateResponsiveLayout(ActualWidth);

            if (Variables.FirstRun) ShowIntro();
        }

        private void ShowIntro()
        {
            FirstRun.Visibility = Visibility.Visible;
        }

        private void IntroDoneBtn_Click(object sender, RoutedEventArgs e)
        {
            Variables.FirstRun = false;
            FirstRun.Visibility = Visibility.Collapsed;
        }


        private void SetupSettings()
        {
            BuildInfo.Text = $"VERSION: {Variables.Version} (EARLY-ACCESS/TEST BUILD)";

            if (Data.isPTTActive())
            {
                PTTCheck.IsChecked = true;
                PTTKey.IsEnabled = true;
            }
            else PTTKey.IsEnabled = false;
            PTTKey.Content = GetPTTInputDisplayName();

            VPackList.ItemsSource = Variables.VPacks;
            VPackList.SelectedItem = Variables.ActiveVPack;

            OverlayCheck.IsChecked = Variables.OverlayVisable;
            ChimeCheck.IsChecked = Variables.UseChime;
            UpdateChimePathText();
            RecognitionTextCheck.IsChecked = Variables.ShowRecognizedText;
            ConfidenceSlider.Value = Variables.RecognitionConfidence * 100;
            PressDurationSlider.Value = Variables.KeyPressDurationMs;
            OpacitySlider.Value = Variables.OverlayOpacity * 100;
            UpdateValueLabels();
        }

        private async void Window_Closed(object sender, EventArgs e)
        {
            try
            {
                await Variables.Overlay.voiceEngine.ReloadEngine();
                Variables.Overlay.ApplySettings();
            }
            catch (Exception ex)
            {
                await this.Log($"Failed to apply settings: {ex.Message}", error: true);
                System.Windows.MessageBox.Show(ex.Message, "VComm settings error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PTTCheck_Checked(object sender, RoutedEventArgs e)
        {
            if (isInitializing) return;
            Data.StoreConfig("PTTActive", "true");
            PTTKey.IsEnabled = true;
        }

        private void PTTCheck_Unchecked(object sender, RoutedEventArgs e)
        {
            if (isInitializing) return;
            Data.StoreConfig("PTTActive", "false");
            PTTKey.IsEnabled = false;
        }

        private void PTTKey_Click(object sender, RoutedEventArgs e)
        {
            PTTKey.Content = "PRESS ANY KEY";
            PTTKey.Focus();
            isCapturingPTTInput = true;
        }

        private void PTTKey_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (!isCapturingPTTInput) return;

            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            int virtualKey = KeyInterop.VirtualKeyFromKey(key);
            if (virtualKey <= 0) return;

            Data.StorePTTKeyboardKey(virtualKey);
            PTTKey.Content = FormatKeyName(key);
            isCapturingPTTInput = false;
            e.Handled = true;
        }

        private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!isCapturingPTTInput) return;

            System.Windows.Forms.MouseButtons button = e.ChangedButton switch
            {
                System.Windows.Input.MouseButton.Left => System.Windows.Forms.MouseButtons.Left,
                System.Windows.Input.MouseButton.Right => System.Windows.Forms.MouseButtons.Right,
                System.Windows.Input.MouseButton.Middle => System.Windows.Forms.MouseButtons.Middle,
                System.Windows.Input.MouseButton.XButton1 => System.Windows.Forms.MouseButtons.XButton1,
                System.Windows.Input.MouseButton.XButton2 => System.Windows.Forms.MouseButtons.XButton2,
                _ => System.Windows.Forms.MouseButtons.None
            };

            if (button == System.Windows.Forms.MouseButtons.None) return;
            Data.StorePTTMouseButton(button);
            PTTKey.Content = FormatMouseButton(button);
            isCapturingPTTInput = false;
            e.Handled = true;
        }

        private void VPackList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (isInitializing || VPackList.SelectedItem is not VPack selectedVPack) return;

            Variables.ActiveVPack = selectedVPack;
            Data.StoreConfig("ActiveVPack", selectedVPack.name);
        }

        private void NewVPack_Click(object sender, RoutedEventArgs e) => OpenVPackBuilder(null);

        private void EditVPack_Click(object sender, RoutedEventArgs e) => OpenVPackBuilder(VPackList.SelectedItem as VPack);

        private void OpenVPackBuilder(VPack? pack)
        {
            CreateVPack builder = new CreateVPack(pack) { Owner = this };
            if (builder.ShowDialog() != true || builder.SavedVPack is null) return;

            VPack savedPack = builder.SavedVPack;
            VPack? existing = Variables.VPacks.FirstOrDefault(candidate =>
                string.Equals(candidate.name, savedPack.name, StringComparison.OrdinalIgnoreCase));
            if (existing is not null) Variables.VPacks.Remove(existing);
            Variables.VPacks.Add(savedPack);
            Variables.VPacks.Sort((left, right) => string.Compare(left.name, right.name, StringComparison.OrdinalIgnoreCase));
            VPackList.ItemsSource = null;
            VPackList.ItemsSource = Variables.VPacks;
            VPackList.SelectedItem = savedPack;
            Variables.ActiveVPack = savedPack;
            Data.StoreConfig("ActiveVPack", savedPack.name);
        }

        private void OverlayCheck_Checked(object sender, RoutedEventArgs e)
        {
            if (isInitializing) return;
            Variables.OverlayVisable = true;
            Data.StoreConfig("OverlayActive", "true");
            Variables.Overlay.ApplySettings();
        }

        private void OverlayCheck_Unchecked(object sender, RoutedEventArgs e)
        {
            if (isInitializing) return;
            Variables.OverlayVisable = false;
            Data.StoreConfig("OverlayActive", "false");
            Variables.Overlay.ApplySettings();
        }

        private void ChimeCheck_Checked(object sender, RoutedEventArgs e)
        {
            if (isInitializing) return;
            Variables.UseChime = true;
            Data.StoreConfig("UseChime", "true");
        }

        private void ChimeCheck_Unchecked(object sender, RoutedEventArgs e)
        {
            if (isInitializing) return;
            Variables.UseChime = false;
            Data.StoreConfig("UseChime", "false");
        }

        private void ChooseChime_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Choose a VComm chime",
                Filter = "Wave audio (*.wav)|*.wav",
                CheckFileExists = true,
                Multiselect = false
            };

            if (dialog.ShowDialog(this) != true) return;
            Variables.CustomChimePath = dialog.FileName;
            Data.StoreConfig("CustomChimePath", dialog.FileName);
            UpdateChimePathText();
        }

        private void ResetChime_Click(object sender, RoutedEventArgs e)
        {
            Variables.CustomChimePath = string.Empty;
            Data.StoreConfig("CustomChimePath", string.Empty);
            UpdateChimePathText();
        }

        private void UpdateChimePathText()
        {
            ChimePathText.Text = string.IsNullOrWhiteSpace(Variables.CustomChimePath)
                ? "Default chime"
                : Variables.CustomChimePath;
        }

        private void RecognitionTextCheck_Checked(object sender, RoutedEventArgs e)
        {
            if (isInitializing) return;
            Variables.ShowRecognizedText = true;
            Data.StoreConfig("ShowRecognizedText", "true");
            Variables.Overlay.ApplySettings();
        }

        private void RecognitionTextCheck_Unchecked(object sender, RoutedEventArgs e)
        {
            if (isInitializing) return;
            Variables.ShowRecognizedText = false;
            Data.StoreConfig("ShowRecognizedText", "false");
            Variables.Overlay.ApplySettings();
        }

        private void ConfidenceSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (ConfidenceValue is null) return;
            Variables.RecognitionConfidence = ConfidenceSlider.Value / 100;
            ConfidenceValue.Text = $"{ConfidenceSlider.Value:0}%";
            if (!isInitializing)
                Data.StoreConfig("RecognitionConfidence", Variables.RecognitionConfidence.ToString(CultureInfo.InvariantCulture));
        }

        private void PressDurationSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (PressDurationValue is null) return;
            Variables.KeyPressDurationMs = (int)PressDurationSlider.Value;
            PressDurationValue.Text = $"{Variables.KeyPressDurationMs} ms";
            if (!isInitializing)
                Data.StoreConfig("KeyPressDurationMs", Variables.KeyPressDurationMs.ToString(CultureInfo.InvariantCulture));
        }

        private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (OpacityValue is null) return;
            Variables.OverlayOpacity = OpacitySlider.Value / 100;
            OpacityValue.Text = $"{OpacitySlider.Value:0}%";
            if (!isInitializing)
            {
                Data.StoreConfig("OverlayOpacity", Variables.OverlayOpacity.ToString(CultureInfo.InvariantCulture));
                Variables.Overlay.ApplySettings();
            }
        }

        private void UpdateValueLabels()
        {
            ConfidenceValue.Text = $"{ConfidenceSlider.Value:0}%";
            PressDurationValue.Text = $"{PressDurationSlider.Value:0} ms";
            OpacityValue.Text = $"{OpacitySlider.Value:0}%";
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateResponsiveLayout(e.NewSize.Width);

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) ToggleMaximized();
            else if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }

        private void MinimizeWindow_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void MaximizeWindow_Click(object sender, RoutedEventArgs e) => ToggleMaximized();

        private void CloseWindow_Click(object sender, RoutedEventArgs e) => Close();

        private void ToggleMaximized()
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            MaximizeButton.Content = WindowState == WindowState.Maximized ? "❐" : "□";
        }

        private void UpdateResponsiveLayout(double width)
        {
            bool compact = width < 850;
            SettingsLeftColumn.Width = new GridLength(1, GridUnitType.Star);
            SettingsGapColumn.Width = compact ? new GridLength(0) : new GridLength(18);
            SettingsRightColumn.Width = compact ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
            SettingsResponsiveGapRow.Height = compact ? new GridLength(0) : new GridLength(0);
            SettingsRightRow.Height = compact ? GridLength.Auto : new GridLength(0);
            Grid.SetRow(RightSettingsPanel, compact ? 2 : 0);
            Grid.SetColumn(RightSettingsPanel, compact ? 0 : 2);
        }

        private static string GetPTTInputDisplayName()
        {
            if (Data.TryGetPTTMouseButton(out System.Windows.Forms.MouseButtons button))
                return FormatMouseButton(button);

            if (Data.TryGetPTTKeyboardKey(out int virtualKey))
            {
                Key key = KeyInterop.KeyFromVirtualKey(virtualKey);
                return key == Key.None ? $"KEY {virtualKey}" : FormatKeyName(key);
            }

            return "NOT SET";
        }

        private static string FormatKeyName(Key key)
        {
            return key switch
            {
                Key.Capital => "CAPS LOCK",
                Key.Return => "ENTER",
                Key.Escape => "ESC",
                Key.LeftCtrl => "LEFT CTRL",
                Key.RightCtrl => "RIGHT CTRL",
                Key.LeftAlt => "LEFT ALT",
                Key.RightAlt => "RIGHT ALT",
                Key.LeftShift => "LEFT SHIFT",
                Key.RightShift => "RIGHT SHIFT",
                _ => key.ToString().ToUpperInvariant()
            };
        }

        private static string FormatMouseButton(System.Windows.Forms.MouseButtons button)
        {
            return button switch
            {
                System.Windows.Forms.MouseButtons.Left => "LEFT MOUSE",
                System.Windows.Forms.MouseButtons.Right => "RIGHT MOUSE",
                System.Windows.Forms.MouseButtons.Middle => "MIDDLE MOUSE",
                System.Windows.Forms.MouseButtons.XButton1 => "MOUSE 4",
                System.Windows.Forms.MouseButtons.XButton2 => "MOUSE 5",
                _ => button.ToString().ToUpperInvariant()
            };
        }
    }
}
