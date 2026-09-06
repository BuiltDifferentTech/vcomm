namespace VComm.Core.Objects
{
    internal class VRequest
    {
        public string[] phrases { get; set; } = Array.Empty<string>();
        public Macro macro { get; set; } = new Macro();
    }
}
