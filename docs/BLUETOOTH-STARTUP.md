# Bluetooth startup and GlideMouse

Podglow temporarily runs the physical USB Bluetooth radio through WinUSB and exposes a virtual radio to Windows. GlideMouse can observe the physical USB traffic through its scoped, signed USBPcap dependency. These are separate parts of the same input path.

Before this patch, the Intel chip could enter the proxy before its firmware was initialized. A driver switch could also leave USBPcap on the root hub but remove its child filter from the Bluetooth device. In that state the mouse pointer could connect while GlideMouse received no finger data.

## Startup order

1. On Intel USB adapters, let the installed native driver initialize the physical radio once per Windows boot.
2. Hand the adapter to WinUSB. If its parent already has the scoped USBPcap filter, re-enumerate only the adapter's USB port so the child filter is created again.
3. Attach the virtual radio. Wait for Windows to start it and for a fresh HCI-version response before publishing Ready or attaching the virtual microphone.
4. GlideMouse uses the mouse node on that virtual radio. Its matching installer avoids the temporary native-radio twin and resolves capture to the physical adapter.

The firmware marker is volatile: it survives a service restart and disappears at the next Windows boot. The code never clears Bluetooth pairing keys. It does not restart Windows on its own.

## Updating both apps

Update Podglow to 0.1.6 alpha and GlideMouse to 0.1.1 alpha or newer. Follow GlideMouse's release notes for its manual alpha update path. Save your work and restart Windows after driver setup; a legacy Apple filter cannot always be reloaded in the current Windows session.

An older manually installed prototype may have the Apple filter on both native and virtual mouse nodes. Do not copy instance IDs from someone else's machine. Use the generalized installer and its installation journal rather than deleting registry entries by hand.

## Recovery and limits

When a virtual radio fails, the service first detaches its own imports. It will only rebuild the dedicated USB/IP controller after a successful query proves the import list is empty. Active imports from another app or an unreadable result prevent the reset. Repeated takeover failures still trigger the existing native-driver fallback and are logged; that fallback does not mean advanced AirPods or mouse features are ready.

For manual recovery, see [Bluetooth recovery](BLUETOOTH-RECOVERY.md). Keep logs local and remove device addresses before sharing them.

The runtime sequence passed a full restart on one Intel Bluetooth development system, with both filters and live finger reports checked. Fresh installation, uninstall and operation on a second PC remain untested. A successful service status by itself is not a hardware test.
