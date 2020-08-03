using System;
namespace SharpBrick.PoweredUp.Bluegiga.Domain.Entities
{
    public class BgBleDeviceCharacteristic
    {
        public byte ConnectionHandle { get; set; }
        public Guid CharacteristicId { get; set; }
        public ushort Handle { get; set; }
        public ushort ClientCharacteristicConfiguratorDescriptorHandle { get; set; }
        public byte[] uuid { get; set; }
    }
}