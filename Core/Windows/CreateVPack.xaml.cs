using System.Collections.ObjectModel;
using System.ComponentModel;

namespace VComm.Core.Windows
{
    public partial class CreateVPack : Window
    {
        private readonly ObservableCollection<CommandDraft> commands = new ObservableCollection<CommandDraft>();
        private readonly string? sourceName;
        private CancellationTokenSource? phraseCaptureCancellation;
        private bool closeAfterPhraseCapture;
        private bool isLoadingSelection;
        private bool isRecordingInputs;

        internal CreateVPack(VPack? source = null)
        {
            InitializeComponent();
            sourceName = source?.name;
            CommandList.ItemsSource = commands;
            PackNameBox.Text = source?.name ?? string.Empty;
            AuthorBox.Text = source?.author ?? string.Empty;

            if (source?.vRequests is { Count: > 0 })
            {
                foreach (VRequest request in source.vRequests) commands.Add(CommandDraft.FromRequest(request));
            }
            else commands.Add(new CommandDraft());

            CommandList.SelectedIndex = 0;
            RefreshCommandList();
            UpdateResponsiveLayout(ActualWidth);
        }

        internal VPack? SavedVPack { get; private set; }

        public IReadOnlyList<string> AvailableInputs { get; } = new[]
        {
            "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M",
            "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z",
            "0", "1", "2", "3", "4", "5", "6", "7", "8", "9",
            "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12",
            "ENTER", "ESC", "SPACE", "TAB", "CTRL", "LCTRL", "RCTRL", "SHIFT", "LSHIFT", "RSHIFT", "ALT", "LALT", "RALT", "CAPSLOCK",
            "UP", "DOWN", "LEFT", "RIGHT", "HOME", "END", "PAGEUP", "PAGEDOWN", "DELETE",
            "LMB", "RMB", "MMB", "MOUSE4", "MOUSE5", "WHEELUP", "WHEELDOWN", "WHEELLEFT", "WHEELRIGHT"
        };

        private CommandDraft? SelectedCommand => CommandList.SelectedItem as CommandDraft;

        private void CommandList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            isLoadingSelection = true;
            CommandDraft? selected = SelectedCommand;
            PhraseList.ItemsSource = selected?.Phrases;
            ActionList.ItemsSource = selected?.Actions;
            DelayBox.Text = selected?.DelayMs.ToString(CultureInfo.InvariantCulture) ?? "0";
            isLoadingSelection = false;
            ValidateEditor();
        }

        private void AddCommand_Click(object sender, RoutedEventArgs e)
        {
            CommandDraft command = new CommandDraft();
            commands.Add(command);
            CommandList.SelectedItem = command;
            RefreshCommandList();
            PhraseBox.Focus();
        }

