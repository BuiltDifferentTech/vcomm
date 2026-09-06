namespace VComm
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        async void App_Start(object sender, StartupEventArgs e)
        {
            HandleArgs(e.Args);
            HandleErrors();

            Preloader preloader = new Preloader();
            preloader.Show();

            try
            {
                PreInit init = new PreInit();
                await init.Start();
                preloader.MarkPreInitComplete();
            }
            catch (Exception ex)
            {
                await this.Log($"Startup failed: {ex}", error: true);
                System.Windows.MessageBox.Show(
                    $"VComm could not start.\n\n{ex.Message}",
                    "VComm startup error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                preloader.Close();
                Shutdown(-1);
            }
        }

        public void HandleErrors()
        {
            AppDomain.CurrentDomain.UnhandledException +=
                new UnhandledExceptionEventHandler(UnhandledExceptions);

            DispatcherUnhandledException += UnhandledDispatcherExceptions;

            TaskScheduler.UnobservedTaskException +=
                new EventHandler<UnobservedTaskExceptionEventArgs>(UnhandledTaskExceptions);
        }

        private async void UnhandledTaskExceptions(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            await this.Log("UNHANDLED TASK EXCEPTION!", error: true);
            await this.Log(e.Exception.ToString(), error: true);
            e.SetObserved();
        }

        private async void UnhandledDispatcherExceptions(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            await this.Log("UNHANDLED DISPATCHER EXCEPTION!", error: true);
            await this.Log(e.Exception.ToString(), error: true);
        }

        private async void UnhandledExceptions(object? sender, UnhandledExceptionEventArgs e)
        {
            await this.Log("UNHANDLED EXCEPTION!", error: true);
            await this.Log($"Message: {e.ExceptionObject}", error: true);
            await this.Log($"Terminating: {e.IsTerminating}", error: true);
        }

        public void HandleArgs(string[] args)
        {
            foreach (string arg in args)
            {
                if (arg == "-console")
                {
                    Helpers.AllocConsole();
                }
            }
        }
    }

    public static class Helpers
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern int AllocConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern int FreeConsole();

        private static readonly object LogLock = new object();
        private static readonly List<string> LogCache = new List<string>();

        public static Task Log(
                this object callingObj,
                string message,
                bool error = false,
                ConsoleColor color = ConsoleColor.White,
                bool stamped = true,
                bool hiddenFromCMD = false
            )
        {
            string parentName = callingObj.GetType().Name.ToUpperInvariant();

            if (!hiddenFromCMD)
            {
                lock (LogLock)
                {
                    string stamp = string.Empty;
                    if (stamped)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        stamp = $"[{DateTime.Now:G}] VComm - {parentName}: ";
                        Console.Write(stamp);
                    }

                    if (error)
                        Console.ForegroundColor = ConsoleColor.Red;

                    else
                        Console.ForegroundColor = color;

                    string cleanMessage = stamp + (error ? "ERROR! " : string.Empty) + message;
                    LogCache.Add(cleanMessage);
                    Console.WriteLine((error ? "ERROR! " : string.Empty) + message);
                    Debug.WriteLine(cleanMessage);
                }
            }

            return Task.CompletedTask;
        }
    }
}
