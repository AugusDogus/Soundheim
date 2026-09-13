using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Soundheim.Audio;

internal static class WindowsOutputs
{
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int EnumEndpoints(IntPtr self, int flow, uint mask, out IntPtr collection);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetCount(IntPtr self, out uint count);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetItem(IntPtr self, uint index, out IntPtr device);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetId(IntPtr self, out IntPtr id);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int OpenProperties(IntPtr self, uint access, out IntPtr properties);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetValue(IntPtr self, ref PropertyKey key, out PropVariant value);

    [StructLayout(LayoutKind.Sequential)]
    private struct PropertyKey { public Guid Format; public uint Id; }
    [StructLayout(LayoutKind.Explicit, Size = 24)]
    private struct PropVariant
    {
        [FieldOffset(0)] public ushort Type;
        [FieldOffset(8)] public IntPtr String;
    }
    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern int PropVariantClear(ref PropVariant value);

    public static IReadOnlyList<AudioDevice> List()
    {
        using var apartment = new WindowsCom.Apartment();
        var clsid = new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E");
        var iid = new Guid("A95664D2-9614-4F35-A746-DE8DB63617E6");
        WindowsCom.Check(WindowsCom.CoCreateInstance(ref clsid, IntPtr.Zero, 1, ref iid, out IntPtr pointer), "Discover Windows outputs");
        using var enumerator = new WindowsCom(pointer);
        // IMMDeviceEnumerator::EnumAudioEndpoints, render flow, active devices only.
        WindowsCom.Check(enumerator.Method<EnumEndpoints>(3)(pointer, 0, 1, out IntPtr list), "List Windows outputs");
        using var collection = new WindowsCom(list);
        WindowsCom.Check(collection.Method<GetCount>(3)(list, out uint count), "Count Windows outputs");
        var outputs = new List<AudioDevice>();
        for (uint index = 0; index < count; index++)
        {
            WindowsCom.Check(collection.Method<GetItem>(4)(list, index, out IntPtr item), "Read Windows output");
            using var device = new WindowsCom(item);
            WindowsCom.Check(device.Method<GetId>(5)(item, out IntPtr idPointer), "Read Windows output ID");
            string id;
            try { id = Marshal.PtrToStringUni(idPointer) ?? throw new InvalidOperationException("Windows returned an empty output ID."); }
            finally { Marshal.FreeCoTaskMem(idPointer); }
            WindowsCom.Check(device.Method<OpenProperties>(4)(item, 0, out IntPtr store), "Read Windows output properties");
            using var properties = new WindowsCom(store);
            var key = new PropertyKey { Format = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), Id = 14 };
            WindowsCom.Check(properties.Method<GetValue>(5)(store, ref key, out PropVariant value), "Read Windows output name");
            try
            {
                string name = value.Type == 31 ? Marshal.PtrToStringUni(value.String) ?? id : id;
                outputs.Add(new AudioDevice(id, name));
            }
            finally { PropVariantClear(ref value); }
        }
        return outputs;
    }
}
