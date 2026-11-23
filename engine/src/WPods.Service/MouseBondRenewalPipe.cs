using System.Buffers.Binary;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32;

namespace WPods.Service;

// Local SYSTEM/admin bridge only. The normal widget cannot select a device or
// arm this directly: it asks its SID-scoped GlideMouse broker to use its fixed target.
internal static class MouseBondRenewalPipe
{
    internal const string Name = "GlideMouse.BondRenewal.v1";
    internal static async Task RunAsync(HciProxy hci, CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            var security = new PipeSecurity(); security.SetAccessRuleProtection(true, false);
            security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.NetworkSid,null),PipeAccessRights.FullControl,AccessControlType.Deny));
            security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid,null),PipeAccessRights.FullControl,AccessControlType.Allow));
            security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid,null),PipeAccessRights.FullControl,AccessControlType.Allow));
            NamedPipeServerStream pipe;
            try { pipe=NamedPipeServerStreamAcl.Create(Name,PipeDirection.InOut,4,PipeTransmissionMode.Message,PipeOptions.Asynchronous,64,64,security); }
            catch (IOException) { await Task.Delay(1000,stop);continue; }
            try { await pipe.WaitForConnectionAsync(stop); }
            catch { await pipe.DisposeAsync();if(stop.IsCancellationRequested)return;throw; }
            _ = Serve(pipe,hci,stop);
        }
    }
    private static async Task Serve(NamedPipeServerStream pipe,HciProxy hci,CancellationToken stop)
    {
        await using (pipe)
        try
        {
            using var limit=CancellationTokenSource.CreateLinkedTokenSource(stop);limit.CancelAfter(3000);
            byte[] request=new byte[16];int count=await Pipes.ReadMessageAsync(pipe,request,limit.Token);
            int error=13;
            ulong selectedAddress=0;byte operation=0;
            if(count==16&&request.AsSpan(0,4).SequenceEqual("GMR1"u8)&&request[5]==0&&request[6]==0&&request[7]==0)
            {
                ulong address=BinaryPrimitives.ReadUInt64LittleEndian(request.AsSpan(8));
                selectedAddress=address;operation=request[4];
                using var key=Registry.LocalMachine.OpenSubKey(@"SOFTWARE\GlideMouse\Handoff");
                ulong allowed=key?.GetValue("RenewalTarget") is long value ? unchecked((ulong)value):0;
                if(address==0||address!=allowed)error=5;
                else error=request[4] switch {1=>hci.ArmMouseRenewal(address)?0:170,2=>0,3=>hci.ActivateMouseTouch(address),4=>hci.AllowMouseDisconnect(address),5=>0,6=>hci.ReleaseMouseForPeer(address),7=>hci.ArmMouseRenewal(address,windowsOwnsSecurity:true)?0:170,_=>13};
            }
            var state=hci.MouseRenewalStatus();var response=new byte[32];"GMR1"u8.CopyTo(response);
            BinaryPrimitives.WriteInt32LittleEndian(response.AsSpan(4),error);
            if(error==0){BinaryPrimitives.WriteInt32LittleEndian(response.AsSpan(8),state.Generation);BinaryPrimitives.WriteInt32LittleEndian(response.AsSpan(12),(int)state.State);BinaryPrimitives.WriteInt32LittleEndian(response.AsSpan(16),(state.FreshKey?1:0)|(state.Authenticated?2:0)|(state.Encrypted?4:0));BinaryPrimitives.WriteInt32LittleEndian(response.AsSpan(20),state.Error);}
            if(error==0&&operation==5){var direct=hci.DirectMouseStatus(selectedAddress);BinaryPrimitives.WriteInt32LittleEndian(response.AsSpan(8),direct.Handle);BinaryPrimitives.WriteInt32LittleEndian(response.AsSpan(16),direct.Flags);}
            BinaryPrimitives.WriteInt64LittleEndian(response.AsSpan(24),Environment.TickCount64);
            await pipe.WriteAsync(response,limit.Token);
            // Bounded acknowledgement avoids dropping the response on disconnect.
            limit.CancelAfter(500);await pipe.ReadExactlyAsync(new byte[1],limit.Token);
        }
        catch(Exception e) when(e is IOException or OperationCanceledException or UnauthorizedAccessException or InvalidDataException) { }
    }
}
