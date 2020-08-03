using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SharpBrick.PoweredUp.Bluegiga.Domain.Entities;
using SharpBrick.PoweredUp.Bluegiga.Domain.Interfaces;
namespace SharpBrick.PoweredUp.Bluegiga.Services
{
    public class BgBleService
    {
        private const int DeviceTimeout = 5000;
        private const int ScanTimeout = 30000;
        public Func<byte[], Task> NotificationHandler;
        private static readonly Guid LegoHubServiceId = new Guid("00001623-1212-EFDE-1623-785FEABCD123");
        private static readonly Guid LegoHubCharacteristicId = new Guid("00001624-1212-EFDE-1623-785FEABCD123");
        private readonly CancellationTokenSource _applicationCancellationTokenSource;
        private readonly IBleRepository _bleRepository;
        public readonly Dictionary<ulong, BgBleDevice> Devices;
        private readonly ILogger<BluegigaPoweredUpBluetoothAdapter> _logger;
        private CancellationTokenSource _currentActionTokenSource;
        public BgBleService(CancellationTokenSource applicationCancellationTokenSource,
            IBleRepository bleRepository, ILogger<BluegigaPoweredUpBluetoothAdapter> logger)
        {
            _applicationCancellationTokenSource = applicationCancellationTokenSource;
            _bleRepository = bleRepository;
            _logger = logger;
            Devices = new Dictionary<ulong, BgBleDevice>();
        }
        public Task Process(CancellationTokenSource discoveryCompleteTokenSource)
        {
            return Task.Run(() =>ProcessBle(discoveryCompleteTokenSource)
            , _applicationCancellationTokenSource.Token);
        }
        public BgBleDevice GetDevice(ulong bluetoothAddress)
        {
            return Devices.TryGetValue(bluetoothAddress, out var bgBleDevice) ? bgBleDevice : null;
        }
        private void ProcessBle(CancellationTokenSource discoveryCompleteTokenSource)
        {

            _logger.LogInformation($"BgBle Service: Processing...");

            // Connect To Bluegiga Hardware
            var comPort = ProcessBgBle_ConnectToHardware();
            if (string.IsNullOrWhiteSpace(comPort))
            {
                return;
            }
            // Scan for Lego Hubs
            ProcessBgBle_ScanForDevices();

            // Connect to Lego hubs
            ProcessBgBle_ConnectToDevices();

            // Get Lego Service
            var service = Devices.Values.FirstOrDefault()?.Services?.FirstOrDefault(x => x.Key == LegoHubServiceId)
                .Value;
            ProcessBgBle_FetchCharacteristics(service);

            // Get Lego Characteristic
            var characteristic = service?.Characteristics.FirstOrDefault(x => x.Key == LegoHubCharacteristicId).Value;
            ProcessBgBle_AttachCharacteristic(characteristic);

            discoveryCompleteTokenSource.Cancel();

            while (!_applicationCancellationTokenSource.IsCancellationRequested)
            {
                WaitHandle.WaitAny(new[] {_applicationCancellationTokenSource.Token.WaitHandle}, 1);
            }

            // Clean up
            _logger.LogInformation($"BgBle Service: Closing Com Port {comPort}");
            _bleRepository.Close();
        }
        private void ProcessBgBle_AttachCharacteristic(BgBleDeviceCharacteristic deviceCharacteristic)
        {
            if (deviceCharacteristic == null)
            {
                return;
            }
            _currentActionTokenSource = new CancellationTokenSource();
            var attachCharacteristicTokenSource =
                CancellationTokenSource.CreateLinkedTokenSource(_applicationCancellationTokenSource.Token,
                    _currentActionTokenSource.Token);
            _logger.LogInformation($"BgBle Service: Attaching characteristic {deviceCharacteristic.CharacteristicId}");
            _bleRepository.AttachCharacteristic(attachCharacteristicTokenSource.Token,
                DeviceTimeout, ProcessBgBle_Callback_Attribute, deviceCharacteristic);
        }
        private void ProcessBgBle_Callback_Attribute(BgBleDeviceAttribute attribute)
        {
            _logger.LogInformation("OnClientAttributeValueEvent: " +
                                   $"connection={attribute.ConnectionHandle}, " +
                                   $"atthandle={attribute.Handle}, " +
                                   $"type={attribute.Type}, " +
                                   $"value={attribute.Value}");
            NotificationHandler?.Invoke(attribute.Data);
        }
        private void ProcessBgBle_Callback_DeviceFound(BgBleDevice device)
        {
            if (!device.Advertisement.AvailableServices.Contains(LegoHubServiceId))
            {
                return;
            }
            if (Devices.ContainsKey(device.BluetoothAddress))
            {
                return;
            }
            _logger.LogInformation($"BgBle Service: Found Device containing Lego Hub service id: {device.Address}");
            Devices.Add(device.BluetoothAddress, device);
            _currentActionTokenSource.Cancel();
        }
        private void ProcessBgBle_ConnectToDevices()
        {
            _currentActionTokenSource = new CancellationTokenSource();
            var connectionTokenSource =
                CancellationTokenSource.CreateLinkedTokenSource(_applicationCancellationTokenSource.Token,
                    _currentActionTokenSource.Token);
            _logger.LogInformation("BgBle Service: Connecting to devices");
            foreach (var device in Devices.Values)
            {
                _logger.LogInformation($"BgBle Service: Connecting to device {device.Address}");
                _bleRepository.Connect(connectionTokenSource.Token, _applicationCancellationTokenSource.Token,
                    DeviceTimeout, device);
            }
            _logger.LogInformation("BgBle Service: Connections complete");
        }
        private string ProcessBgBle_ConnectToHardware()
        {
            var comPorts = _bleRepository.ComPorts();
            if (comPorts.Count == 0)
            {
                _logger.LogCritical("BgBle Service: No BLE hardware found");
                _applicationCancellationTokenSource.Cancel();
                return string.Empty;
            }
            var comPort = comPorts.Keys.FirstOrDefault();
            _logger.LogInformation($"BgBle Service: Opening Com Port {comPort}");
            if (!_bleRepository.Open(comPort))
            {
                _logger.LogCritical($"BgBle Service: Unable to open connection to {comPort}");
                _applicationCancellationTokenSource.Cancel();
                return string.Empty;
            }
            return comPort;
        }
        private void ProcessBgBle_FetchCharacteristics(BgBleDeviceService deviceClientGroup)
        {
            if (deviceClientGroup == null)
            {
                return;
            }
            _currentActionTokenSource = new CancellationTokenSource();
            var fetchCharacteristicsTokenSource =
                CancellationTokenSource.CreateLinkedTokenSource(_applicationCancellationTokenSource.Token,
                    _currentActionTokenSource.Token);
            _logger.LogInformation($"BgBle Service: Fetching characteristics for {deviceClientGroup.ServiceId}");
            _bleRepository.FetchCharacteristics(fetchCharacteristicsTokenSource.Token,
                DeviceTimeout, deviceClientGroup);
        }
        private void ProcessBgBle_ScanForDevices()
        {
            _currentActionTokenSource = new CancellationTokenSource();
            var scanningTokenSource =
                CancellationTokenSource.CreateLinkedTokenSource(_applicationCancellationTokenSource.Token,
                    _currentActionTokenSource.Token);
            _logger.LogInformation("BgBle Service: Scanning for devices");
            _bleRepository.Scan(scanningTokenSource.Token, _applicationCancellationTokenSource.Token,
                ScanTimeout,
                ProcessBgBle_Callback_DeviceFound);
            _logger.LogInformation("BgBle Service: Scanning complete");
        }
        public void WriteValueToCharacteristic(BgBleDeviceCharacteristic deviceCharacteristic, byte[] dataToWrite)
        {
            _bleRepository.WriteValueToCharacteristic(deviceCharacteristic, dataToWrite);
        }
    }
}