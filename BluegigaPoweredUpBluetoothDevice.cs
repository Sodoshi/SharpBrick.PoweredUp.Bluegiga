using System;
using System.Threading.Tasks;
using SharpBrick.PoweredUp.Bluegiga.Domain.Entities;
using SharpBrick.PoweredUp.Bluegiga.Services;
using SharpBrick.PoweredUp.Bluetooth;
namespace SharpBrick.PoweredUp.Bluegiga
{
    public class BluegigaPoweredUpBluetoothDevice : IPoweredUpBluetoothDevice
    {
        private BgBleService _bgBleService;
        private BgBleDevice _bgBleDevice;
        public BluegigaPoweredUpBluetoothDevice(BgBleService bgBleService, BgBleDevice bgBleDevice)
        {
            _bgBleService = bgBleService;
            _bgBleDevice = bgBleDevice;
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        public async Task<IPoweredUpBluetoothService> GetServiceAsync(Guid serviceId)
        {
            return _bgBleDevice.Services.TryGetValue(serviceId, out var bgBleService) ? new BluegigaPoweredUpBluetoothService(_bgBleService, bgBleService) : null;
        }
        public string Name => _bgBleDevice.Address;
        protected virtual void Dispose(bool disposing)
        {
            _bgBleService = null;
            _bgBleDevice = null;
        }
        ~BluegigaPoweredUpBluetoothDevice()
        {
            Dispose(false);
        }
    }
}