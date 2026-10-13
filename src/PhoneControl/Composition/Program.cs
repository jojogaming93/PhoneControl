// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using PhoneControl.Adapters.Configuration;
using PhoneControl.Adapters.Diagnostics;
using PhoneControl.Adapters.Windows.Interop;
using PhoneControl.Tests;

namespace PhoneControl.Composition;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Contains("--self-test") || args.Contains("--self-test-ios"))
        {
            Environment.ExitCode = SelfTest.Run(args.Contains("--self-test-ios"));
            return;
        }
        string user = System.Security.Principal.WindowsIdentity.GetCurrent().User!.Value;
        using var instance = new Mutex(true, "Local\\" + AppInfo.InstanceName + "-" + user, out bool firstInstance);
        if (!firstInstance) {
            foreach (var process in System.Diagnostics.Process.GetProcessesByName("PhoneControl")) {
                using (process) {
                    try {
                        if (process.Id == Environment.ProcessId || process.MainWindowHandle == 0) continue;
                        if (!string.Equals(process.MainModule?.FileName, Path.Combine(AppContext.BaseDirectory, "PhoneControl.exe"), StringComparison.OrdinalIgnoreCase)) continue;
                        Native.ShowWindow(process.MainWindowHandle, 9);
                        Native.SetForegroundWindow(process.MainWindowHandle);
                        break;
                    } catch (System.ComponentModel.Win32Exception) { } catch (InvalidOperationException) { }
                }
            }
            return;
        }
        WinRT.ComWrappersSupport.InitializeComWrappers();
        Log.Write("Starting WinUI application.");
        Microsoft.UI.Xaml.Application.Start(args => {
            var context = new Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(
                Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });
    }
}
