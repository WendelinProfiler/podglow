// Private per-user Island integration for WPods v0.4.2 (GPL-3.0).
// No raw Bluetooth packets, pairing keys or audio are written by this bridge.
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using WPods.Core;

namespace WPods.Ui;

internal sealed partial class TrayApp
{
    private IslandWindow? _islandWindow;
    private MemoryMappedFile? _islandMap;
    private MemoryMappedViewAccessor? _islandView;
    private readonly System.Windows.Forms.Timer _islandTimer = new() { Interval = 500 };
    private readonly string _islandFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AirPodsCompanion");
    private bool _islandBusy, _islandWasConnected;
    private int _islandError, _islandSequence;
    private string _islandMessage = "";
    private int _islandLeft = -1, _islandRight = -1, _islandCase = -1, _islandCharging;
    private ulong _islandBatteryTick;
    private bool _islandCaseCharging; private long _islandCaseTick; private string _islandCaseSource = "unknown"; private bool? _islandCaseLidOpen, _islandCaseBudInside;
    private bool _islandAutoMic = true;
    private bool _islandMicRouted;
    private int _islandMicKind = -1;
    private bool _islandOwnTray;



    private ulong _islandLastMicAttempt;
    private readonly int[] _islandAudio = new int[5];

    [DllImport("AirPodsAudio.dll", CharSet = CharSet.Unicode)] private static extern void AirPodsAudioInitialize(string profile);
    [DllImport("AirPodsAudio.dll")] private static extern int AirPodsAudioSnapshot([Out] int[] values);
    [DllImport("AirPodsAudio.dll")] private static extern int AirPodsAudioCommand(int command, int value);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern uint GetPrivateProfileInt(string section, string key, int fallback, string file);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern bool WritePrivateProfileString(string section, string key, string value, string file);

    private void StartIslandBridge()
    {
        Directory.CreateDirectory(_islandFolder);
        string ini = Path.Combine(_islandFolder, "settings.ini");
        _islandAutoMic = GetPrivateProfileInt("AirPods", "AutomaticMicrophone", 1, ini) != 0;
        AirPodsAudioInitialize(ini);
        _islandOwnTray = true; ResolveCompanionAddress(); StartUxBridge();
        _islandMap = MemoryMappedFile.CreateOrOpen(@"Local\IslandAirPods.v1", 688, MemoryMappedFileAccess.ReadWrite);
        _islandView = _islandMap.CreateViewAccessor(0, 688, MemoryMappedFileAccess.ReadWrite);
        _islandWindow = new IslandWindow(this);
        _islandTimer.Tick += (_, _) => PublishIslandState();
        _islandTimer.Start();
        PublishIslandState();
    }

