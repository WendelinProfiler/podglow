using System.Buffers.Binary;

namespace WPods.Service;

// Explicitly opted-in, single-device HIDP endpoint. Windows still owns Classic
// authentication; these two L2CAP channels are ours, not the stalled HidBth PDO.
// The existing signed USB reader and per-user GlideMouse engine consume input.
internal sealed class DirectMouseHid
{
    internal readonly record struct Outbound(ushort Cid,byte[] Payload);
    private sealed class Channel(ushort psm,ushort local)
    {
        internal readonly ushort Psm=psm,Local=local;
        internal ushort Remote;
        internal bool Requested,LocalConfigured,RemoteConfigured;
        internal bool Open=>Remote!=0&&LocalConfigured&&RemoteConfigured;
    }
    private readonly Channel _control=new(0x11,0x7FC0),_interrupt=new(0x13,0x7FC1);
    private readonly Dictionary<byte,(byte Code,Channel Channel)> _pending=[];
    private readonly Queue<Outbound> _output=[];
    private byte _nextId=0xC0;
    private bool _featureMayBeActive,_featureRetried;
    private long _featureRetryAt=-1;
    private long _started=-1,_protocolSent=-1,_enableSent=-1,_controlOpenAt=-1;
    internal bool Failed { get; private set; }
    internal bool Ready { get; private set; }
    internal bool Released { get; private set; }
    internal ulong InputReports { get; private set; }
    internal int BatteryPercent { get; private set; } = -1;
    internal ulong BatterySequence { get; private set; }
    private long _batteryRequestedAt = -1;
    private bool _batteryPending;
    internal ushort ControlCid=>_control.Local;
    internal bool OwnCid(ushort cid)=>cid==_control.Local||cid==_interrupt.Local;
    internal bool ReleaseForPeer()
    {
        if(Released)return true;if(!_control.Open)return false;
        // HID_CONTROL VIRTUAL_CABLE_UNPLUG clears the mouse's old virtual cable
        // without deleting its Windows PnP node or restarting either adapter.
        Released=true;Ready=false;_output.Clear();_output.Enqueue(new(_control.Remote,[0x15]));return true;
    }
    internal List<Outbound> TakeOutput(){var output=_output.ToList();_output.Clear();return output;}
    private static ushort U16(ReadOnlySpan<byte> value)=>BinaryPrimitives.ReadUInt16LittleEndian(value);
    private static byte[] Pair(ushort a,ushort b)=>[(byte)a,(byte)(a>>8),(byte)b,(byte)(b>>8)];
    private void Signal(byte code,byte id,ReadOnlySpan<byte> data)
    {
        var frame=new byte[4+data.Length];frame[0]=code;frame[1]=id;
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2),(ushort)data.Length);data.CopyTo(frame.AsSpan(4));
        _output.Enqueue(new(1,frame));
    }
    private void Request(byte code,Channel channel,ReadOnlySpan<byte> data)
    {
        byte id=_nextId;_nextId=_nextId==0xFF?(byte)0xC0:(byte)(_nextId+1);
        if(_pending.ContainsKey(id)){Failed=true;return;}
        _pending[id]=(code,channel);Signal(code,id,data);
    }
    internal void Start(long now)
    {
        if(_started>=0||Failed)return;_started=now;
        // The USB-C mouse actively opens its virtual-cable channels after
        // encryption. Accept that first instead of racing it with a duplicate
        // control channel. A host-initiated fallback remains bounded.
    }
    private void Connect(Channel channel)
    {
        if(channel.Requested||channel.Remote!=0)return;
        channel.Requested=true;Request(2,channel,Pair(channel.Psm,channel.Local));
    }
    private void Configure(Channel channel)
    {
        // Basic mode with a 512-byte receive MTU; enough for all mouse reports.
        Request(4,channel,[(byte)channel.Remote,(byte)(channel.Remote>>8),0,0,1,2,0,2]);
    }
    internal bool OwnSignal(ReadOnlySpan<byte> command,bool prefix=false)
    {
        if(command.Length<4||U16(command[2..])+4<command.Length||(!prefix&&U16(command[2..])+4!=command.Length))return false;
        byte code=command[0];var body=command[4..];
        if(code is 1 or 3 or 5 or 7)
        {
            if(!_pending.TryGetValue(command[1],out var pending))return false;
            if(code==1)return true;
            int offset=code==5?0:2;
            return code==pending.Code+1&&body.Length>=offset+2&&U16(body[offset..])==pending.Channel.Local;
        }
        if(code==2&&body.Length==4)return U16(body) is 0x11 or 0x13;
        return code is 4 or 6&&body.Length>=4&&OwnCid(U16(body));
    }
    internal void SignalInput(ReadOnlySpan<byte> command,long now)
    {
        if(Failed||!OwnSignal(command))return;
        byte code=command[0],id=command[1];var body=command[4..];
        if(Released&&code!=6&&code!=7)return;
        if(code==2)
        {
            var channel=U16(body)==0x11?_control:_interrupt;ushort remote=U16(body[2..]);
            if(remote<0x40||channel.Remote!=0||channel.Requested)
            {Signal(3,id,[(byte)channel.Local,(byte)(channel.Local>>8),(byte)remote,(byte)(remote>>8),4,0,0,0]);return;}
            channel.Remote=remote;channel.Requested=true;
            Signal(3,id,[(byte)channel.Local,(byte)(channel.Local>>8),(byte)remote,(byte)(remote>>8),0,0,0,0]);Configure(channel);
        }
        else if(code==3&&body.Length==8)
        {
            ushort result=U16(body[4..]);if(result==1)return;
            var pending=_pending[id];_pending.Remove(id);
            if(result!=0||U16(body)<0x40){Failed=true;return;}
            pending.Channel.Remote=U16(body);Configure(pending.Channel);
        }
        else if(code==4&&body.Length>=4)
        {
            var channel=U16(body)==_control.Local?_control:_interrupt;
            if(channel.Remote==0)return;
            // Reject unknown mandatory options, echoing only those options.
            var unsupported=new List<byte>();bool malformed=false;
            for(int at=4;at<body.Length;)
            {
                if(at+2>body.Length||at+2+body[at+1]>body.Length){malformed=true;break;}
                int size=2+body[at+1];byte kind=(byte)(body[at]&0x7F);
                if((body[at]&0x80)==0&&kind is not (1 or 2))unsupported.AddRange(body.Slice(at,size).ToArray());
                at+=size;
            }
            byte result=malformed?(byte)2:unsupported.Count>0?(byte)3:(byte)0;
            byte[] reply=[(byte)channel.Remote,(byte)(channel.Remote>>8),0,0,result,0,..unsupported];
            Signal(5,id,reply);
            if(result==0&&U16(body[2..])==0)channel.RemoteConfigured=true;
        }
        else if(code==5&&body.Length>=6)
        {
            ushort result=U16(body[4..]);if(result==4)return;
            var pending=_pending[id];_pending.Remove(id);
            if(result!=0||U16(body[2..])!=0){Failed=true;return;}
            pending.Channel.LocalConfigured=true;
        }
        else if(code==6)
        {
            var channel=U16(body)==_control.Local?_control:_interrupt;
            Signal(7,id,Pair(channel.Local,channel.Remote));channel.Remote=0;Ready=false;Failed=true;
        }
        else if(code==1){_pending.Remove(id);Failed=true;}
        CheckOpen(now);
    }
    private void CheckOpen(long now)
    {
        if(Failed||Released)return;
        if(_control.Open&&_controlOpenAt<0)_controlOpenAt=now;
        if(_controlOpenAt>=0&&now-_controlOpenAt>=500)Connect(_interrupt);
        if(_control.Open&&_interrupt.Open&&_protocolSent<0)
        {
            _protocolSent=now;
            _output.Enqueue(new(_control.Remote,[0x14])); // EXIT_SUSPEND has no response.
            _output.Enqueue(new(_control.Remote,[0x71])); // SET_PROTOCOL: report mode.
        }
    }
    internal void Data(ushort cid,ReadOnlySpan<byte> payload,long now)
    {
        if(Failed||Released)return;
        // MM3 input report 0x90 is [report ID, power flags, capacity].
        // GET_REPORT responses use the control channel; the status byte is
        // not a charge percentage. Never decode feature 0x47 as a battery.
        if(cid==_control.Local && _batteryPending && payload.Length==4 && payload[0]==0xA1 && payload[1]==0x90) {
            _batteryPending=false;
            if(payload[3]<=100){BatteryPercent=payload[3];BatterySequence++;}
            return;
        }
        if(cid==_control.Local && _batteryPending && payload.Length==1 && (payload[0]&0xF0)==0) {
            _batteryPending=false;return; // An unsupported battery read must not stop touch.
        }
        if(_enableSent>=0&&!Ready&&!_featureMayBeActive&&now-_enableSent>3000){Failed=true;return;}
        if(cid==_control.Local&&_protocolSent>=0&&payload.Length==1&&(payload[0]&0xF0)==0)
        {
            if(_enableSent<0)
            {
                // A report-only device may reject SET_PROTOCOL as unsupported.
                // Its default is already report mode; still try the known feature.
                if(payload[0] is not (0 or 3)){Failed=true;return;}
                _enableSent=now;_output.Enqueue(new(_control.Remote,[0x53,0xF1,0x02,0x01]));
            }
            else if(payload[0]==2)
            {
                // Known device quirk: INVALID_REPORT_ID can accompany a mode
                // switch that actually took effect. Retry once after settling,
                // then require real full reports instead of inventing success.
                _featureMayBeActive=true;
                if(!_featureRetried&&_featureRetryAt<0)_featureRetryAt=now+500;
            }
            else if(payload[0]!=0)Failed=true;else Ready=true;
        }
        // A validated full report is stronger proof than a missing handshake.
        if(cid==_interrupt.Local&&_interrupt.Open&&payload.Length>=9&&payload[0]==0xA1&&payload[1]==0x12)
        {
            int size=payload.Length-1;if(size!=8&&(size<14||size>142||(size-14)%8!=0))return;
            InputReports++;if(size>=14&&_enableSent>=0)Ready=true;
        }
    }
    internal void Tick(long now)
    {
        if(_started<0||Failed||Released)return;
        if(Ready && _control.Open && (_batteryRequestedAt<0 || now-_batteryRequestedAt>=60000)) {
            _batteryRequestedAt=now;_batteryPending=true;
            _output.Enqueue(new(_control.Remote,[0x41,0x90]));
        }
        if(!Ready&&!_featureMayBeActive&&now-_started>10000){Failed=true;return;}
        if(now-_started>=500)Connect(_control);CheckOpen(now);
        if(!Ready&&_featureRetryAt>=0&&now>=_featureRetryAt&&!_featureRetried)
        {_featureRetried=true;_featureRetryAt=-1;_enableSent=now;_output.Enqueue(new(_control.Remote,[0x53,0xF1,0x02,0x01]));}
    }
}
