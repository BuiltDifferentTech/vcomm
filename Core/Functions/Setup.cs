namespace VComm.Core.Functions
{
    internal class Setup
    {
        /// <summary>
        /// Runs checks required before application runtime.
        /// </summary>
        public async Task RuntimeChecks()
        {
            await this.Log("Running Init...");
            await CheckForVPacks();
            await ApplySettings();
        }

        /// <summary>
        /// Makes changes/applies settings saved in the config file.
        /// </summary>
        public async Task<bool> ApplySettings()
        {
            await this.Log("Applying settings...");

            string? preferred = Data.GetConfig("ActiveVPack");
            if (preferred != null)
            {

                VPack? pack = Variables.VPacks.FirstOrDefault(pack => string.Equals(
                    pack.name,
                    preferred,
                    StringComparison.OrdinalIgnoreCase));
                if (pack != null)
                {
                    await this.Log($"Saved VPack: {preferred}");
                    Variables.ActiveVPack = pack;
                }
            }

            Variables.OverlayVisable = GetBooleanSetting("OverlayActive", defaultValue: true);
            Variables.UseChime = GetBooleanSetting("UseChime", defaultValue: true);
            string? customChimePath = Data.GetConfig("CustomChimePath");
            Variables.CustomChimePath = !string.IsNullOrWhiteSpace(customChimePath) && File.Exists(customChimePath)
                ? customChimePath
                : string.Empty;
            Variables.ShowRecognizedText = GetBooleanSetting("ShowRecognizedText", defaultValue: true);
            Variables.RecognitionConfidence = GetDoubleSetting("RecognitionConfidence", 0.65, 0, 1);
            Variables.KeyPressDurationMs = GetIntSetting("KeyPressDurationMs", 75, 10, 1000);
            Variables.OverlayOpacity = GetDoubleSetting("OverlayOpacity", 0.35, 0.1, 1);
            await this.Log($"Overlay hidden: {!Variables.OverlayVisable}");

            await this.Log($"All setting applied!");
            return true;
        }

        /// <summary>
        /// Performs a check of the VPacks folder for any available VPacks.
        /// </summary>
        public async Task<bool> CheckForVPacks()
        {
            await this.Log("Checking for VPacks...");
            string vPackDir = Variables.VPacksDirectory;
            Directory.CreateDirectory(vPackDir);

            DirectoryInfo packDirectoryInfo = new DirectoryInfo(vPackDir);
            FileInfo[] vPacks = packDirectoryInfo.GetFiles("*.vcomm");

            foreach (FileInfo vPack in vPacks)
            {
                VPack? pack = await Data.LoadVPack(new Uri(vPack.FullName));
                if (pack is null)
                {
                    await this.Log($"Couldn't load '{vPack.Name}' VPack!", error: true);
                    continue;
                }

                if (Variables.VPacks.Any(existing => string.Equals(
                    existing.name,
                    pack.name,
                    StringComparison.OrdinalIgnoreCase)))
                {
                    await this.Log($"VPack already exists! Ignoring '{pack.name}'.");
                    continue;
                }

                await this.Log($"Adding '{vPack.Name}' to VPacks...");
                Variables.VPacks.Add(pack);
            }

            if (Variables.VPacks.Count == 0)
            {
                await this.Log("No VPacks found, creating one for you...");

                Assembly assembly = Assembly.GetExecutingAssembly();
                IEnumerable<string> defaultVPacks = assembly.GetManifestResourceNames()
                    .Where(name => name.StartsWith("VComm.Core.Assets.VPacks", StringComparison.Ordinal));

                foreach (string defaultVPack in defaultVPacks)
                {
                    await using Stream? stream = assembly.GetManifestResourceStream(defaultVPack);
                    if (stream is null)
                    {
                        await this.Log($"Couldn't read embedded VPack '{defaultVPack}'.", error: true);
                        continue;
                    }

                    using StreamReader reader = new StreamReader(stream);
                    string result = await reader.ReadToEndAsync();
                    VPack? vPack = Data.LoadVPack(result);
                    if (vPack is null)
                    {
                        await this.Log($"Embedded VPack '{defaultVPack}' is invalid.", error: true);
                        continue;
                    }

                    await Data.SaveVPack(vPack);
                    Variables.VPacks.Add(vPack);
                    Variables.FirstRun = true;
                }
            }

            return true;
        }

        private static bool GetBooleanSetting(string name, bool defaultValue)
        {
            return bool.TryParse(Data.GetConfig(name), out bool value) ? value : defaultValue;
        }

        private static int GetIntSetting(string name, int defaultValue, int minimum, int maximum)
        {
            return int.TryParse(Data.GetConfig(name), out int value)
                ? Math.Clamp(value, minimum, maximum)
                : defaultValue;
        }

        private static double GetDoubleSetting(string name, double defaultValue, double minimum, double maximum)
        {
            return double.TryParse(
                    Data.GetConfig(name),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out double value)
                ? Math.Clamp(value, minimum, maximum)
                : defaultValue;
        }
    }
}
