using System.Buffers.Binary;

namespace WPods.Service;

// Protocol-only channel discovery for the configured mouse. No HID input,
// user text, audio or authentication key is stored. A fresh ACL owns a fresh
// instance; channel numbers from an earlier connection must never be reused.
internal sealed class MouseTouchActivation
{
    internal ushort LocalCid { get; private set; }
    internal ushort RemoteCid { get; private set; }
    internal long LastWindowsControl { get; private set; }
    internal long PendingUntil { get; private set; }
    internal bool Confirmed { get; private set; }
    private ushort _requested, _remoteRequested;
    internal void Observe(ReadOnlySpan<byte> pdu,bool incoming,long now)
    {
        if(pdu.Length<4||BinaryPrimitives.ReadUInt16LittleEndian(pdu)+4!=pdu.Length)return;
        ushort cid=BinaryPrimitives.ReadUInt16LittleEndian(pdu[2..]);var data=pdu[4..];
        if(cid!=1){if(!incoming&&cid==RemoteCid)LastWindowsControl=now;return;}
        while(data.Length>=4)
        {
            int size=BinaryPrimitives.ReadUInt16LittleEndian(data[2..]);
            if(size>data.Length-4)return;
            var body=data.Slice(4,size);
            if(data[0]==2&&size==4&&BinaryPrimitives.ReadUInt16LittleEndian(body)==0x11)
            {
                ushort requested=BinaryPrimitives.ReadUInt16LittleEndian(body[2..]);
                if(requested>=0x40){if(incoming)_remoteRequested=requested;else _requested=requested;}
            }
            else if(data[0]==3&&size==8&&BinaryPrimitives.ReadUInt16LittleEndian(body[4..])==0)
            {
                ushort destination=BinaryPrimitives.ReadUInt16LittleEndian(body),source=BinaryPrimitives.ReadUInt16LittleEndian(body[2..]);
                if(destination>=0x40&&source>=0x40)
                {
                    if(incoming&&_requested!=0&&source==_requested){LocalCid=source;RemoteCid=destination;}
                    else if(!incoming&&_remoteRequested!=0&&source==_remoteRequested){LocalCid=destination;RemoteCid=source;}
                }
            }
            else if(data[0] is 6 or 7&&size==4&&LocalCid!=0)
            {
                ushort destination=BinaryPrimitives.ReadUInt16LittleEndian(body),source=BinaryPrimitives.ReadUInt16LittleEndian(body[2..]);
                if((destination==LocalCid&&source==RemoteCid)||(source==LocalCid&&destination==RemoteCid))
                {LocalCid=RemoteCid=_requested=_remoteRequested=0;Confirmed=false;PendingUntil=0;}
            }
            data=data[(size+4)..];
        }
    }
    internal bool Begin(long now)
    {
        if(LocalCid==0||RemoteCid==0||Confirmed||PendingUntil!=0||now-LastWindowsControl<1000)return false;
        PendingUntil=now+1000;return true;
    }
    internal bool Handshake(ReadOnlySpan<byte> pdu,long now)
    {
        if(PendingUntil==0||now>PendingUntil||pdu.Length!=5||BinaryPrimitives.ReadUInt16LittleEndian(pdu)!=1
            ||BinaryPrimitives.ReadUInt16LittleEndian(pdu[2..])!=LocalCid||(pdu[4]&0xF0)!=0)return false;
        Confirmed=pdu[4]==0;PendingUntil=0;return true;
    }
    internal void Expire(long now){if(PendingUntil!=0&&now>=PendingUntil)PendingUntil=0;}
    // HIDP SET_REPORT / FEATURE, followed by Magic Mouse 2 multitouch enable.
    // Same protocol bytes documented by Linux hid-magicmouse, not copied code.
    internal static ReadOnlySpan<byte> EnableReport => [0x53,0xF1,0x02,0x01];
}
