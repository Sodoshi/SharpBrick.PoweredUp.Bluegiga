using System;
using System.Collections.Generic;
namespace SharpBrick.PoweredUp.Bluegiga.Domain.Entities
{
    public class BgBleDevice
    {
        public BgBleDevice()
        {
            Advertisement = new BgBleDeviceAdvertisement();
            Services = new Dictionary<Guid, BgBleDeviceService>();
        }
        public BgBleDeviceAdvertisement Advertisement { get; }
        public Dictionary<Guid, BgBleDeviceService> Services { get; }
        public ulong BluetoothAddress { get; set; }
        public string Address { get; set; }
        public byte[] Identifier { get; set; }
    }
}