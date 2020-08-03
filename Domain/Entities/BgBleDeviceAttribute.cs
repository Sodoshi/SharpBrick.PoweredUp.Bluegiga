namespace SharpBrick.PoweredUp.Bluegiga.Domain.Entities
{
    public class BgBleDeviceAttribute
    {
        public byte ConnectionHandle { get; set; }
        public ushort Handle { get; set; }
        public byte Type { get; set; }
        public byte[] Data { get; set; }
        public string Value { get; set; }
    }
}