namespace VComm.Core.Functions.Helpers
{
    internal static class ProgressBarExtension
    {
        // Animation duration - How long it takes to get from a -> b
        private static TimeSpan duration = TimeSpan.FromSeconds(0.08);

        /// <summary>
        /// Sets the percentage of a progress bar gradually.
        /// </summary>
        /// <param name="progressBar">The ProgressBar to set the value of</param>
        /// <param name="percentage">The value to set the ProgressBar to</param>
        public static void SetPercent(this System.Windows.Controls.ProgressBar progressBar, double percentage)
        {
            DoubleAnimation animation = new DoubleAnimation(percentage, duration);
            progressBar.BeginAnimation(System.Windows.Controls.ProgressBar.ValueProperty, animation);
        }
    }
}
