// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PhoneControl;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Contains("--self-test"))
        {
            Environment.ExitCode = SelfTest.Run();
            return;
        }
        string user = System.Security.Principal.WindowsIdentity.GetCurrent().User!.Value;
        using var instance = new Mutex(true, "Local\\PhoneControl-" + user, out bool firstInstance);
        if (!firstInstance) {
            foreach (var process in System.Diagnostics.Process.GetProcessesByName("PhoneControl")) {
                using (process) {
                    try {
                        if (process.Id == Environment.ProcessId || process.MainWindowHandle == 0) continue;
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
        Application.Start(args => {
            var context = new Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(
                Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });
    }
}

public sealed partial class App : Application
{
    private MainWindow? window;
    public App()
    {
        Log.Write("Initializing application resources.");
        InitializeComponent();
        UnhandledException += (_, e) => {
            Log.Write(e.Exception.ToString());
        };
    }
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try {
            Log.Write("Creating main window.");
            window = new MainWindow(new AndroidPhoneBackend(), [new SamsungDriver()]);
            window.Activate();
        } catch (Exception e) { Log.Write(e.ToString()); throw; }
    }
}
