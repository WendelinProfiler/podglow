# Bluetooth recovery

If Windows and the Island both show no Bluetooth adapter, the problem is below the widget. Podglow temporarily uses a virtual USB/IP path for the radio; that path can fail after sleep even while the service reports Running.

Version 0.1.5 fixes a USB-speed translation bug. A Bluetooth radio with bulk endpoints must not be advertised to USB/IP as a low-speed device. The updated service reports full or high speed instead.

## Updating an affected installation

Install 0.1.6 or newer first. The service now verifies startup and can recover an empty virtual controller automatically. See [the startup notes](BLUETOOTH-STARTUP.md). If Bluetooth remains unavailable, its virtual USB/IP controller may still need a reset. Repeating a service restart alone did not clear that condition on the development machine.

Save ongoing work and keep a touchpad or wired input available. The recovery step briefly disconnects Bluetooth devices and the virtual Podglow microphone. It does not restart Windows, change pairing keys or reset physical USB controllers.

Open Windows PowerShell as administrator. Check the target without changing anything:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$env:ProgramFiles\AirPods Companion\support\Repair-Bluetooth.ps1" -CheckOnly
```

Then reconnect the virtual controller:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$env:ProgramFiles\AirPods Companion\support\Repair-Bluetooth.ps1"
```

The script requires exactly one controller with hardware ID `ROOT\USBIP_WIN2\UDE` and driver service `usbip2_ude`. It also verifies that WPodsService belongs to Podglow. It stops that service, restarts the virtual controller and starts the service again. It never resets a controller chosen only by a friendly name or a fixed instance number.

After Bluetooth returns, open the AirPods case or put the earbuds in your ears, then connect from Podglow. If the adapter remains missing, stop repeating resets and report the service log and Windows device error. Do not delete driver files or pairings.

## What was checked

The speed-policy tests cover seven input cases. On the development Lenovo with an Intel AX211, correcting the speed and resetting the virtual USB/IP controller restored the Windows Bluetooth adapter without a Windows restart. AirPods then reconnected, ANC was reported, both earbuds supplied fresh observations and the audio endpoints returned. The packaged recovery script also passed its read-only target check on that installation.

This is one-machine evidence. Other adapters, clean installations and a complete sleep/resume cycle with the patched package are not yet verified. The script is a manual recovery tool, not a claim that all standby failures are fixed.
