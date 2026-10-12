// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only

using System.Text.RegularExpressions;

namespace PhoneControl;

// ADB/scrcpy supports Android vendors through the same protocol. Tool discovery
// is lazy so a missing installation is reported inside the dashboard.
public sealed class AndroidPhoneBackend : IPhoneBackend
{
    private readonly Lazy<ToolPaths> tools = new(() => new ToolPaths());
    public async Task<IReadOnlyList<PhoneDevice>> DiscoverDevices() =>
        ParseDevices(await Processes.Run(tools.Value.Adb, "devices", "-l"));

    public IPhoneSession CreateSession() => new DeviceSession(tools.Value);

    internal static IReadOnlyList<PhoneDevice> ParseDevices(string output) =>
        output.Split('\n').Select(line => Regex.Match(line, @"^(\S+)\s+(device|unauthorized|offline)\b(.*)"))
            .Where(m => m.Success).Select(m => {
                string id = m.Groups[1].Value;
                var state = m.Groups[2].Value switch {
                    "device" => PhoneDeviceState.Ready,
                    "unauthorized" => PhoneDeviceState.Unauthorized,
                    _ => PhoneDeviceState.Offline
                };
                var transport = id.StartsWith("emulator-", StringComparison.OrdinalIgnoreCase) ? PhoneTransport.Emulator :
                    id.Contains(':') || id.EndsWith("._adb-tls-connect._tcp", StringComparison.OrdinalIgnoreCase) ? PhoneTransport.Network : PhoneTransport.Usb;
                return new PhoneDevice(id, state, m.Groups[3].Value.Trim(), transport);
            }).ToArray();
}
