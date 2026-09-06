namespace VComm.Core.Functions.Helpers
{
    internal static class Keycodes
    {
        /// <summary>
        /// Converts a Input Key to it's Virtual Key ID.
        /// </summary>
        /// <param name="key">The key to convert</param>
        /// <returns>A Virtual Key ID</returns>
        public static uint ToVirtualKey(this Key key)
        {
            return (uint)KeyInterop.VirtualKeyFromKey(key);
        }

        public static Key? ToKeycode(this string virtualKey)
        {
            if (int.TryParse(virtualKey, out int vk) && vk > 0)
            {
                return KeyInterop.KeyFromVirtualKey(vk);
            }

            return null;
        }
    }
}
