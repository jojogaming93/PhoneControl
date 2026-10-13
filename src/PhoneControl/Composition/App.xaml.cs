// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using PhoneControl.Adapters.Android;
using PhoneControl.Adapters.Configuration;
using PhoneControl.Adapters.Diagnostics;
using PhoneControl.Adapters.Ios;
using PhoneControl.Adapters.Persistence;
using PhoneControl.Adapters.Windows.Capture;
using PhoneControl.Adapters.Windows.Drivers;
using PhoneControl.Adapters.Windows.Input;
using PhoneControl.Presentation.Windows;

namespace PhoneControl.Composition;

public sealed partial class App : Microsoft.UI.Xaml.Application
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
                new("android", "Android Â· USB / scrcpy", _ => new AndroidPhoneBackend(), [new SamsungDriver()],
                    "Android setup: connect a data-capable USB cable, unlock the phone, enable USB debugging in Developer options, and accept Allow USB debugging. On Samsung, enable Developer options by tapping Build number seven times under About phone â†’ Software information. Missing Samsung drivers can use Repair Samsung USB driver; other vendors use their manufacturer's driver. Dismiss idle touch protection before playing."),
                new("iphone", "iPhone Â· WebDriverAgent (experimental)", endpoint => new IosPhoneBackend(endpoint, session => new IosMirrorWindow(session, session, () => session.GeometryRevision, session.ViewClosed)), [],
                    "iPhone setup: supply a dedicated, already running WebDriverAgent endpoint for an iPhone or an iOS Simulator on a Mac. Enter its reachable address above (a USB forward can expose it on localhost). PhoneControl does not install or start the Apple test runner. The viewer supports taps, swipes, long presses up to 2 seconds, and Home through complete gestures. Audio and continuous keyboard/mouse gameplay mapping are unavailable. An existing WDA session will not be taken over. No real iPhone has been tested yet.",
                    UsesAddress: true, SupportsRealtimeTouch: false, NormalizeAddress: endpoint => IosWdaClient.ValidateEndpoint(endpoint).AbsoluteUri)
            ], new JsonSettingsStore(JsonSettingsStore.DirectoryPath, migrateStable: !AppInfo.IsPreview),
                new WindowsDesktopInput(), new WindowsPngDecoder(), () => new MirrorFrameCapture(), new ApplicationLog());
            window.Activate();
        } catch (Exception e) { Log.Write(e.ToString()); throw; }
    }
}
