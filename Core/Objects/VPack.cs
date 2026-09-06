namespace VComm.Core.Objects
{
    internal class VPack
    {
        public override string ToString()
        {
            return name;
        }
        public string name { get; set; } = "Ready Or Not";
        public string author { get; set; } = "Devlin";
        public List<VRequest> vRequests { get; set; } = new List<VRequest>();
    }
}