        private void DuplicateCommand_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedCommand is not CommandDraft selected) return;
            CommandDraft copy = selected.Clone();
            commands.Add(copy);
            CommandList.SelectedItem = copy;
            RefreshCommandList();
        }

        private void DeleteCommand_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedCommand is not CommandDraft selected) return;
            if (commands.Count == 1)
            {
                ShowStatus("A pack must contain at least one voice command.", isError: true);
                return;
            }

            int index = CommandList.SelectedIndex;
            commands.Remove(selected);
            CommandList.SelectedIndex = Math.Min(index, commands.Count - 1);
            RefreshCommandList();
        }

        private void AddPhrase_Click(object sender, RoutedEventArgs e) => AddPhrase();

        private void PhraseBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            AddPhrase();
            e.Handled = true;
        }

        private bool AddPhrase()
        {
            if (SelectedCommand is not CommandDraft selected) return false;
            string phrase = PhraseBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(phrase))
            {
                PhraseError.Text = "Enter a phrase first.";
                return false;
            }

            if (selected.Phrases.Any(existing => string.Equals(existing, phrase, StringComparison.OrdinalIgnoreCase)))
            {
                PhraseError.Text = "That phrase is already in this command.";
                return false;
            }

            if (commands.Where(command => command != selected).Any(command =>
                    command.Phrases.Any(existing => string.Equals(existing, phrase, StringComparison.OrdinalIgnoreCase))))
            {
                PhraseError.Text = "That phrase already triggers another command in this pack.";
                return false;
            }

            selected.Phrases.Add(phrase);
            PhraseBox.Clear();
            RefreshCommandList();
            ValidateEditor();
            PhraseBox.Focus();
            return true;
        }

        private void RemovePhrase_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedCommand is not CommandDraft selected || sender is not System.Windows.Controls.Button { DataContext: string phrase }) return;
            selected.Phrases.Remove(phrase);
            RefreshCommandList();
            ValidateEditor();
        }

        private async void ListenPhrase_Click(object sender, RoutedEventArgs e)
        {
            if (phraseCaptureCancellation is not null) return;

            phraseCaptureCancellation = new CancellationTokenSource();
            ListenPhraseButton.IsEnabled = false;
            ListeningPanel.Visibility = Visibility.Visible;
            ListeningText.Text = "Listening… say the phrase naturally";
            CaptureLevel.Value = 0;
            PhraseError.Text = string.Empty;
            try
            {
                PhraseCaptureResult? result = await Variables.Overlay.voiceEngine.CapturePhraseAsync(
                    TimeSpan.FromSeconds(8),
                    level => Dispatcher.InvokeAsync(() => CaptureLevel.Value = level),
                    phraseCaptureCancellation.Token);
                if (result is null || string.IsNullOrWhiteSpace(result.Text))
                {
                    PhraseError.Text = "I didn't catch that. Try again or type the phrase.";
                    return;
                }

                PhraseBox.Text = result.Text;
                if (AddPhrase())
                    ShowStatus($"Added “{result.Text}” from your microphone ({result.Confidence:P0} confidence).", isError: false);
            }
            catch (Exception ex)
            {
                PhraseError.Text = $"Voice capture could not start: {ex.Message}";
            }
            finally
            {
                phraseCaptureCancellation.Dispose();
                phraseCaptureCancellation = null;
                ListenPhraseButton.IsEnabled = true;
                ListeningPanel.Visibility = Visibility.Collapsed;
                CaptureLevel.Value = 0;
                if (closeAfterPhraseCapture)
                {
                    closeAfterPhraseCapture = false;
                    _ = Dispatcher.BeginInvoke(Close);
                }
            }
        }

        private void CancelPhraseCapture_Click(object sender, RoutedEventArgs e)
        {
            ListeningText.Text = "Stopping…";
            phraseCaptureCancellation?.Cancel();
        }

        private void AddAction_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedCommand is not CommandDraft selected) return;
            string token = InputPicker.Text.Trim();
            string serialized = HoldInputCheck.IsChecked == true ? $"(HOLD){{{TrimBraces(token)}}}" : $"{{{TrimBraces(token)}}}";
            if (!Simulate.TryParseInput(serialized, out _))
            {
                ActionError.Text = string.IsNullOrWhiteSpace(token)
                    ? "Choose or type an input first."
                    : $"'{token}' is not a supported keyboard or mouse input.";
                return;
            }

            selected.Actions.Add(new ActionDraft(serialized));
            selected.RefreshActionPositions();
            InputPicker.Text = string.Empty;
            HoldInputCheck.IsChecked = false;
            RefreshCommandList();
            ValidateEditor();
            InputPicker.Focus();
        }

        private void RemoveAction_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedCommand is not CommandDraft selected || sender is not System.Windows.Controls.Button { DataContext: ActionDraft action }) return;
            selected.Actions.Remove(action);
            selected.RefreshActionPositions();
            RefreshCommandList();
            ValidateEditor();
        }

        private void MoveActionUp_Click(object sender, RoutedEventArgs e) => MoveAction(sender, -1);
        private void MoveActionDown_Click(object sender, RoutedEventArgs e) => MoveAction(sender, 1);

        private void MoveAction(object sender, int offset)
        {
            if (SelectedCommand is not CommandDraft selected || sender is not System.Windows.Controls.Button { DataContext: ActionDraft action }) return;
            int oldIndex = selected.Actions.IndexOf(action);
            int newIndex = oldIndex + offset;
            if (oldIndex < 0 || newIndex < 0 || newIndex >= selected.Actions.Count) return;
            selected.Actions.Move(oldIndex, newIndex);
            selected.RefreshActionPositions();
        }

        private void RecordInputs_Click(object sender, RoutedEventArgs e)
        {
            isRecordingInputs = !isRecordingInputs;
            RecordingPanel.Visibility = isRecordingInputs ? Visibility.Visible : Visibility.Collapsed;
            RecordInputsButton.Content = isRecordingInputs ? "■ STOP RECORDING" : "● RECORD KEY SEQUENCE";
            if (isRecordingInputs)
            {
                RecordInputsButton.Focus();
                ShowStatus("Recording keyboard input. Modifier keys will remain held until the sequence finishes.", isError: false);
            }
            else
            {
                ShowStatus("Keyboard sequence added. Review its order below.", isError: false);
                ValidateEditor();
            }
        }

        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (!isRecordingInputs || e.IsRepeat || SelectedCommand is not CommandDraft selected) return;

            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            string? token = GetMacroToken(key);
            if (token is null) return;

            bool keepHeld = key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt;
            string serialized = keepHeld ? $"(HOLD){{{token}}}" : $"{{{token}}}";
            if (keepHeld && selected.Actions.Any(action => string.Equals(action.Token, serialized, StringComparison.OrdinalIgnoreCase)))
            {
                e.Handled = true;
                return;
            }

            if (Simulate.TryParseInput(serialized, out _))
            {
                selected.Actions.Add(new ActionDraft(serialized));
                selected.RefreshActionPositions();
                RefreshCommandList();
                ActionError.Text = string.Empty;
            }
            e.Handled = true;
        }

        private void DelayBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isLoadingSelection || SelectedCommand is not CommandDraft selected) return;
            if (int.TryParse(DelayBox.Text, NumberStyles.None, CultureInfo.InvariantCulture, out int delay) && delay >= 0)
                selected.DelayMs = delay;
            ValidateEditor();
        }

        private void PackDetails_Changed(object sender, TextChangedEventArgs e)
        {
            if (IsInitialized) ValidateEditor();
        }

        private async void SavePack_Click(object sender, RoutedEventArgs e)
        {
            VPack pack = BuildPack();
            List<string> errors = Data.GetVPackValidationErrors(pack).ToList();
            if (!int.TryParse(DelayBox.Text, NumberStyles.None, CultureInfo.InvariantCulture, out _))
                errors.Add("The selected command's delay must be a whole number.");

            if (errors.Count > 0)
            {
                ShowStatus(string.Join("  ", errors), isError: true);
                ValidateEditor();
                return;
            }

            bool overwritesAnotherPack = Variables.VPacks.Any(existing =>
                string.Equals(existing.name, pack.name, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(existing.name, sourceName, StringComparison.OrdinalIgnoreCase));
            if (overwritesAnotherPack && System.Windows.MessageBox.Show(
                    this,
                    $"A pack named '{pack.name}' already exists. Replace it?",
                    "Replace VPack",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            try
            {
                await Data.SaveVPack(pack);
                SavedVPack = pack;
                DialogResult = true;
            }
            catch (Exception ex)
            {
                ShowStatus($"VComm could not save this pack: {ex.Message}", isError: true);
            }
        }

        private VPack BuildPack() => new VPack
        {
            name = PackNameBox.Text.Trim(),
            author = AuthorBox.Text.Trim(),
            vRequests = commands.Select(command => command.ToRequest()).ToList()
        };

        private void ValidateEditor()
        {
            if (PackNameError is null) return;
            PackNameError.Text = string.IsNullOrWhiteSpace(PackNameBox.Text) ? "Enter a pack name." : string.Empty;
            CommandDraft? selected = SelectedCommand;
            PhraseError.Text = selected is not null && selected.Phrases.Count == 0 ? "Add at least one phrase." : string.Empty;
            ActionError.Text = selected is not null && selected.Actions.Count == 0 ? "Add at least one input action." : string.Empty;
            DelayError.Text = !int.TryParse(DelayBox.Text, NumberStyles.None, CultureInfo.InvariantCulture, out int delay) || delay < 0
                ? "Enter a whole number of 0 or more."
                : string.Empty;

            if (string.IsNullOrEmpty(PackNameError.Text) && string.IsNullOrEmpty(PhraseError.Text)
                && string.IsNullOrEmpty(ActionError.Text) && string.IsNullOrEmpty(DelayError.Text))
                ShowStatus("Ready to save.", isError: false);
        }

        private void ShowStatus(string message, bool isError)
        {
            StatusText.Text = message;
            StatusText.Foreground = new SolidColorBrush(isError
                ? System.Windows.Media.Color.FromRgb(255, 107, 107)
                : System.Windows.Media.Color.FromRgb(170, 170, 170));
        }

        private void RefreshCommandList()
        {
            foreach (CommandDraft command in commands) command.NotifySummaryChanged();
            CommandList.Items.Refresh();
            CommandCount.Text = commands.Count == 1 ? "1 command" : $"{commands.Count} commands";
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
            bool compact = width < 900;
            CommandColumn.Width = compact ? new GridLength(1, GridUnitType.Star) : new GridLength(300);
            GapColumn.Width = compact ? new GridLength(0) : new GridLength(16);
            EditorColumn.Width = compact ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
            CommandRow.Height = compact ? new GridLength(180) : new GridLength(1, GridUnitType.Star);
            ResponsiveGapRow.Height = compact ? new GridLength(12) : new GridLength(0);
            EditorRow.Height = compact ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
            Grid.SetRow(EditorPanel, compact ? 2 : 0);
            Grid.SetColumn(EditorPanel, compact ? 0 : 2);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

        private void Window_Closing(object? sender, CancelEventArgs e)
        {
            isRecordingInputs = false;
            if (phraseCaptureCancellation is null) return;

            e.Cancel = true;
            closeAfterPhraseCapture = true;
            ListeningText.Text = "Stopping voice capture…";
            phraseCaptureCancellation.Cancel();
        }

        private static string? GetMacroToken(Key key)
        {
            return key switch
            {
                Key.LeftCtrl => "LCTRL",
                Key.RightCtrl => "RCTRL",
                Key.LeftShift => "LSHIFT",
                Key.RightShift => "RSHIFT",
                Key.LeftAlt => "LALT",
                Key.RightAlt => "RALT",
                Key.Return => "ENTER",
                Key.Escape => "ESC",
                Key.Space => "SPACE",
                Key.Capital => "CAPSLOCK",
                Key.Prior => "PAGEUP",
                Key.Next => "PAGEDOWN",
                Key.None => null,
                _ => key.ToString().ToUpperInvariant()
            };
        }

        private static string TrimBraces(string token)
        {
            string trimmed = token.Trim();
            if (trimmed.StartsWith("(HOLD)", StringComparison.OrdinalIgnoreCase)) trimmed = trimmed[6..].Trim();
            return trimmed.Length >= 2 && trimmed[0] == '{' && trimmed[^1] == '}'
                ? trimmed[1..^1].Trim()
                : trimmed;
        }

        private sealed class CommandDraft : INotifyPropertyChanged
        {
            public ObservableCollection<string> Phrases { get; } = new ObservableCollection<string>();
            public ObservableCollection<ActionDraft> Actions { get; } = new ObservableCollection<ActionDraft>();
            public int DelayMs { get; set; }
            public string PrimaryPhrase => Phrases.FirstOrDefault() ?? "New voice command";
            public string Summary => $"{Phrases.Count} phrase{(Phrases.Count == 1 ? string.Empty : "s")} · {Actions.Count} action{(Actions.Count == 1 ? string.Empty : "s")}";
            public event PropertyChangedEventHandler? PropertyChanged;

            public static CommandDraft FromRequest(VRequest request)
            {
                CommandDraft draft = new CommandDraft { DelayMs = Math.Max(0, request.macro?.msToWait ?? 0) };
                foreach (string phrase in request.phrases ?? Array.Empty<string>()) draft.Phrases.Add(phrase);
                foreach (string action in request.macro?.keycodes ?? new List<string>()) draft.Actions.Add(new ActionDraft(action));
                draft.RefreshActionPositions();
                return draft;
            }

            public CommandDraft Clone()
            {
                CommandDraft clone = new CommandDraft { DelayMs = DelayMs };
                foreach (string phrase in Phrases) clone.Phrases.Add(phrase);
                foreach (ActionDraft action in Actions) clone.Actions.Add(new ActionDraft(action.Token));
                clone.RefreshActionPositions();
                return clone;
            }

            public VRequest ToRequest() => new VRequest
            {
                phrases = Phrases.ToArray(),
                macro = new Macro { msToWait = DelayMs, keycodes = Actions.Select(action => action.Token).ToList() }
            };

            public void RefreshActionPositions()
            {
                for (int index = 0; index < Actions.Count; index++) Actions[index].Position = index + 1;
                NotifySummaryChanged();
            }

            public void NotifySummaryChanged()
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PrimaryPhrase)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Summary)));
            }
        }

        private sealed class ActionDraft : INotifyPropertyChanged
        {
            private int position;
            public ActionDraft(string token) => Token = token;
            public string Token { get; }
            public string DisplayToken => Token.Replace("(HOLD)", string.Empty, StringComparison.OrdinalIgnoreCase).Trim('{', '}');
            public string Behaviour => Token.StartsWith("(HOLD)", StringComparison.OrdinalIgnoreCase)
                ? "Keep held through every following step, then release"
                : "Press and release";
            public int Position
            {
                get => position;
                set { position = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Position))); }
            }
            public event PropertyChangedEventHandler? PropertyChanged;
        }
    }
}
