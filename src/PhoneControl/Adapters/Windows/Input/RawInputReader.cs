// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only

using System.Runtime.InteropServices;

using PhoneControl.Adapters.Diagnostics;
using PhoneControl.Adapters.Windows.Interop;
using PhoneControl.Application.Input;

namespace PhoneControl.Adapters.Windows.Input;

// Raw Input is scoped in InputEngine to the foreground phone view. It also preserves
// very short press/release edges that can occur between two polling ticks.
public sealed class RawInputReader : IDisposable
{
    private readonly nint window;
    private readonly SubclassProc callback;
    private readonly InputEngine engine;
    public RawInputReader(nint window, InputEngine engine)
    {
        this.window = window; this.engine = engine; callback = Proc;
        if (!SetWindowSubclass(window, callback, 42, 0)) throw new System.ComponentModel.Win32Exception();
        var devices = new[] { new Device { Page = 1, Usage = 6, Flags = 0x100, Target = window },
            new Device { Page = 1, Usage = 2, Flags = 0x100, Target = window } };
        if (!RegisterRawInputDevices(devices, 2, (uint)Marshal.SizeOf<Device>())) {
            RemoveWindowSubclass(window, callback, 42);
            throw new System.ComponentModel.Win32Exception();
        }
    }
    private nint Proc(nint hwnd, uint message, nint wp, nint lp, nuint id, nuint data)
    {
        if (message == 0xFF) {
            uint size = 0; uint header = (uint)(8 + IntPtr.Size * 2);
            GetRawInputData(lp, 0x10000003, 0, ref size, header);
            if (size is > 0 and < 4096) {
                var buffer = Marshal.AllocHGlobal((int)size);
                try {
                    if (GetRawInputData(lp, 0x10000003, buffer, ref size, header) == size) {
                        int type = Marshal.ReadInt32(buffer); int offset = (int)header;
                        if (type == 1) {
                            int key = (ushort)Marshal.ReadInt16(buffer, offset + 6);
                            ushort flags = (ushort)Marshal.ReadInt16(buffer, offset + 2);
                            bool down = (flags & 1) == 0;
                            key = NormalizeKey(key, flags);
                            engine.RecordEdge(key, down);
                        } else if (type == 0) {
                            ushort movementFlags = (ushort)Marshal.ReadInt16(buffer, offset);
                            bool virtualDesktop = (movementFlags & 2) != 0;
                            engine.RecordMotion(movementFlags, Marshal.ReadInt32(buffer, offset + 12), Marshal.ReadInt32(buffer, offset + 16),
                                Native.GetSystemMetrics(virtualDesktop ? 78 : 0), Native.GetSystemMetrics(virtualDesktop ? 79 : 1));
                            ushort flags = (ushort)Marshal.ReadInt16(buffer, offset + 4);
                            if ((flags & 0x400) != 0) engine.RecordWheel(Marshal.ReadInt16(buffer, offset + 6));
                            var buttonCodes = new[] { 1, 2, 4, 5, 6 };
                            for (int i = 0; i < 5; i++) {
                                if ((flags & (1 << (i * 2))) != 0) engine.RecordEdge(buttonCodes[i], true);
                                if ((flags & (2 << (i * 2))) != 0) engine.RecordEdge(buttonCodes[i], false);
                            }
                        }
                    }
                } catch (Exception e) { Log.Write("Raw input: " + e.Message); }
                finally { Marshal.FreeHGlobal(buffer); }
            }
        }
        return DefSubclassProc(hwnd, message, wp, lp);
    }
    internal static int NormalizeKey(int key, ushort flags) => key switch {
        0xA0 or 0xA1 => 0x10,
        0xA2 or 0xA3 => 0x11,
        // Raw keyboards usually report VK_MENU; RI_KEY_E0 distinguishes Right Alt.
        0x12 => (flags & 2) != 0 ? 0xA5 : 0xA4,
        _ => key
    };
    public void Dispose()
    {
        var devices = new[] { new Device { Page = 1, Usage = 6, Flags = 1 }, new Device { Page = 1, Usage = 2, Flags = 1 } };
        RegisterRawInputDevices(devices, 2, (uint)Marshal.SizeOf<Device>());
        RemoveWindowSubclass(window, callback, 42); GC.KeepAlive(callback);
    }
    [StructLayout(LayoutKind.Sequential)] private struct Device { public ushort Page, Usage; public uint Flags; public nint Target; }
    private delegate nint SubclassProc(nint hwnd, uint msg, nint wp, nint lp, nuint id, nuint data);
    [DllImport("comctl32.dll")] private static extern bool SetWindowSubclass(nint h, SubclassProc p, nuint id, nuint data);
    [DllImport("comctl32.dll")] private static extern bool RemoveWindowSubclass(nint h, SubclassProc p, nuint id);
    [DllImport("comctl32.dll")] private static extern nint DefSubclassProc(nint h, uint msg, nint wp, nint lp);
    [DllImport("user32.dll")] private static extern bool RegisterRawInputDevices(Device[] d, uint count, uint size);
    [DllImport("user32.dll")] private static extern uint GetRawInputData(nint input, uint command, nint buffer, ref uint size, uint header);
}
