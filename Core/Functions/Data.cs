namespace VComm.Core.Functions
{
    internal static class Data
    {
        private static readonly object ConfigLock = new object();

        /// <summary>
        /// Loads a VPack from a file path.
        /// </summary>
        /// <param name="filepath">The URI to the file you wish to load a VPack</param>
        /// <returns>The VPack parsed from the file</returns>
        public static async Task<VPack?> LoadVPack(Uri filepath)
        {
            try
            {
                string json = await File.ReadAllTextAsync(filepath.LocalPath);
                return LoadVPack(json);
            }
            catch (Exception ex)
            {
                await typeof(Data).Log($"Failed to load VPack '{filepath.LocalPath}': {ex.Message}", error: true);
                return null;
            }
        }

        /// <summary>
        /// Loads a VPack from a string.
        /// </summary>
        /// <param name="json">The JSON string containing the VPack</param>
        /// <returns>The VPack parsed from the string</returns>
        public static VPack? LoadVPack(string json)
        {
            try
            {
                VPack? vPack = JsonConvert.DeserializeObject<VPack>(json);
                return GetVPackValidationErrors(vPack).Count == 0 ? vPack : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        internal static IReadOnlyList<string> GetVPackValidationErrors(VPack? vPack)
        {
            List<string> errors = new List<string>();
            if (vPack is null)
            {
                errors.Add("The pack could not be read.");
                return errors;
            }

            if (string.IsNullOrWhiteSpace(vPack.name)) errors.Add("Enter a pack name.");
            if (vPack.vRequests is not { Count: > 0 })
            {
                errors.Add("Add at least one voice command.");
                return errors;
            }

            Dictionary<string, int> phraseOwners = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            for (int requestIndex = 0; requestIndex < vPack.vRequests.Count; requestIndex++)
            {
                VRequest? request = vPack.vRequests[requestIndex];
                string commandName = $"Command {requestIndex + 1}";
                if (request is null)
                {
                    errors.Add($"{commandName} is missing.");
                    continue;
                }

                if (request.phrases is not { Length: > 0 } || request.phrases.Any(string.IsNullOrWhiteSpace))
                    errors.Add($"{commandName} needs at least one phrase.");
                else
                {
                    foreach (string phrase in request.phrases)
                    {
                        string normalizedPhrase = phrase.Trim();
                        if (phraseOwners.TryGetValue(normalizedPhrase, out int ownerIndex) && ownerIndex != requestIndex)
                            errors.Add($"'{normalizedPhrase}' is used by both Command {ownerIndex + 1} and {commandName}.");
                        else phraseOwners[normalizedPhrase] = requestIndex;
                    }
                }
                if (request.macro is null)
                {
                    errors.Add($"{commandName} needs an action sequence.");
                    continue;
                }

                if (request.macro.msToWait < 0) errors.Add($"{commandName}'s delay cannot be negative.");
                if (request.macro.keycodes is not { Count: > 0 })
                {
                    errors.Add($"{commandName} needs at least one input action.");
                    continue;
                }

                for (int inputIndex = 0; inputIndex < request.macro.keycodes.Count; inputIndex++)
                {
                    string keycode = request.macro.keycodes[inputIndex];
                    if (!Simulate.TryParseInput(keycode, out _))
                        errors.Add($"{commandName}, action {inputIndex + 1} has an unsupported input: '{keycode}'.");
                }
            }

            return errors;
        }

        /// <summary>
        /// Saves a VPack to the VPacks folder.
        /// </summary>
        /// <param name="vPack">The VPack you would like to save</param>
        public static async Task SaveVPack(VPack vPack)
        {
            string vPackDir = Variables.VPacksDirectory;
            Directory.CreateDirectory(vPackDir);

            string safeName = string.Join("_", vPack.name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Trim();
            if (string.IsNullOrWhiteSpace(safeName))
                throw new ArgumentException("The VPack must have a valid name.", nameof(vPack));

            string pureFilepath = Path.Combine(vPackDir, safeName + ".vcomm");

            string json = JsonConvert.SerializeObject(vPack, formatting: Formatting.Indented);
            await File.WriteAllTextAsync(pureFilepath, json);
        }

        /// <summary>
        /// Stores a value to the config file.
        /// </summary>
        /// <param name="name">The title/key for the data you would like to store</param>
        /// <param name="data">The string data you would like to store</param>
        public static void StoreConfig(string name, string data)
        {
            lock (ConfigLock)
            {
                ExeConfigurationFileMap fileMap = new ExeConfigurationFileMap
                {
                    ExeConfigFilename = Variables.ConfigFile
                };
                Configuration configuration = ConfigurationManager.OpenMappedExeConfiguration(fileMap, ConfigurationUserLevel.None);
                KeyValueConfigurationCollection settings = configuration.AppSettings.Settings;
                if (settings[name] == null) settings.Add(name, data);
                else settings[name].Value = data;
                configuration.Save(ConfigurationSaveMode.Modified);
            }
        }

        /// <summary>
        /// Gets a values from the config file.
        /// </summary>
        /// <param name="name">The title/key for the data you would like to retrieve</param>
        /// <returns>The string data from the config file</returns>
        public static string? GetConfig(string name)
        {
            lock (ConfigLock)
            {
                ExeConfigurationFileMap fileMap = new ExeConfigurationFileMap
                {
                    ExeConfigFilename = Variables.ConfigFile
                };
                Configuration configuration = ConfigurationManager.OpenMappedExeConfiguration(fileMap, ConfigurationUserLevel.None);
                return configuration.AppSettings.Settings[name]?.Value;
            }
        }

        /// <summary>
        /// Is Push-To-Talk currently active?
        /// </summary>
        /// <returns>true if config contains (PTTActive == true)</returns>
        public static bool isPTTActive()
        {
            bool pttActive = false;
            bool.TryParse(Data.GetConfig("PTTActive"), out pttActive);
            return pttActive;
        }

        /// <summary>
        /// Gets the saved Push-To-Talk key ID.
        /// </summary>
        /// <returns>The Virtual Key ID saved in the config</returns>
        public static int getPTTKey()
        {
            string? storedKey = Data.GetConfig("PTTKey");
            return int.TryParse(storedKey, out int pttKey) && pttKey > 0 ? pttKey : -1;
        }

        public static string GetPTTInput()
        {
            string? storedInput = GetConfig("PTTInput");
            if (!string.IsNullOrWhiteSpace(storedInput))
                return storedInput;

            int legacyKey = getPTTKey();
            return legacyKey > 0 ? $"KEY:{legacyKey}" : $"KEY:{KeyInterop.VirtualKeyFromKey(Key.Capital)}";
        }

        public static void StorePTTKeyboardKey(int virtualKey)
        {
            StoreConfig("PTTInput", $"KEY:{virtualKey}");
            StoreConfig("PTTKey", virtualKey.ToString());
        }

        public static void StorePTTMouseButton(System.Windows.Forms.MouseButtons button)
        {
            StoreConfig("PTTInput", $"MOUSE:{button}");
        }

        public static bool TryGetPTTKeyboardKey(out int virtualKey)
        {
            virtualKey = -1;
            string input = GetPTTInput();
            return input.StartsWith("KEY:", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(input[4..], out virtualKey)
                && virtualKey > 0;
        }

        public static bool TryGetPTTMouseButton(out System.Windows.Forms.MouseButtons button)
        {
            button = System.Windows.Forms.MouseButtons.None;
            string input = GetPTTInput();
            return input.StartsWith("MOUSE:", StringComparison.OrdinalIgnoreCase)
                && Enum.TryParse(input[6..], ignoreCase: true, out button)
                && button != System.Windows.Forms.MouseButtons.None;
        }

    }
}
