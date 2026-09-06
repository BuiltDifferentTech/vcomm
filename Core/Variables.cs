namespace VComm.Core
{
    internal class Variables
    {
        public static string AppDirectory { get; } = AppContext.BaseDirectory;
        public static string AppFilePath { get; } = Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location;
        public static string LogFolder { get; } = Path.Combine(AppDirectory, "Logs");
        public static string LogFilePath { get; } = Path.Combine(LogFolder, $"{Process.GetCurrentProcess().StartTime:yyyy-MM-dd--HH-mm-ss}.txt");
        public static string Version { get; } = FileVersionInfo.GetVersionInfo(AppFilePath).FileVersion ?? "Unknown";
        public static string VPacksDirectory { get; } = Path.Combine(AppDirectory, "VPacks");
        public static string ConfigFile { get; } = Path.Combine(AppDirectory, "VComm.config");
        public static Overlay Overlay { get; set; } = null!;
        public static bool OverlayVisable = true;

        public static bool UseChime = true;
        public static bool ShowRecognizedText = true;
        public static double RecognitionConfidence = 0.65;
        public static int KeyPressDurationMs = 75;
        public static double OverlayOpacity = 0.35;
        public static string ChimePath => string.IsNullOrWhiteSpace(CustomChimePath)
            ? DefaultChimeResource
            : CustomChimePath;
        public static string CustomChimePath { get; set; } = string.Empty;
        public const string DefaultChimeResource = "VComm.Core.Assets.Sounds.chime.wav";



        public static bool FirstRun = false;
        public static List<VPack> VPacks = new List<VPack>();

        private static VPack? _activeVPack;
        public static VPack? ActiveVPack
        {
            get
            {
                if (_activeVPack == null)
                {
                    return VPacks.FirstOrDefault();
                }
                return _activeVPack;
            }
            set
            {
                _activeVPack = value;
            }
        }
    }
}
