namespace Netcad.NDU.GUA.Elements
{
    public class InstalledItem
    {
        public string Id { get; set; }
        public Category Category { get; set; }
        public int Version { get; set; }

        public ItemStatus Status { get; set; }
    }
    public enum ItemStatus
    {
        Active,
        Inactive
    }
}