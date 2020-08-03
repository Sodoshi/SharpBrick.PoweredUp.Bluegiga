using System;
using System.Linq;
using System.Threading.Tasks;
using SharpBrick.PoweredUp.Bluegiga.Domain.Entities;
using SharpBrick.PoweredUp.Bluegiga.Services;
using SharpBrick.PoweredUp.Bluetooth;
namespace SharpBrick.PoweredUp.Bluegiga
{
    public class BluegigaPoweredUpBluetoothService : IPoweredUpBluetoothService
    {
        private BgBleService _bgBleService;
        private BgBleDeviceService _bgBleDeviceService;
        public Guid Uuid => _bgBleDeviceService.ServiceId;

        public BluegigaPoweredUpBluetoothService(BgBleService bgBleService, BgBleDeviceService bgBleDeviceService)
        {
            _bgBleService = bgBleService;
            _bgBleDeviceService = bgBleDeviceService;
        }

        ~BluegigaPoweredUpBluetoothService() => Dispose(false);
        public void Dispose() { Dispose(true); GC.SuppressFinalize(this); }
        protected virtual void Dispose(bool disposing)
        {
            _bgBleService = null;
            _bgBleDeviceService = null;
        }

        public async Task<IPoweredUpBluetoothCharacteristic> GetCharacteristicAsync(Guid guid)
        {
            return new BluegigaPoweredUpBluetoothCharacteristic(_bgBleService, _bgBleDeviceService.Characteristics.FirstOrDefault().Value);
        }
    }

}