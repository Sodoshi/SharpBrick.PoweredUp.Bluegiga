using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Management;
using System.Threading;
using Bluegiga;
using Bluegiga.BLE.Events.ATTClient;
using Bluegiga.BLE.Events.Connection;
using Bluegiga.BLE.Events.GAP;
using Microsoft.Extensions.Logging;
using SharpBrick.PoweredUp.Bluegiga.Domain.Entities;
using SharpBrick.PoweredUp.Bluegiga.Domain.Interfaces;
using SharpBrick.PoweredUp.Bluegiga.Domain.Mappers;
namespace SharpBrick.PoweredUp.Bluegiga.Repositories
{
    public class BgBleRepository : IBleRepository
    {
        private static readonly object ThreadLock = new object();
        private readonly BGLib _bglib;
        private readonly ILogger<BluegigaPoweredUpBluetoothAdapter> _logger;
        private readonly SerialPort _serialPort;
        private Action<BgBleDeviceAttribute> _attributeNotifications;
        private bool _busy;
        private CancellationTokenSource _currentAction;
        private BgBleDevice _device;
        private Action<BgBleDevice> _deviceNotifications;
        private BgBleDeviceService _deviceService;
        private BgBleDeviceCharacteristic _lastCharacteristic;
        public BgBleRepository(ILogger<BluegigaPoweredUpBluetoothAdapter> logger)
        {
            _logger = logger;
            _bglib = new BGLib();
            _bglib.BLEEventConnectionStatus += OnDeviceConnected;
            _bglib.BLEEventGAPScanResponse += OnDeviceFound;
            _bglib.BLEEventATTClientGroupFound += OnServiceFound;
            _bglib.BLEEventATTClientFindInformationFound += OnCharacteristicFound;
            _bglib.BLEEventATTClientProcedureCompleted += OnClientProcedureCompletedEvent;
            _bglib.BLEEventATTClientAttributeValue += OnClientAttributeValueEvent;
            _serialPort = new SerialPort
            {
                Handshake = Handshake.RequestToSend,
                BaudRate = 115200,
                DataBits = 8,
                StopBits = StopBits.One,
                Parity = Parity.None
            };
            _serialPort.DataReceived += OnDataReceived;
        }
        public void AttachCharacteristic(CancellationToken appToken, int connectTimeoutInMilliseconds,
            Action<BgBleDeviceAttribute> attributeNotifications, BgBleDeviceCharacteristic deviceCharacteristic)
        {
            if(deviceCharacteristic.ClientCharacteristicConfiguratorDescriptorHandle == 0)
            {
                return;
            }
            try
            {
                var lockTaken = false;
                Monitor.TryEnter(ThreadLock, 150, ref lockTaken);
                if (lockTaken)
                {
                    if (_busy)
                    {
                        return;
                    }
                    _busy = true;
                    Monitor.Exit(ThreadLock);
                }
                _attributeNotifications = attributeNotifications;
                _currentAction = new CancellationTokenSource();
                var attachCharacteristicTokenSource =
                    CancellationTokenSource.CreateLinkedTokenSource(appToken,
                        _currentAction.Token);
                _bglib.SendCommand(_serialPort,
                    _bglib.BLECommandATTClientAttributeWrite(deviceCharacteristic.ConnectionHandle,
                        deviceCharacteristic.ClientCharacteristicConfiguratorDescriptorHandle, new byte[] {0x01, 0x00}));
                WaitHandle.WaitAny(new[] {attachCharacteristicTokenSource.Token.WaitHandle},
                    connectTimeoutInMilliseconds);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception,
                    $"ControlPlus Repository: Unable to attach characteristic {deviceCharacteristic.CharacteristicId}");
            }

