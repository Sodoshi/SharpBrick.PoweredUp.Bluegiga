using System;
using System.Linq;
using System.Text;
using Bluegiga.BLE.Events.ATTClient;
using Bluegiga.BLE.Events.Connection;
using Bluegiga.BLE.Events.GAP;
using SharpBrick.PoweredUp.Bluegiga.Domain.Entities;
namespace SharpBrick.PoweredUp.Bluegiga.Domain.Mappers
{
    public static class BleMappings
    {
        private static string ByteArrayToHexString(byte[] byteArray)
        {
            var hexString = new StringBuilder(byteArray.Length * 2);
            foreach (var byteToProcess in byteArray)
            {
                hexString.AppendFormat("{0:x2}", byteToProcess);
            }
            return hexString.ToString();
        }
        private static string ByteToHexString(byte byteToProcess)
        {
            var hexString = new StringBuilder(2);
            hexString.AppendFormat("{0:x2}", byteToProcess);
            return hexString.ToString();
        }
        public static BgBleDevice MapBleDevice(ScanResponseEventArgs e)
        {
            var bluetoothAddress=new byte[8];
            var id = e.sender.Take(8).Reverse().ToArray();
            Buffer.BlockCopy(id, 0, bluetoothAddress, 8 - id.Length, id.Length);
            var data = e.data;
            var device = new BgBleDevice
            {
                BluetoothAddress = BitConverter.ToUInt64(bluetoothAddress),
                Identifier = e.sender,
                Address = ByteArrayToHexString(e.sender)
            };
            device.Advertisement.Data = data;
            device.Advertisement.AddressType = e.address_type;
            byte[] serviceIdentifier = { };
            var bytesRemaining = 0;
            var serviceIdentifieroffset = 0;
            for (var i = 0; i < data.Length; i++)
            {
                if (bytesRemaining == 0)
                {
                    bytesRemaining = data[i];
                    serviceIdentifier = new byte[data[i]];
                    serviceIdentifieroffset = i + 1;
                }
                else
                {
                    serviceIdentifier[i - serviceIdentifieroffset] = data[i];
                    bytesRemaining--;
                    if (bytesRemaining != 0)
                    {
                        continue;
                    }
                    if (serviceIdentifier[0] != 0x06 && serviceIdentifier[0] != 0x07)
                    {
                        continue;
                    }
                    var serviceId =
                        new Guid(
                            ByteArrayToHexString(serviceIdentifier.Skip(1).Take(16).Reverse().ToArray()));
                    if (!device.Advertisement.AvailableServices.Contains(serviceId))
                    {
                        device.Advertisement.AvailableServices.Add(serviceId);
                    }
                }
            }
            return device;
        }
        public static BgBleDeviceAttribute MapBleDeviceAttribute(AttributeValueEventArgs e)
        {
            return new BgBleDeviceAttribute
            {
                ConnectionHandle = e.connection,
                Handle = e.atthandle,
                Type = e.type,
                Data = e.value,
                Value = ByteArrayToHexString(e.value)
            };
        }
        public static BgBleDeviceCharacteristic MapBleDeviceCharacteristic(FindInformationFoundEventArgs e)
        {
            return new BgBleDeviceCharacteristic
            {
                uuid = e.uuid,
                CharacteristicId = e.uuid.Length < 15
                    ? Guid.Empty
                    : new Guid(
                        ByteArrayToHexString(e.uuid.Reverse().ToArray())),
                ConnectionHandle = e.connection,
                Handle = e.chrhandle
            };
        }
        public static BgBleDeviceService MapBleDeviceService(GroupFoundEventArgs e)
        {
            if (e.uuid.Length < 15)
            {
                return null;
            }
            var serviceId =
                new Guid(
                    ByteArrayToHexString(e.uuid.Reverse().ToArray()));
            return new BgBleDeviceService
            {
                ServiceId = serviceId,
                ConnectionHandle = e.connection,
                Start = e.start,
                End = e.end
            };
        }
        public static BgBleDeviceStatus MapBleDeviceStatus(StatusEventArgs e)
        {
            return new BgBleDeviceStatus
            {
                Address = ByteArrayToHexString(e.address),
                IsConnected = (e.flags & 0x05) == 0x05,
                Flags = ByteToHexString(e.flags)
            };
        }
    }
}