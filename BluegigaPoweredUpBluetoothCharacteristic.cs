using System;
using System.Threading.Tasks;
using SharpBrick.PoweredUp.Bluegiga.Domain.Entities;
using SharpBrick.PoweredUp.Bluegiga.Services;
using SharpBrick.PoweredUp.Bluetooth;
namespace SharpBrick.PoweredUp.Bluegiga
{
    public class BluegigaPoweredUpBluetoothCharacteristic : IPoweredUpBluetoothCharacteristic
    {
        private readonly BgBleDeviceCharacteristic _bgBleDeviceCharacteristic;
        private readonly BgBleService _bgBleService;
       
        public BluegigaPoweredUpBluetoothCharacteristic(BgBleService bgBleService,
            BgBleDeviceCharacteristic bgBleDeviceCharacteristic)
        {
            _bgBleService = bgBleService;
            _bgBleDeviceCharacteristic = bgBleDeviceCharacteristic;
        }
        public async Task<bool> NotifyValueChangeAsync(Func<byte[], Task> notificationHandler)
        {
            _bgBleService.NotificationHandler = notificationHandler;
            return true;
        }
        public Guid Uuid => _bgBleDeviceCharacteristic.CharacteristicId;
        public async Task<bool> WriteValueAsync(byte[] data)
        {
            _bgBleService.WriteValueToCharacteristic(_bgBleDeviceCharacteristic, data);
            return true;
        }
    }
}