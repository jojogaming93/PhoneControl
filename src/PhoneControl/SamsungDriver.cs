// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace PhoneControl;

// Download the original vendor installer instead of redistributing proprietary
// driver files or modifying their signed INF/catalog. Never elevate PhoneControl.
internal static class SamsungDriver
{
    internal const string Sha256 = "0ECC9E47D836A7CFF7215EC9CDF33DC7F3E416A7D5EDEDAD522B978B2EB84A20";
    private const string DownloadUrl = "https://developer.samsung.com/SDP/file/a2fcaf64-7aa3-49f7-b06f-9e646590d528";

    public static bool HasMissingDriver()
    {
        const uint present = 0x100;
        if (CM_Get_Device_ID_List_SizeW(out uint length, null, present) != 0 || length > 1_000_000) return false;
        var buffer = new char[length];
        if (CM_Get_Device_ID_ListW(null, buffer, length, present) != 0) return false;
        foreach (string id in new string(buffer).Split('\0', StringSplitOptions.RemoveEmptyEntries)) {
            if (!id.StartsWith("USB\\VID_04E8&", StringComparison.OrdinalIgnoreCase)) continue;
            if (CM_Locate_DevNodeW(out uint node, id, 0) == 0 && CM_Get_DevNode_Status(out _, out uint problem, node, 0) == 0 && problem == 28)
                return true;
        }
        return false;
    }

    public static async Task Install(CancellationToken cancellation)
    {
        using var downloadTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        downloadTimeout.CancelAfter(TimeSpan.FromMinutes(3));
        CancellationToken downloadCancellation = downloadTimeout.Token;
        string directory = Path.Combine(Settings.DirectoryPath, "downloads", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string destination = Path.Combine(directory, "Samsung-USB-Driver-1.9.5.0.exe");
        bool started = false;
        try {
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
            using var response = await client.GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead, downloadCancellation);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is long size && size > 60_000_000) throw new IOException("Unexpected driver download size.");
            await using (var source = await response.Content.ReadAsStreamAsync(downloadCancellation))
            await using (var target = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
                var buffer = new byte[81920]; int count; long total = 0;
                while ((count = await source.ReadAsync(buffer, downloadCancellation)) != 0) {
                    total += count; if (total > 60_000_000) throw new IOException("Unexpected driver download size.");
                    await target.WriteAsync(buffer.AsMemory(0, count), downloadCancellation);
                }
            }
            await VerifyDownload(destination, downloadCancellation);
            cancellation.ThrowIfCancellationRequested();
            Log.Write("Starting verified Samsung USB driver 1.9.5.0. Windows owns the elevation prompt.");
            using var process = Process.Start(new ProcessStartInfo(destination) { UseShellExecute = true, Verb = "runas", Arguments = "/S", WorkingDirectory = directory })
                ?? throw new IOException("Windows could not start Samsung driver setup.");
            started = true;
            // Do not cancel/terminate an elevated driver installer during app shutdown.
            await process.WaitForExitAsync();
            started = false;
            if (process.ExitCode != 0 && process.ExitCode != 3010) throw new IOException($"Samsung driver setup exited with code {process.ExitCode}.");
            if (process.ExitCode == 3010) throw new IOException("Windows needs a restart to finish driver installation. Restart at a convenient time.");
        } catch (OperationCanceledException) when (!cancellation.IsCancellationRequested) {
            throw new TimeoutException("Driver download timed out. Check your Internet connection and retry.");
        } finally {
            if (!started) { try { File.Delete(destination); Directory.Delete(directory); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
        }
    }

    internal static async Task VerifyDownload(string path, CancellationToken cancellation)
    {
        await using var file = File.OpenRead(path);
        string hash = Convert.ToHexString(await SHA256.HashDataAsync(file, cancellation));
        if (!hash.Equals(Sha256, StringComparison.Ordinal)) throw new IOException("Samsung driver checksum mismatch. Nothing was executed; contact support for an updated driver.");
    }

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint CM_Get_Device_ID_List_SizeW(out uint length, string? filter, uint flags);
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint CM_Get_Device_ID_ListW(string? filter, [Out] char[] buffer, uint length, uint flags);
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint CM_Locate_DevNodeW(out uint node, string deviceId, uint flags);
    [DllImport("cfgmgr32.dll", ExactSpelling = true)]
    private static extern uint CM_Get_DevNode_Status(out uint status, out uint problem, uint node, uint flags);
}
