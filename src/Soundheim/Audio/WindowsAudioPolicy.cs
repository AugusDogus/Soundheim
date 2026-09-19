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

    public void Apply(uint pid, string id, bool refresh = false)
    {
        string endpoint = id.Length == 0 ? "" : Prefix + id + Suffix;
        Apply(endpoint, role => Read(pid, role), (role, value) => Write(pid, role, value), refresh);
    }

    internal static void Apply(string endpoint, Func<int, string> read, Func<int, string, int> write, bool refresh)
    {
        string console = read(0);
        string multimedia = read(1);
        bool matches = string.Equals(console, endpoint, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(multimedia, endpoint, StringComparison.OrdinalIgnoreCase);
        if (matches && !refresh) return;
        // Persisted preferences can match while a new game session still plays
        // on the default device. Recreate the user's off/on change in that case.
        if (matches && endpoint.Length > 0) WritePair("");
        WritePair(endpoint);

        void WritePair(string value)
        {
            int result = write(0, value);
            if (result >= 0) result = write(1, value);
            if (result >= 0) return;
            int restoreConsole = write(0, console);
            int restoreMultimedia = write(1, multimedia);
            if (restoreConsole < 0 || restoreMultimedia < 0)
                throw new COMException($"Windows could not apply or restore Valheim's output preferences (console 0x{restoreConsole:X8}, multimedia 0x{restoreMultimedia:X8}). Select System default or use Windows Volume mixer.", result);
            WindowsCom.Check(result, "Set Valheim output (previous preferences restored)");
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