            _busy = false;
        }
        public void WriteValueToCharacteristic(BgBleDeviceCharacteristic deviceCharacteristic, byte[] dataToWrite)
        {
            _bglib.SendCommand(_serialPort,
                _bglib.BLECommandATTClientAttributeWrite(deviceCharacteristic.ConnectionHandle,
                    deviceCharacteristic.Handle, dataToWrite));
        }
        public void Close()
        {
            try
            {
                _serialPort.Close();
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception,
                    $"ControlPlus Repository: Unable to close Com port {_serialPort.PortName}");
            }
        }
        public IDictionary<string, string> ComPorts()
        {
            var comPorts = new Dictionary<string, string>();
            try
            {
                var managementObjectSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_SerialPort");
                foreach (var queryObj in managementObjectSearcher.Get())
                {
                    var devideId = queryObj["DeviceID"]?.ToString() ?? string.Empty;
                    var caption = queryObj["Caption"]?.ToString() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(devideId) || comPorts.ContainsKey(devideId))
                    {
                        continue;
                    }
                    if (caption.Contains("Bluegiga"))
                    {
                        comPorts.Add(devideId, caption);
                    }
                }
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "ControlPlus Repository: Unable to read Com ports");
            }

            return comPorts;
        }
        public void Connect(CancellationToken connectToken, CancellationToken appToken,
            int connectTimeoutInMilliseconds, BgBleDevice deviceToConnect)
        {
            try
            {
                var lockTaken = false;
                Monitor.TryEnter(ThreadLock, 150, ref lockTaken);
                if (lockTaken)
                {
                    if (_busy)
                    {
                        return;
                    }
                    _busy = true;
                    Monitor.Exit(ThreadLock);
                }
                _device = deviceToConnect;
                _currentAction = new CancellationTokenSource();
                var connectToDevicesTokenSource =
                    CancellationTokenSource.CreateLinkedTokenSource(connectToken,
                        _currentAction.Token);
                _bglib.SendCommand(_serialPort,
                    _bglib.BLECommandGAPConnectDirect(deviceToConnect.Identifier,
                        deviceToConnect.Advertisement.AddressType, 0x20, 0x30, 0x100, 0));
                WaitHandle.WaitAny(new[] {connectToDevicesTokenSource.Token.WaitHandle}, connectTimeoutInMilliseconds);
                // cleanup takes 125ms
                _bglib.SendCommand(_serialPort, _bglib.BLECommandGAPEndProcedure());
                WaitHandle.WaitAny(new[] {appToken.WaitHandle}, 130);
                _logger.LogInformation(
                    $"ControlPlus Repository: Connected to device {deviceToConnect.Address} with {_device.Services.Count} services");
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception,
                    $"ControlPlus Repository: Unable to connect to device {deviceToConnect.Address}");
            }
            _deviceService = null;
            _device = null;
            _busy = false;
        }
        public void FetchCharacteristics(CancellationToken appToken, int connectTimeoutInMilliseconds,
            BgBleDeviceService deviceService)
        {
            deviceService.Characteristics.Clear();
            try
            {
                var lockTaken = false;
                Monitor.TryEnter(ThreadLock, 150, ref lockTaken);
                if (lockTaken)
                {
                    if (_busy)
                    {
                        return;
                    }
                    _busy = true;
                    Monitor.Exit(ThreadLock);
                }
                _deviceService = deviceService;
                _currentAction = new CancellationTokenSource();
                var populateClientInformationTokenSource =
                    CancellationTokenSource.CreateLinkedTokenSource(appToken,
                        _currentAction.Token);
                _bglib.SendCommand(_serialPort,
                    _bglib.BLECommandATTClientFindInformation(deviceService.ConnectionHandle, deviceService.Start,
                        deviceService.End));
                WaitHandle.WaitAny(new[] {populateClientInformationTokenSource.Token.WaitHandle},
                    connectTimeoutInMilliseconds);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception,
                    $"ControlPlus Repository: Unable to populate Client Information {deviceService.ServiceId}");
            }
            _lastCharacteristic = null;
            _deviceService = null;
            _device = null;
            _busy = false;
        }
        public bool Open(string comPort)
        {
            try
            {
                _serialPort.PortName = comPort;
                _serialPort.Open();
                return true;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, $"ControlPlus Repository: Unable to open Com port {comPort}");
                return false;
            }
        }
        public void Scan(CancellationToken scanToken, CancellationToken appToken, int scanTimeoutInMilliseconds,
            Action<BgBleDevice> processDevices)
        {
            try
            {
                var lockTaken = false;
                Monitor.TryEnter(ThreadLock, 150, ref lockTaken);
                if (lockTaken)
                {
                    if (_busy)
                    {
                        return;
                    }
                    _busy = true;
                    Monitor.Exit(ThreadLock);
                }
                _deviceNotifications = processDevices;
                _bglib.SendCommand(_serialPort, _bglib.BLECommandGAPSetScanParameters(0xC8, 0xC8, 1));
                _bglib.SendCommand(_serialPort, _bglib.BLECommandGAPDiscover(1));
                WaitHandle.WaitAny(new[] {scanToken.WaitHandle}, scanTimeoutInMilliseconds);
                // cleanup takes 125ms
                _bglib.SendCommand(_serialPort, _bglib.BLECommandGAPEndProcedure());
                _deviceNotifications = null;
                WaitHandle.WaitAny(new[] {appToken.WaitHandle}, 130);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception,
                    $"ControlPlus Repository: Unable to scan via Com port {_serialPort.PortName}");
            }
            _busy = false;
        }
        private void OnCharacteristicFound(object sender, FindInformationFoundEventArgs e)
        {
            if (_deviceService == null)
            {
                return;
            }
            var characteristic = BleMappings.MapBleDeviceCharacteristic(e);
            if (characteristic == null)
            {
                return;
            }
            if (characteristic.CharacteristicId != Guid.Empty)
            {
                _lastCharacteristic = characteristic;
                if (!_deviceService.Characteristics.ContainsKey(characteristic.CharacteristicId))
                {
                    _deviceService.Characteristics.Add(characteristic.CharacteristicId, characteristic);
                }
            }
            else if (_lastCharacteristic?.Handle > 0 && _lastCharacteristic?.ClientCharacteristicConfiguratorDescriptorHandle==0)
            {
                _lastCharacteristic.ClientCharacteristicConfiguratorDescriptorHandle = characteristic.Handle;
            }
        }
        private void OnClientAttributeValueEvent(object sender, AttributeValueEventArgs e)
        {
            var attribute = BleMappings.MapBleDeviceAttribute(e);
            if (attribute == null)
            {
                return;
            }
            _attributeNotifications?.Invoke(attribute);
        }
        private void OnClientProcedureCompletedEvent(object sender, ProcedureCompletedEventArgs e)
        {
            _logger.LogInformation("OnClientProcedureCompletedEvent");
            _currentAction?.Cancel();
        }
        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            var sp = (SerialPort) sender;
            var inData = new byte[sp.BytesToRead];
            sp.Read(inData, 0, sp.BytesToRead);
            foreach (var t in inData)
            {
                _bglib.Parse(t);
            }
        }
        private void OnDeviceConnected(object sender, StatusEventArgs e)
        {
            var deviceStatus = BleMappings.MapBleDeviceStatus(e);
            if (_device == null || deviceStatus == null || _device.Address != deviceStatus.Address)
            {
                return;
            }
            if (deviceStatus.IsConnected) // Connected
            {
                // Service Discovery
                _bglib.SendCommand(_serialPort,
                    _bglib.BLECommandATTClientReadByGroupType(e.connection, 0x0001, 0xFFFF,
                        new byte[] {0x00, 0x28})); // "service" UUID is 0x2800 (little-endian for UUID uint8array)
            }
            else
            {
                _logger.LogInformation(
                    $"ControlPlus Repository: Connection changed: {deviceStatus.Address} {deviceStatus.Flags}");
            }
        }
        private void OnDeviceFound(object sender, ScanResponseEventArgs e)
        {
            _deviceNotifications?.Invoke(BleMappings.MapBleDevice(e));
        }
        private void OnServiceFound(object sender, GroupFoundEventArgs e)
        {
            if (_device == null || e.end == 0 || e.uuid.Length < 15)
            {
                return;
            }
            var service = BleMappings.MapBleDeviceService(e);
            if (service == null)
            {
                return;
            }
            if (!_device.Services.ContainsKey(service.ServiceId))
            {
                _device.Services.Add(service.ServiceId, service);
            }
        }
    }
}