namespace VComm.Core.Functions
{
    internal sealed class VoiceEngine
    {
        private SpeechRecognitionEngine? engine;
        private IKeyboardMouseEvents? inputHook;
        public bool listening = false;

        /// <summary>
        /// Starts the voice recognizer using the currently active VPack.
        /// </summary>
        public async Task StartEngine()
        {
            VPack activeVPack = Variables.ActiveVPack
                ?? throw new InvalidOperationException("No valid VPack is available.");

            if (engine is not null)
                await StopEngine();

            await this.Log("Starting VoiceEngine...");

            SpeechRecognitionEngine newEngine = new SpeechRecognitionEngine();
            try
            {
                Choices phrases = new Choices();
                IEnumerable<string> uniquePhrases = activeVPack.vRequests
                    .SelectMany(request => request.phrases)
                    .Distinct(StringComparer.OrdinalIgnoreCase);

                foreach (string phrase in uniquePhrases)
                {
                    await this.Log($"Adding Engine listener for phrase: {phrase}");
                    phrases.Add(phrase);
                }

                GrammarBuilder builder = new GrammarBuilder();
                builder.Append(phrases);
                Grammar grammar = new Grammar(builder)
                {
                    Name = "VComm",
                    Enabled = true
                };

                newEngine.LoadGrammar(grammar);
                newEngine.SetInputToDefaultAudioDevice();
                newEngine.SpeechHypothesized += Engine_SpeechHypothesized;
                newEngine.SpeechRecognized += Engine_SpeechRecognized;
                newEngine.AudioLevelUpdated += Engine_AudioLevelChange;
                newEngine.AudioStateChanged += Engine_AudioStateChanged;
                engine = newEngine;
            }
            catch
            {
                newEngine.Dispose();
                throw;
            }

            try
            {
                if (Data.isPTTActive())
                {
                    SubscribeToPTTHooks();
                    await this.Log("PTT Active.");
                }
                else
                {
                    await StartListening();
                }
            }
            catch
            {
                await StopEngine();
                throw;
            }

            await this.Log("VoiceEngine ready!");
        }

        private void SubscribeToPTTHooks()
        {
            RemovePTTHooks();
            inputHook = Hook.GlobalEvents();
            inputHook.KeyDown += OnKeyDown;
            inputHook.KeyUp += OnKeyUp;
            inputHook.MouseDownExt += OnMouseDown;
            inputHook.MouseUpExt += OnMouseUp;
        }

        private void RemovePTTHooks()
        {
            if (inputHook is null)
                return;

            inputHook.KeyDown -= OnKeyDown;
            inputHook.KeyUp -= OnKeyUp;
            inputHook.MouseDownExt -= OnMouseDown;
            inputHook.MouseUpExt -= OnMouseUp;
            inputHook.Dispose();
            inputHook = null;
        }

        private async void OnKeyDown(object? sender, System.Windows.Forms.KeyEventArgs e)
        {
            try
            {
                if (Data.isPTTActive() && Data.TryGetPTTKeyboardKey(out int key) && e.KeyValue == key && !listening)
                    await StartListening();
            }
            catch (Exception ex)
            {
                await this.Log($"Failed to start push-to-talk: {ex.Message}", error: true);
            }
        }

        private async void OnKeyUp(object? sender, System.Windows.Forms.KeyEventArgs e)
        {
            try
            {
                if (Data.isPTTActive() && Data.TryGetPTTKeyboardKey(out int key) && e.KeyValue == key && listening)
                    await StopListening();
            }
            catch (Exception ex)
            {
                await this.Log($"Failed to stop push-to-talk: {ex.Message}", error: true);
            }
        }

        private async void OnMouseDown(object? sender, MouseEventExtArgs e)
        {
            try
            {
                if (Data.isPTTActive() && Data.TryGetPTTMouseButton(out System.Windows.Forms.MouseButtons button)
                    && e.Button == button && !listening)
                    await StartListening();
            }
            catch (Exception ex)
            {
                await this.Log($"Failed to start mouse push-to-talk: {ex.Message}", error: true);
            }
        }

        private async void OnMouseUp(object? sender, MouseEventExtArgs e)
        {
            try
            {
                if (Data.isPTTActive() && Data.TryGetPTTMouseButton(out System.Windows.Forms.MouseButtons button)
                    && e.Button == button && listening)
                    await StopListening();
            }
            catch (Exception ex)
            {
                await this.Log($"Failed to stop mouse push-to-talk: {ex.Message}", error: true);
            }
        }

        public async Task StartListening()
        {
            if (listening)
                return;

            SpeechRecognitionEngine currentEngine = engine
                ?? throw new InvalidOperationException("The voice engine has not been started.");

            currentEngine.RecognizeAsync(RecognizeMode.Multiple);
            listening = true;
            await this.Log("Started listening...");
        }

        public async Task StopListening()
        {
            if (!listening)
                return;

            engine?.RecognizeAsyncStop();
            listening = false;
            await this.Log("Stopped listening...");
        }

        private async void Engine_AudioStateChanged(object? sender, AudioStateChangedEventArgs e)
        {
            await this.Log($"Audio State Changed: {e.AudioState}");
            if (e.AudioState is AudioState.Silence or AudioState.Stopped)
                Variables.Overlay.Volume = 0;
            if (e.AudioState == AudioState.Stopped)
                listening = false;
        }

