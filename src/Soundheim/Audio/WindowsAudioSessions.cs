using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Soundheim.Audio;

internal static class WindowsAudioSessions
{
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetDevice(IntPtr self, [MarshalAs(UnmanagedType.LPWStr)] string id, out IntPtr device);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int Activate(IntPtr self, ref Guid iid, uint context, IntPtr parameters, out IntPtr instance);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetEnumerator(IntPtr self, out IntPtr sessions);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetCount(IntPtr self, out int count);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetSession(IntPtr self, int index, out IntPtr session);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetProcessId(IntPtr self, out uint pid);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetState(IntPtr self, out int state);

    // Caller owns the COM apartment. Enumerate actual active streams rather than
    // treating GetPersistedDefaultAudioEndpoint as proof of where audio is playing.
    internal static IReadOnlyList<string> ActiveOutputs(uint processId, IReadOnlyList<AudioDevice> outputs)
    {
        var clsid = new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E");
        var enumeratorId = new Guid("A95664D2-9614-4F35-A746-DE8DB63617E6");
        WindowsCom.Check(WindowsCom.CoCreateInstance(ref clsid, IntPtr.Zero, 1, ref enumeratorId, out IntPtr pointer), "Discover Valheim audio sessions");
        using var devices = new WindowsCom(pointer);
        var active = new List<string>();
        foreach (AudioDevice output in outputs)
        {
            WindowsCom.Check(devices.Method<GetDevice>(5)(pointer, output.Id, out IntPtr endpoint), "Open Windows audio output for session inspection");
            using var device = new WindowsCom(endpoint);
            var managerId = new Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F");
            WindowsCom.Check(device.Method<Activate>(3)(endpoint, ref managerId, 1, IntPtr.Zero, out IntPtr managerPointer), "Inspect Windows audio sessions");
            using var manager = new WindowsCom(managerPointer);
            WindowsCom.Check(manager.Method<GetEnumerator>(5)(managerPointer, out IntPtr sessionsPointer), "Enumerate Windows audio sessions");
            using var sessions = new WindowsCom(sessionsPointer);
            WindowsCom.Check(sessions.Method<GetCount>(3)(sessionsPointer, out int count), "Count Windows audio sessions");
            for (int index = 0; index < count; index++)
            {
                WindowsCom.Check(sessions.Method<GetSession>(4)(sessionsPointer, index, out IntPtr controlPointer), "Read Windows audio session");
                using var control = new WindowsCom(controlPointer);
                var controlId = new Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D");
                WindowsCom.Check(Marshal.QueryInterface(controlPointer, ref controlId, out IntPtr sessionPointer), "Inspect audio session owner");
                using var session = new WindowsCom(sessionPointer);
                // IAudioSessionControl2::GetProcessId is slot 14 in audiopolicy.h.
                int result = session.Method<GetProcessId>(14)(sessionPointer, out uint owner);
                WindowsCom.Check(result, "Read audio session process ID");
                // S_FALSE-like success identifies a shared session, not one owner.
                if (result != 0 || owner != processId) continue;
                WindowsCom.Check(session.Method<GetState>(3)(sessionPointer, out int state), "Read Valheim audio session state");
                if (state != 1) continue; // AudioSessionStateActive
                active.Add(output.Id);
                break;
            }
        }
        return active;
    }
}
