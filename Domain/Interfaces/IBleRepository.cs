using System;
using System.Collections.Generic;
using System.Threading;
using SharpBrick.PoweredUp.Bluegiga.Domain.Entities;
namespace SharpBrick.PoweredUp.Bluegiga.Domain.Interfaces
{
    public interface IBleRepository
    {
        void AttachCharacteristic(CancellationToken appToken, int connectTimeoutInMilliseconds,
            Action<BgBleDeviceAttribute> attributeNotifications, BgBleDeviceCharacteristic deviceCharacteristic);
        void Close();
        IDictionary<string, string> ComPorts();
        void Connect(CancellationToken connectToken, CancellationToken appToken, int connectTimeoutInMilliseconds,
            BgBleDevice deviceToConnect);
        void FetchCharacteristics(CancellationToken appToken, int connectTimeoutInMilliseconds,
            BgBleDeviceService deviceService);
        bool Open(string comPort);
        void Scan(CancellationToken scanToken, CancellationToken appToken, int scanTimeoutInMilliseconds,
            Action<BgBleDevice> processDevices);
        void WriteValueToCharacteristic(BgBleDeviceCharacteristic deviceCharacteristic, byte[] dataToWrite);
    }
}