        public async Task ReloadEngine()
        {
            await this.Log("Reloading the VoiceEngine...");
            await StopEngine();
            await StartEngine();
        }

        /// <summary>
        /// Temporarily pauses normal command recognition and listens for one free-form phrase.
        /// The command engine is restored before this method returns.
        /// </summary>
        public async Task<PhraseCaptureResult?> CapturePhraseAsync(
            TimeSpan timeout,
            Action<int>? audioLevelChanged = null,
            CancellationToken cancellationToken = default)
        {
            await StopEngine();
            try
            {
                using SpeechRecognitionEngine captureEngine = new SpeechRecognitionEngine();
                captureEngine.LoadGrammar(new DictationGrammar());
                captureEngine.SetInputToDefaultAudioDevice();

                TaskCompletionSource<PhraseCaptureResult?> completion = new TaskCompletionSource<PhraseCaptureResult?>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                captureEngine.AudioLevelUpdated += (_, args) => audioLevelChanged?.Invoke(args.AudioLevel);
                captureEngine.SpeechRecognized += (_, args) => completion.TrySetResult(
                    new PhraseCaptureResult(args.Result.Text.Trim(), args.Result.Confidence));
                captureEngine.RecognizeCompleted += (_, _) => completion.TrySetResult(null);

                using CancellationTokenSource timeoutSource = new CancellationTokenSource(timeout);
                using CancellationTokenSource linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    timeoutSource.Token);
                using CancellationTokenRegistration registration = linkedSource.Token.Register(() =>
                {
                    try { captureEngine.RecognizeAsyncCancel(); }
                    catch (InvalidOperationException) { }
                    completion.TrySetResult(null);
                });

                captureEngine.RecognizeAsync(RecognizeMode.Single);
                return await completion.Task;
            }
            finally
            {
                await StartEngine();
            }
        }

        public async Task StopEngine()
        {
            await this.Log("Stopping VoiceEngine...");
            RemovePTTHooks();

            SpeechRecognitionEngine? currentEngine = engine;
            engine = null;
            bool wasListening = listening;
            listening = false;
            if (currentEngine is null)
                return;

            currentEngine.SpeechHypothesized -= Engine_SpeechHypothesized;
            currentEngine.SpeechRecognized -= Engine_SpeechRecognized;
            currentEngine.AudioLevelUpdated -= Engine_AudioLevelChange;
            currentEngine.AudioStateChanged -= Engine_AudioStateChanged;
            if (wasListening)
                currentEngine.RecognizeAsyncCancel();
            currentEngine.UnloadAllGrammars();
            currentEngine.Dispose();
        }

        private void Engine_AudioLevelChange(object? sender, AudioLevelUpdatedEventArgs e)
        {
            Variables.Overlay.Volume = e.AudioLevel;
        }

        private async void Engine_SpeechHypothesized(object? sender, SpeechHypothesizedEventArgs e)
        {
            try
            {
                await this.Log($"Detected text: {e.Result.Text}?");
                if (Variables.ShowRecognizedText)
                    await Variables.Overlay.SetSpeechText(e.Result.Text.ToUpperInvariant(), hypothetical: true);
            }
            catch (Exception ex)
            {
                await this.Log($"Failed to display hypothesized speech: {ex.Message}", error: true);
            }
        }

        private async void Engine_SpeechRecognized(object? sender, SpeechRecognizedEventArgs e)
        {
            try
            {
                string request = e.Result.Text;
                await this.Log($"Detected text: {request}.");
                if (e.Result.Confidence < Variables.RecognitionConfidence)
                {
                    await this.Log($"Ignored '{request}' at {e.Result.Confidence:P0}; minimum is {Variables.RecognitionConfidence:P0}.");
                    return;
                }

                if (Variables.ShowRecognizedText)
                    await Variables.Overlay.SetSpeechText(request.ToUpperInvariant());

                VRequest? vRequest = FindVRequest(request);
                if (vRequest is null)
                {
                    await this.Log($"No macro was found for recognized phrase '{request}'.", error: true);
                    return;
                }

                await this.Log($"Sending '{vRequest.macro}' from VPack: {Variables.ActiveVPack}");

                Task chimeTask = Variables.UseChime ? Chime() : Task.CompletedTask;
                Task<bool> macroTask = Simulate.Press(vRequest.macro);
                await Task.WhenAll(chimeTask, macroTask);

                if (!await macroTask)
                    await this.Log("Failed to simulate macro!", error: true);
            }
            catch (Exception ex)
            {
                await this.Log($"Failed to process recognized speech: {ex.Message}", error: true);
            }
        }

        public VRequest? FindVRequest(string requestRecognized)
        {
            return Variables.ActiveVPack?.vRequests.FirstOrDefault(request =>
                request.phrases.Any(phrase => string.Equals(
                    phrase,
                    requestRecognized,
                    StringComparison.OrdinalIgnoreCase)));
        }

        public async Task Chime()
        {
            await Task.Run(() =>
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                using Stream stream = File.Exists(Variables.CustomChimePath)
                    ? File.OpenRead(Variables.CustomChimePath)
                    : assembly.GetManifestResourceStream(Variables.DefaultChimeResource)
                        ?? throw new InvalidOperationException($"Chime resource '{Variables.DefaultChimeResource}' was not found.");
                using SoundPlayer player = new SoundPlayer(stream);
                player.PlaySync();
            });
        }
    }

    internal sealed record PhraseCaptureResult(string Text, float Confidence);
}
