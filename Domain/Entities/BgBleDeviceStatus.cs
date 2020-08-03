namespace SharpBrick.PoweredUp.Bluegiga.Domain.Entities
{
    public class BgBleDeviceStatus
    {
        public string Address { get; set; }
        public bool IsConnected { get; set; }
        public string Flags { get; set; }
    }
}