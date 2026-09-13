using System;
using System.Runtime.InteropServices;

namespace Soundheim.Audio;

// Explicit ABI calls also work on Unity's Mono runtime, without WinRT RCW support.
internal sealed class WindowsCom(IntPtr pointer) : IDisposable
{
    public IntPtr Pointer { get; private set; } = pointer;

    public T Method<T>(int slot) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(Pointer), slot * IntPtr.Size));

    public void Dispose()
    {
        if (Pointer == IntPtr.Zero) return;
        Marshal.Release(Pointer);
        Pointer = IntPtr.Zero;
    }

    public static void Check(int result, string operation)
    {
        if (result < 0) throw new COMException($"{operation} failed (0x{result:X8}). Check Windows audio settings and refresh.", result);
    }

    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern int RoInitialize(uint mode);
    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern void RoUninitialize();
    [DllImport("ole32.dll", ExactSpelling = true)]
    internal static extern int CoCreateInstance(ref Guid clsid, IntPtr outer, uint context, ref Guid iid, out IntPtr instance);

    internal sealed class Apartment : IDisposable
    {
        private readonly bool initialized;
        public Apartment()
        {
            int result = RoInitialize(1);
            // A Unity-owned thread may already have a different COM apartment.
            if (result != unchecked((int)0x80010106)) Check(result, "Initialize Windows audio COM");
            initialized = result >= 0;
        }
        public void Dispose() { if (initialized) RoUninitialize(); }
    }
}
