namespace VComm.Core.Objects
{
    internal class Settings
    {
        public bool PTTEnabled { get; set; } = false;
        public bool VoiceGateEnabled { get; set; } = false;
        public string PTTKey { get; set; } = Key.Capital.ToString();

    }
}