using System;
using System.Collections.Generic;
namespace SharpBrick.PoweredUp.Bluegiga.Domain.Entities
{
    public class BgBleDeviceService
    {
        public BgBleDeviceService()
        {
            Characteristics = new Dictionary<Guid, BgBleDeviceCharacteristic>();
        }
        public byte ConnectionHandle { get; set; }
        public Guid ServiceId { get; set; }
        public ushort Start { get; set; }
        public ushort End { get; set; }
        public Dictionary<Guid, BgBleDeviceCharacteristic> Characteristics { get; }
    }
}