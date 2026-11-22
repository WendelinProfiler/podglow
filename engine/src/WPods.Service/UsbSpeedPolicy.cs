namespace WPods.Service;

internal static class UsbSpeedPolicy
{
    // This proxy serves HCI Bluetooth radios with bulk ACL endpoints. USB low
    // speed cannot carry bulk transfers. WinUSB documents 0x01 as full speed or
    // lower; passing that byte straight through as USB/IP low speed makes the
    // virtual hub reject the Bluetooth device during enumeration (Code 43).
    internal static uint ForBluetoothRadio(bool queried, byte winUsbSpeed) =>
        queried && winUsbSpeed >= 3 ? 3u : 2u;
}
