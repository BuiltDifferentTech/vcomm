namespace VComm.Core.Objects
{
    internal class Macro
    {
        public override string ToString()
        {
            return string.Join(" ", keycodes);
        }
        public int msToWait { get; set; } = 0; //ms to wait after each key in the keycodes are executed.
        public List<string> keycodes { get; set; } = new List<string>();
    }
}
