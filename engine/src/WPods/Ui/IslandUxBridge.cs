using System.IO.MemoryMappedFiles;
using System.Text.Json;
using WPods.Core;

namespace WPods.Ui;

internal sealed partial class TrayApp
{
    private MemoryMappedFile? _uxMap;
    private MemoryMappedViewAccessor? _uxView;
    private int _uxSequence, _uxRequestId, _uxCommand, _uxValue, _uxPhase;
    private long _uxActionAt, _uxBeginAt, _uxLeftObserved, _uxRightObserved, _uxCaseObserved;
    private long _uxLeftSeen, _uxRightSeen, _uxCaseSeen, _uxCacheSavedAt;
    private uint _uxCached;
    private string _uxCacheWritten = "";

    private sealed class UxBatteryCache
    {
        public ulong Address { get; set; }
        public int Left { get; set; } = -1;
        public int Right { get; set; } = -1;
        public int Case { get; set; } = -1;
        public int Charging { get; set; }
        public long LeftSeen { get; set; }
        public long RightSeen { get; set; }
        public long CaseSeen { get; set; }
    }

    private void StartUxBridge()
    {
        string file = Path.Combine(_islandFolder, "battery-cache.json");
        try
        {
            UxBatteryCache? cache = File.Exists(file) ? JsonSerializer.Deserialize<UxBatteryCache>(File.ReadAllText(file)) : null;
            if (cache is not null && cache.Address != _companionAddress) cache = null;
            if (cache is not null)
            {
                long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                int Valid(int n) => n is >= 0 and <= 100 ? n : -1;
                long Seen(long n) => n > 0 && n <= now + 60000 ? Math.Min(n, now) : 0;
                _islandLeft = Valid(cache.Left); _islandRight = Valid(cache.Right); _islandCase = Valid(cache.Case);
                _uxLeftSeen = Seen(cache.LeftSeen); _uxRightSeen = Seen(cache.RightSeen); _uxCaseSeen = Seen(cache.CaseSeen);
                _islandCharging = cache.Charging & 7; _islandCaseCharging = (_islandCharging & 4) != 0;
                _uxCached = (uint)((_islandLeft >= 0 ? 1 : 0) | (_islandRight >= 0 ? 2 : 0) | (_islandCase >= 0 ? 4 : 0));
                if (_islandCase >= 0) _islandCaseSource = "cache";
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        { Log.Warn("island", "battery cache unavailable: " + e.GetType().Name); }
        _uxMap = MemoryMappedFile.CreateOrOpen(@"Local\IslandAirPods.Ux.v1", 80, MemoryMappedFileAccess.ReadWrite);
        _uxView = _uxMap.CreateViewAccessor(0, 80, MemoryMappedFileAccess.ReadWrite);
    }

    private void BeginUxCommand(int command, int value)
    {
        if (command is not (1 or 2 or 3)) return;
        _uxCommand = command; _uxValue = value; _uxPhase = 1; _uxRequestId++;
        _uxBeginAt = _uxActionAt = Environment.TickCount64;
    }

    private void UpdateUxState(DeviceState? state, bool connected, uint backend, ulong tick)
    {
        long now = (long)tick, utc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        void UpdateSeen(long observed, ref long previous, ref long saved, uint mask)
        {
            if (observed <= 0 || observed == previous || observed > now) return;
            previous = observed; saved = utc - (now - observed); _uxCached &= ~mask;
        }
        if (state is not null)
        {
            UpdateSeen(state.LeftBatteryReceivedTick, ref _uxLeftObserved, ref _uxLeftSeen, 1);
            UpdateSeen(state.RightBatteryReceivedTick, ref _uxRightObserved, ref _uxRightSeen, 2);
        }
        UpdateSeen(_islandCaseTick, ref _uxCaseObserved, ref _uxCaseSeen, 4);
        if (_uxPhase == 1)
        {
            bool confirmed = _uxCommand switch
            {
                1 => connected && backend == 2,
                2 => !connected,
                3 => state?.ConfirmedListeningMode is { } mode && (int)mode == _uxValue && state.ListeningModeReportedTick >= _uxBeginAt,
                _ => false,
            };
            if (confirmed) { _uxPhase = 2; _uxActionAt = now; }
            else if (now - _uxBeginAt > (_uxCommand == 3 ? 8000 : 20000))
            {
                _uxPhase = 3; _uxActionAt = now; _islandError = 1460;
                _islandMessage = _uxCommand == 3 ? "The AirPods did not confirm the listening mode. Please try again." : "Connection failed. Open the case and try again.";
            }
        }
    }

    private void PublishUxState(DeviceState? state, ulong tick)
    {
        if (_uxView is null) return;
        using var memory = new MemoryStream(80); using var w = new BinaryWriter(memory);
        w.Write(0); w.Write(0x41505531u); w.Write(tick);
        w.Write((ulong)Math.Max(0, _uxCaseSeen)); w.Write((ulong)Math.Max(0, _uxLeftSeen)); w.Write((ulong)Math.Max(0, _uxRightSeen));
        w.Write(_uxCommand == 3 ? _uxValue : -1); w.Write(state?.ConfirmedListeningMode is { } confirmed ? (int)confirmed : -1);
        w.Write(_uxPhase); w.Write(_uxRequestId); w.Write((ulong)Math.Max(0, _uxActionAt)); w.Write(_uxCommand); w.Write(_uxValue);
        w.Write(1u); w.Write(_uxCached | (_islandCaseSource == "case-ble" ? 8u : 0u) | (_islandCaseLidOpen == true ? 16u : 0u));
        byte[] bytes = memory.ToArray(); if (bytes.Length != 80) throw new InvalidOperationException("AirPods UX ABI mismatch");
        _uxView.Write(0, ++_uxSequence); Thread.MemoryBarrier(); _uxView.WriteArray(4, bytes, 4, 76); Thread.MemoryBarrier(); _uxView.Write(0, ++_uxSequence);
        SaveUxBatteryCache(false);
    }

    private void SaveUxBatteryCache(bool force)
    {
        long now = Environment.TickCount64; if (!force && now - _uxCacheSavedAt < 5000) return;
        _uxCacheSavedAt = now;
        string data = JsonSerializer.Serialize(new UxBatteryCache { Address = _companionAddress, Left = _islandLeft, Right = _islandRight, Case = _islandCase,
            Charging = _islandCharging, LeftSeen = _uxLeftSeen, RightSeen = _uxRightSeen, CaseSeen = _uxCaseSeen });
        if (data == _uxCacheWritten) return;
        try { string file = Path.Combine(_islandFolder, "battery-cache.json"); File.WriteAllText(file + ".tmp", data); File.Move(file + ".tmp", file, true); _uxCacheWritten = data; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Log.Warn("island", "battery cache write: " + e.GetType().Name); }
    }

    private void StopUxBridge()
    {
        SaveUxBatteryCache(true); _uxView?.Dispose(); _uxMap?.Dispose();
    }
}
