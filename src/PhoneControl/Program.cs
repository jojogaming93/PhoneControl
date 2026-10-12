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
            window = new MainWindow([
                new("android", "Android · USB / scrcpy", _ => new AndroidPhoneBackend(), [new SamsungDriver()],
                    "Android setup: connect a data-capable USB cable, unlock the phone, enable USB debugging in Developer options, and accept Allow USB debugging. On Samsung, enable Developer options by tapping Build number seven times under About phone → Software information. Missing Samsung drivers can use Repair Samsung USB driver; other vendors use their manufacturer's driver. Dismiss idle touch protection before playing."),
                new("iphone", "iPhone · WebDriverAgent (experimental)", endpoint => new IosPhoneBackend(endpoint), [],
                    "iPhone setup: supply a dedicated, already running WebDriverAgent endpoint for an iPhone or an iOS Simulator on a Mac. Enter its reachable address above (a USB forward can expose it on localhost). PhoneControl does not install or start the Apple test runner. The viewer supports taps, swipes, long presses up to 2 seconds, and Home through complete gestures. Audio and continuous keyboard/mouse gameplay mapping are unavailable. An existing WDA session will not be taken over. No real iPhone has been tested yet.",
                    UsesAddress: true, SupportsRealtimeTouch: false, NormalizeAddress: endpoint => IosWdaClient.ValidateEndpoint(endpoint).AbsoluteUri)
            ]);
            window.Activate();
        } catch (Exception e) { Log.Write(e.ToString()); throw; }
    }
}
