using System;
using System.Runtime.InteropServices;

namespace Soundheim.Audio;

// Undocumented per-app policy ABI. GUIDs and slot order verified
// against EarTrumpet's AudioPolicyConfigFactory interfaces. See THIRD-PARTY-NOTICES.md.
internal sealed class WindowsAudioPolicy : IDisposable
{
    private readonly WindowsCom factory;
    private const string RuntimeClass = "Windows.Media.Internal.AudioPolicyConfig";
    private const string Prefix = @"\\?\SWD#MMDEVAPI#";
    private const string Suffix = "#{e6327cad-dcec-4949-ae8a-991e976a79d2}";

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int SetEndpoint(IntPtr self, uint pid, int flow, int role, IntPtr device);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetEndpoint(IntPtr self, uint pid, int flow, int role, out IntPtr device);
    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern int WindowsCreateString([MarshalAs(UnmanagedType.LPWStr)] string value, uint length, out IntPtr handle);
    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern int WindowsDeleteString(IntPtr handle);
    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern IntPtr WindowsGetStringRawBuffer(IntPtr handle, out uint length);
    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern int RoGetActivationFactory(IntPtr name, ref Guid iid, out IntPtr factory);

    public WindowsAudioPolicy()
    {
        using var name = new HString(RuntimeClass);
        var modern = new Guid("AB3D4648-E242-459F-B02F-541C70306324");
        int result = RoGetActivationFactory(name.Pointer, ref modern, out IntPtr pointer);
        if (result < 0)
        {
            var older = new Guid("2A59116D-6C4F-45E0-A74F-707E3FEF9258");
            result = RoGetActivationFactory(name.Pointer, ref older, out pointer);
        }
        WindowsCom.Check(result, "Open Windows per-app audio policy");
        factory = new WindowsCom(pointer);
    }

    private string Read(uint pid, int role)
    {
        // IUnknown (3), IInspectable (3), then 19 preceding audio-policy methods.
        WindowsCom.Check(factory.Method<GetEndpoint>(26)(factory.Pointer, pid, 0, role, out IntPtr value), "Read Valheim output preference");
        try
        {
            IntPtr text = WindowsGetStringRawBuffer(value, out uint length);
            return length == 0 ? "" : Marshal.PtrToStringUni(text, checked((int)length)) ?? "";
        }
        finally { WindowsDeleteString(value); }
    }

    private int Write(uint pid, int role, string endpoint)
    {
        using var value = new HString(endpoint);
        return factory.Method<SetEndpoint>(25)(factory.Pointer, pid, 0, role, value.Pointer);
    }

    public void Apply(uint pid, string id)
    {
        string endpoint = id.Length == 0 ? "" : Prefix + id + Suffix;
        string console = Read(pid, 0);
        string multimedia = Read(pid, 1);
        if (console == endpoint && multimedia == endpoint) return;
        WindowsCom.Check(Write(pid, 0, endpoint), "Set Valheim console output");
        int result = Write(pid, 1, endpoint);
        if (result < 0)
        {
            int rollback = Write(pid, 0, console);
            if (rollback < 0)
                throw new COMException($"Windows only partly applied the output and could not restore it (0x{rollback:X8}). Select System default or use Windows Volume mixer.", rollback);
            WindowsCom.Check(result, "Set Valheim multimedia output (previous console preference restored)");
        }
    }

    public void Dispose() => factory.Dispose();

    private sealed class HString : IDisposable
    {
        public IntPtr Pointer { get; }
        public HString(string value)
        {
            if (value.Length == 0) return;
            WindowsCom.Check(WindowsCreateString(value, (uint)value.Length, out IntPtr pointer), "Prepare Windows audio identifier");
            Pointer = pointer;
        }
        public void Dispose() { if (Pointer != IntPtr.Zero) WindowsDeleteString(Pointer); }
    }
}
