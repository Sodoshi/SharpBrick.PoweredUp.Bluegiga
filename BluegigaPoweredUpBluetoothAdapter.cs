using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SharpBrick.PoweredUp.Bluegiga.Domain.Interfaces;
using SharpBrick.PoweredUp.Bluegiga.Repositories;
using SharpBrick.PoweredUp.Bluegiga.Services;
using SharpBrick.PoweredUp.Bluetooth;
namespace SharpBrick.PoweredUp.Bluegiga
{
    public class BluegigaPoweredUpBluetoothAdapter : IPoweredUpBluetoothAdapter
    {
        private readonly IBleRepository _bleRepository;
        private readonly ILogger<BluegigaPoweredUpBluetoothAdapter> _logger;
        private BgBleService _bleService;
        private CancellationTokenSource _tokenSource;
        public BluegigaPoweredUpBluetoothAdapter(ILogger<BluegigaPoweredUpBluetoothAdapter> logger = default)
        {
            _logger = logger;
            _bleRepository = new BgBleRepository(logger);
        }
        public void Discover(Action<PoweredUpBluetoothDeviceInfo> discoveryHandler,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("BluegigaPoweredUpBluetoothAdapter: Connecting to BgBleService");

            _tokenSource = new CancellationTokenSource();
            var applicationCancellationTokenSource =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,
                    _tokenSource.Token);

            var discoveryCompleteTokenSource = new CancellationTokenSource();

            _bleService = new BgBleService(applicationCancellationTokenSource, _bleRepository, _logger);
            _bleService.Process(discoveryCompleteTokenSource).ConfigureAwait(false);

            while (!discoveryCompleteTokenSource.IsCancellationRequested)
            {
                WaitHandle.WaitAny(new[] {cancellationToken.WaitHandle}, 500);
            }

            _logger.LogInformation("BluegigaPoweredUpBluetoothAdapter: BgBleService Processing Complete");
            foreach (var bgBleDevice in _bleService.Devices.Values)
            {
                discoveryHandler?.Invoke(new PoweredUpBluetoothDeviceInfo
                {
                    ManufacturerData = bgBleDevice.Advertisement.Data,
                    BluetoothAddress = bgBleDevice.BluetoothAddress
                });
            }
        }
        public async Task<IPoweredUpBluetoothDevice> GetDeviceAsync(ulong bluetoothAddress)
        {
            _logger?.LogInformation($"GetDeviceAsync {bluetoothAddress}");
            var bgBleDevice = _bleService?.GetDevice(bluetoothAddress);
            return bgBleDevice != null ? new BluegigaPoweredUpBluetoothDevice(_bleService, bgBleDevice) : null;
        }
    }
}