    private sealed class IslandWindow : NativeWindow
    {
        private readonly TrayApp _owner;
        public IslandWindow(TrayApp owner) { _owner = owner; CreateHandle(new CreateParams { Caption = "Island AirPods Control", Style = unchecked((int)0x80000000), ExStyle = 0x80 }); }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x8002) { _owner.IslandCommand((int)m.WParam, (int)m.LParam); return; }
            if (m.Msg == 0x8003) { _owner.Exit(); return; }
            if (m.Msg == 0x8004) { _owner.ShowMain(); return; }
            base.WndProc(ref m);
        }
    }

    // Follow the active AirPods session; never embed a developer's Bluetooth address.
    private ulong ResolveCompanionAddress()
    {
        var devices = _connections.GetDevices();
        ulong address = _connections.Session?.Address
            ?? devices.FirstOrDefault(d => d.LinkUp)?.Address
            ?? devices.FirstOrDefault(d => d.Address == _companionAddress)?.Address
            ?? devices.FirstOrDefault()?.Address ?? 0;
        if (address != _companionAddress)
        {
            _companionAddress = address;
            _islandLeft = _islandRight = _islandCase = -1; _islandCharging = 0;
            _islandCaseTick = 0; _islandCaseCharging = false; _islandCaseSource = "unknown";
            _uxLeftSeen = _uxRightSeen = _uxCaseSeen = 0;
            _uxLeftObserved = _uxRightObserved = _uxCaseObserved = 0; _uxCached = 0;
        }
        return address;
    }
    private ulong _companionAddress;
    [DllImport("AirPodsAudio.dll", CharSet = CharSet.Unicode)] private static extern void AirPodsAudioSetTarget(string name);
    private void PublishIslandState()
    {
        if (_islandView is null) return;
        try
        {
            AapSession? session = _connections.Session;
            if (session?.Address != ResolveCompanionAddress()) session = null;
            DeviceState? state = session?.State;
            KnownDevice? device = _connections.GetDevices().FirstOrDefault(d => d.Address == ResolveCompanionAddress());
            bool connected = device?.LinkUp == true;
            uint backend = state?.Status == SessionStatus.Ready ? 2u : state?.Status == SessionStatus.NoResponse ? 4u : state is not null ? 1u : _restartPending ? 3u : 0u;
            AirPodsAudioSetTarget(device?.Name.Replace(" - Find My", "", StringComparison.Ordinal) ?? "AirPods");
            int nativeResult = AirPodsAudioSnapshot(_islandAudio);
            if (nativeResult != 0) Array.Fill(_islandAudio, -1);
            ulong now = unchecked((ulong)Environment.TickCount64);
            if (state is not null)
            {
                bool hasBattery = state.LeftBattery.Percent.HasValue || state.RightBattery.Percent.HasValue || state.CaseBattery.Percent.HasValue;
                if (hasBattery)
                {
                    _islandLeft = state.LeftBattery.Percent ?? _islandLeft;
                    _islandRight = state.RightBattery.Percent ?? _islandRight;
                    if (state.CaseBattery.Percent is { } casePercent && state.CaseBatteryReceivedTick > _islandCaseTick) { _islandCase = casePercent; _islandCaseTick = state.CaseBatteryReceivedTick; _islandCaseSource = "aap"; _islandCaseCharging = state.CaseBattery.Status == BatteryStatus.Charging; }
                    _islandCharging = (state.LeftBattery.Status == BatteryStatus.Charging ? 1 : 0) | (state.RightBattery.Status == BatteryStatus.Charging ? 2 : 0) | (_islandCaseCharging ? 4 : 0);
                    _islandBatteryTick = now;
                }
            }
            if (_nearby.TryGetCaseBattery(ResolveCompanionAddress(), out var caseBattery, out long caseSeen, out bool caseOpen, out bool budInside, out bool independent))
            {
                _islandCaseLidOpen = caseOpen; _islandCaseBudInside = budInside;
                if (caseBattery.Percent is { } percent && caseSeen > _islandCaseTick)
                {
                    _islandCase = percent; _islandCaseTick = caseSeen; _islandCaseSource = independent ? "case-ble" : "paired-ble";
                    _islandCaseCharging = caseBattery.Status == BatteryStatus.Charging; _islandCharging = (_islandCharging & ~4) | (_islandCaseCharging ? 4 : 0);
                    _islandBatteryTick = (ulong)caseSeen;
                }
            }
            if (!connected) { _islandMicRouted = false; _islandMicKind = -1; }
            if (_islandAutoMic && connected && (!_islandMicRouted || _islandAudio[4] > _islandMicKind) && _islandAudio[3] >= 0 && now - _islandLastMicAttempt > 2000)
            {
                _islandLastMicAttempt = now;
                int result = _islandAudio[3] == 1 ? 0 : AirPodsAudioCommand(16, 0);
                if (result == 0) { _islandMicRouted = true; _islandMicKind = _islandAudio[4]; }
                if (result != 0) Log.Warn("island", $"automatic microphone request failed: 0x{result:X8}");
            }
            if (!connected && _islandWasConnected && _islandAutoMic) _ = AirPodsAudioCommand(18, 0);
            _islandWasConnected = connected;
            UpdateIslandProvider(backend == 2, connected, now);
            UpdateUxState(state, connected, backend, now);
            using var stream = new MemoryStream(688);
            using var w = new BinaryWriter(stream, Encoding.Unicode, leaveOpen: true);
            w.Write(0); w.Write(0x41504931u); w.Write(now);
            w.Write(connected ? 1u : 0u); w.Write(backend); w.Write(state is null ? 0u : (uint)state.Capabilities); w.Write(_islandBusy ? 1u : 0u); w.Write(unchecked((uint)_islandError));
            w.Write(_islandLeft); w.Write(_islandRight); w.Write(_islandCase); w.Write(_islandCharging);
            w.Write(state?.LeftEar is { } le ? (int)le : -1); w.Write(state?.RightEar is { } re ? (int)re : -1);
            w.Write(state?.ListeningMode is { } lm ? (int)lm : -1);
            w.Write(state?.ConversationAwareness is { } ca ? ca ? 1 : 0 : -1);
            w.Write(state?.PersonalizedVolume is { } pv ? pv ? 1 : 0 : -1);
            w.Write(state?.EarDetection is { } ed ? ed ? 1 : 0 : -1);
            w.Write(state?.MicSide is { } ms ? (int)ms : -1);
            w.Write(_islandAutoMic ? 1 : 0); w.Write(_islandAudio[1]); w.Write(_islandAudio[0]); w.Write(_islandAudio[2]); w.Write(_islandAudio[4]);
            uint advanced = (uint)((state?.PressSpeed is { } ps ? (int)ps + 1 : 0) | ((state?.HoldDuration is { } hd ? (int)hd + 1 : 0) << 3) | ((state?.ToneVolume is { } tv ? tv + 1 : 0) << 6) | ((state?.AllowOff == true ? 1 : 0) << 14));
            w.Write(advanced); w.Write(_islandBatteryTick);
            string name = device?.Name ?? "AirPods";
            WriteFixed(w, name.Replace(" - Find My", "", StringComparison.Ordinal), 128);
            WriteFixed(w, _islandMessage, 160);
            byte[] bytes = stream.ToArray();
            if (bytes.Length != 688) throw new InvalidOperationException("Island ABI length mismatch");
            _islandView.Write(0, ++_islandSequence); Thread.MemoryBarrier();
            _islandView.WriteArray(4, bytes, 4, bytes.Length - 4); Thread.MemoryBarrier(); _islandView.Write(0, ++_islandSequence);
            PublishUxState(state, now);
            var caseReception = _nearby.CaseReception;
            File.WriteAllText(Path.Combine(_islandFolder, "status.json"), JsonSerializer.Serialize(new { tick = now, connected, backend, name, batteryLeft = _islandLeft, batteryRight = _islandRight, batteryCase = _islandCase, caseLayoutMatches = caseReception.Fixed, caseLevelMatches = caseReception.Levels, caseHeaderMatches = caseReception.Headers, casePacketsSeen = caseReception.Packets, caseKeyMatches = caseReception.Matches, caseReportsAccepted = caseReception.Reports, caseSeenUnixMs = _uxCaseSeen, leftSeenUnixMs = _uxLeftSeen, rightSeenUnixMs = _uxRightSeen, actionPhase = _uxPhase, actionCommand = _uxCommand, actionRequest = _uxRequestId, actionTick = _uxActionAt, confirmedMode = state?.ConfirmedListeningMode?.ToString(), requestedMode = _uxCommand == 3 ? _uxValue : -1, caseSource = _islandCaseSource, caseReceivedTick = _islandCaseTick, caseLidOpen = _islandCaseLidOpen, caseBudInside = _islandCaseBudInside, mode = state?.ListeningMode?.ToString(), conversation = state?.ConversationAwareness, personalized = state?.PersonalizedVolume, earDetection = state?.EarDetection, autoMicrophone = _islandAutoMic, micMuted = _islandAudio[1], volume = _islandAudio[0], error = _islandError, message = _islandMessage }));
        }
        catch (Exception e) { Log.Warn("island", $"publish: {e.GetType().Name}: {e.Message}"); }
    }

    private static void WriteFixed(BinaryWriter w, string text, int count)
    {
        char[] chars = new char[count]; text.AsSpan(0, Math.Min(text.Length, count - 1)).CopyTo(chars); w.Write(Encoding.Unicode.GetBytes(chars));
    }

    private async void IslandCommand(int command, int value)
    {
        if (_islandBusy) return;
        BeginUxCommand(command, value);
        _islandBusy = true; _islandError = 0; _islandMessage = ""; PublishIslandState();
        bool ok = false;
        try
        {
            if (command == 3 && _connections.Session is { } current && current.State.ConfirmedListeningMode is { } active && (int)active == value)
            { ok = true; _uxPhase = 2; _uxActionAt = Environment.TickCount64; _islandMessage = "Listening mode is already active"; return; }
            if (command is 1 or 2)
            {
                if (command == 1) _islandError = await Task.Run(() => WPods.Nearby.NearbyNative.wpn_bt_connect_audio(ResolveCompanionAddress()));
                else if (_connections.Session?.Address == ResolveCompanionAddress()) ok = await _connections.DisconnectAsync();
                else _islandError = await Task.Run(() => Native.wpn_bt_disconnect_audio(ResolveCompanionAddress()));
                if (command == 1 || _connections.Session is null) ok = _islandError == 0;
                await Task.Delay(700);
            }
            else if (command is 12 or 13 or 14 or 16)
            {
                _islandError = AirPodsAudioCommand(command, value); ok = _islandError == 0;
            }
            else if (command == 15)
            {
                _islandAutoMic = value != 0;
                _islandMicRouted = false;
                ok = WritePrivateProfileString("AirPods", "AutomaticMicrophone", _islandAutoMic ? "1" : "0", Path.Combine(_islandFolder, "settings.ini"));
            }
            else
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                if (_connections.Session is { } session && session.Address == ResolveCompanionAddress())
                {
                    ok = command switch
                    {
                        3 when value is >= 1 and <= 4 => await session.SetListeningModeAsync((ListeningMode)value, timeout.Token),
                        4 => await session.SetConversationAwarenessAsync(value != 0, timeout.Token),
                        5 => await session.SetPersonalizedVolumeAsync(value != 0, timeout.Token),
                        6 => await session.SetEarDetectionAsync(value != 0, timeout.Token),
                        7 when value is >= 0 and <= 2 => await session.SetMicSideAsync((MicSide)value, timeout.Token),
                        8 when value is >= 0 and <= 2 => await session.SetPressSpeedAsync((PressSpeed)value, timeout.Token),
                        9 when value is >= 0 and <= 2 => await session.SetHoldDurationAsync((HoldDuration)value, timeout.Token),
                        10 when value is >= 15 and <= 100 => await session.SetToneVolumeAsync((byte)value, timeout.Token),
                        11 => await session.SetAllowOffAsync(value != 0, timeout.Token),
                        _ => false,
                    };
                    if (ok) _islandMessage = "Sent to the AirPods";
                    if (ok && command == 6)
                    {
                        using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\AirPodsDesktop\AirPodsDesktop");
                        key.SetValue("automatic_ear_detection", value != 0 ? 1 : 0, Microsoft.Win32.RegistryValueKind.DWord);
                    }
                }
            }
        }
        catch (Exception e) when (e is OperationCanceledException or IOException or InvalidOperationException or ArgumentException)
        {
            Log.Warn("island", $"command {command} failed: {e.GetType().Name}"); _islandError = e.HResult;
        }
        finally
        {
            if (!ok && command is 1 or 2 or 3) { _uxPhase = 3; _uxActionAt = Environment.TickCount64; }
            if (!ok) { _islandError = _islandError == 0 ? 1167 : _islandError; _islandMessage = "The action could not be completed"; }
            Log.Info("island", $"command={command} value={value} sent={ok} error={_islandError}");
            _islandBusy = false; PublishIslandState();
        }
    }



    private void UpdateIslandProvider(bool ready, bool connected, ulong now) { _tray.Visible = false; }

    private void StopIslandBridge()
    {
        _islandTimer.Stop(); StopUxBridge(); _islandTimer.Dispose(); _islandView?.Dispose(); _islandMap?.Dispose(); _islandWindow?.DestroyHandle();
    }
}
