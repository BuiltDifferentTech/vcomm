namespace VComm.Core
{
    internal class PreInit
    {
        /// <summary>
        /// Starts the application and runs checks before main window/overlay & VoiceEngine initialization.
        /// </summary>
        public async Task Start()
        {
            Setup setup = new Setup();
            await setup.RuntimeChecks();

            await this.Log("Started!");
        }
    }
}
