using System;
using System.Collections.Generic;
namespace SharpBrick.PoweredUp.Bluegiga.Domain.Entities
{
    public class BgBleDeviceAdvertisement
    {
        public BgBleDeviceAdvertisement()
        {
            AvailableServices = new List<Guid>();
        }
        public byte AddressType { get; set; }
        public List<Guid> AvailableServices { get; }
        public byte[] Data { get; set; }
    }
}