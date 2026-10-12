// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only

using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Text.Json;
using Windows.Storage.Pickers;
using Windows.UI;

namespace PhoneControl;

public sealed class MainWindow : Window
{
    private readonly Settings settings = Settings.Load();
    private readonly StackPanel body = new() { Spacing = 18 };
    private readonly TextBlock status = Text("Ready to connect", 14);
    private readonly TextBlock stats = Text("Choose a phone connection", 12);
    private readonly TextBlock hint = Text("", 13);
    private readonly TextBlock shortcuts = Text("", 12);
    private readonly TextBox diagnostics = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 360 };
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer uiTimer;
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer deviceTimer;
    private readonly CancellationTokenSource shutdown = new();
    private bool refreshing, repairingDriver, autoSuppressed;
    private readonly HashSet<IUsbDriverSupport> driverAttempts = new();
    private string? attemptedDeviceId;
    private readonly IReadOnlyList<PhoneBackendRegistration> backends;
    private PhoneBackendRegistration selectedBackend;
    private IPhoneBackend backend;
    private IReadOnlyList<IUsbDriverSupport> driverSupport => selectedBackend.Drivers;
    private IPhoneSession? session;
    private InputEngine? input;
    private DrivingModeMonitor? drivingMonitor;
    private bool editingDriving;
    private List<Binding> EditorBindings => editingDriving ? settings.Profile.Driving!.Bindings : settings.Profile.Bindings;
    private MappingOverlay? overlay;
    private RawInputReader? rawInput;
    private ComboBox? devices;
    private readonly List<PhoneDevice> discovered = new();
    private readonly Dictionary<Binding, (NumberBox X, NumberBox Y)> positionEditors = new();
    private int selected = -1;
    private int currentPage;
    private bool busy, closing;
    private TextBox? profileName;
    private NumberBox? joyX, joyY, lookX, lookY;
    private readonly Button connectButton = Button("Connect phone", null);

    public MainWindow(IReadOnlyList<PhoneBackendRegistration> backends)
    {
        this.backends = backends;
        selectedBackend = backends.FirstOrDefault(b => b.Key == settings.PhoneBackend) ?? backends[0];
        backend = selectedBackend.Create(settings.IPhoneEndpoint);
        settings.Profile.EnsureDrivingLayout();
        Title = "PhoneControl";
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "assets", "phonecontrol.ico"));
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1180, 880));
        SystemBackdrop = new MicaBackdrop();
        var root = new Grid { RequestedTheme = ElementTheme.Dark, Background = Brush(15, 18, 26) };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(238) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var side = new StackPanel { Spacing = 12, Padding = new Thickness(24, 32, 24, 24), Background = Brush(21, 25, 35) };
        side.Children.Add(new Image {
            Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(Path.Combine(AppContext.BaseDirectory, "assets", "phonecontrol-logo.png"))),
            Width = 88, Height = 88, HorizontalAlignment = HorizontalAlignment.Left
        });
        side.Children.Add(Text("PhoneControl", 25, true));
        side.Children.Add(Text("Your phone. Your controls.", 13));
        side.Children.Add(new Border { Height = 20 });
        foreach (var item in new[] { ("◉  Connection", 0), ("⌘  Controls", 1), ("⚙  Stream settings", 2), ("≡  Diagnostics", 3), ("ⓘ  About & license", 4) }) {
            int index = item.Item2;
            var nav = Button(item.Item1, (_, _) => Navigate(index)); nav.HorizontalAlignment = HorizontalAlignment.Stretch;
            nav.HorizontalContentAlignment = HorizontalAlignment.Left; side.Children.Add(nav);
        }
        side.Children.Add(new Border { Height = 28 });
        side.Children.Add(status); side.Children.Add(stats);
        side.Children.Add(shortcuts);
        side.Children.Add(new Border { Height = 32 });
        side.Children.Add(Text($"PhoneControl v{AppInfo.Version}", 13, true));
        side.Children.Add(Text(AppInfo.Copyright, 11));
        side.Children.Add(Text("Open source · GNU AGPLv3\nWinUI 3 · .NET 10\nAndroid USB · experimental iPhone", 11));
        root.Children.Add(side);
        var content = new ScrollViewer { Content = body, Padding = new Thickness(34, 28, 34, 32), HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Grid.SetColumn(content, 1); root.Children.Add(content); Content = root;
        Log.Message += OnLog;
        uiTimer = DispatcherQueue.CreateTimer(); uiTimer.Interval = TimeSpan.FromMilliseconds(33); uiTimer.Tick += (_, _) => UpdateOverlay(); uiTimer.Start();
        deviceTimer = DispatcherQueue.CreateTimer(); deviceTimer.Interval = TimeSpan.FromSeconds(2);
        deviceTimer.Tick += async (_, _) => await RefreshDevices(); deviceTimer.Start();
        AppWindow.Closing += async (_, e) => {
            if (closing) return;
            e.Cancel = true;
            closing = true; uiTimer.Stop(); deviceTimer.Stop(); shutdown.Cancel(); Log.Message -= OnLog;
            try { settings.Save(); } catch (Exception error) { Log.Write(error.Message); }
            await Disconnect();
            Close();
        };
        connectButton.Click += async (_, _) => { autoSuppressed = session is not null; attemptedDeviceId = null; await ToggleConnection(); };
        Navigate(0);
        _ = RefreshDevices();
    }

    private static SolidColorBrush Brush(byte r, byte g, byte b) => new(Color.FromArgb(255, r, g, b));
    private static TextBlock Text(string text, double size = 14, bool bold = false) => new() {
        Text = text, FontSize = size, TextWrapping = TextWrapping.Wrap, Foreground = Brush(219, 225, 238),
        FontWeight = bold ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal
    };
    private static Button Button(string text, RoutedEventHandler? click) {
        var b = new Button { Content = text, Padding = new Thickness(16, 10, 16, 10), CornerRadius = new CornerRadius(8) };
        if (click is not null) b.Click += click;
        return b;
    }
    private static StackPanel Row(params UIElement[] controls) {
        var p = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        foreach (var c in controls) p.Children.Add(c);
        return p;
    }
    private ComboBox KeyPicker(string header, string value, Action<string> changed)
    {
        var picker = new ComboBox { Header = header, Width = 105, ItemsSource = KeyNames.Choices.Where(k => !KeyNames.IsWheel(KeyNames.Parse(k))).ToArray(), SelectedItem = KeyNames.Canonical(value) };
        picker.SelectionChanged += (_, _) => {
            if (picker.SelectedItem is string key) { changed(key); ApplyLive(); }
        };
        return picker;
    }
    private FrameworkElement ActionKeyPicker(Binding binding)
    {
        static string Label(string value) {
            var names = value.Split(',');
            return names.Length == 1 ? KeyNames.Canonical(value) : $"{names.Length} selected";
        }
        var selectedKeys = KeyNames.ParseMany(binding.Key).ToHashSet();
        var choices = new ListView { ItemsSource = KeyNames.Choices, SelectionMode = ListViewSelectionMode.Multiple, Height = 300, Width = 230 };
        foreach (var key in KeyNames.Choices.Where(k => selectedKeys.Contains(KeyNames.Parse(k)))) choices.SelectedItems.Add(key);
        var dropdown = new DropDownButton {
            Content = Label(binding.Key), Width = 150, HorizontalContentAlignment = HorizontalAlignment.Left,
            Flyout = new Flyout { Content = choices }
        };
        dropdown.Flyout.Opened += (_, _) => {
            if (choices.SelectedItems.Count > 0) choices.ScrollIntoView(choices.SelectedItems[0]);
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(dropdown, binding.Name + " keys");
        ToolTipService.SetToolTip(dropdown, binding.Key);
        bool restoring = false;
        choices.SelectionChanged += (_, args) => {
            if (restoring) return;
            if (choices.SelectedItems.Count == 0) {
                restoring = true;
                foreach (var key in args.RemovedItems) choices.SelectedItems.Add(key);
                restoring = false;
                return;
            }
            binding.Key = string.Join(", ", KeyNames.Choices.Where(k => choices.SelectedItems.Contains(k)));
            dropdown.Content = Label(binding.Key);
            ToolTipService.SetToolTip(dropdown, binding.Key);
            ApplyLive();
        };
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(Text("Keys / mouse", 14)); panel.Children.Add(dropdown);
        return panel;
    }
    private static Border Card(UIElement child) => new() {
        Child = child, Padding = new Thickness(20), CornerRadius = new CornerRadius(12), Background = Brush(28, 33, 45),
        BorderBrush = Brush(43, 51, 67), BorderThickness = new Thickness(1)
    };
    private void Navigate(int page)
    {
        shortcuts.Text = selectedBackend.SupportsRealtimeTouch ?
            "Left Alt   capture / release mouse\nF8    edit mappings\nF9    pause / resume controls\nEsc   release mouse\nF11   fullscreen phone view" :
            "iPhone view\nClick   tap\nDrag   swipe\nHold   long press up to 2 s\nHome   return to Home screen";
        foreach (var reused in new FrameworkElement[] { hint, diagnostics, connectButton })
            if (reused.Parent is Panel parent) parent.Children.Remove(reused);
        currentPage = page; body.Children.Clear(); positionEditors.Clear(); joyX = joyY = lookX = lookY = null;
        switch (page) { case 0: ConnectionPage(); break; case 1: ControlsPage(); break; case 2: SettingsPage(); break; case 3: DiagnosticsPage(); break; case 4: AboutPage(); break; }
    }
    private void Heading(string title, string subtitle)
    {
        body.Children.Add(Text(title, 30, true)); body.Children.Add(Text(subtitle, 14));
    }
    private void ConnectionPage()
    {
        Heading("A direct line to your phone", "Android USB controls and an experimental iPhone screenshot view.");
        var card = new StackPanel { Spacing = 16 };
        card.Children.Add(Text("01  CONNECT YOUR DEVICE", 12, true));
        var platform = new ComboBox { Header = "Phone connection", ItemsSource = backends.Select(b => b.Label).ToArray(),
            SelectedIndex = backends.ToList().IndexOf(selectedBackend), IsEnabled = session is null && !busy };
        platform.SelectionChanged += async (_, _) => {
            if (session is not null || busy || repairingDriver) { hint.Text = "Disconnect before changing the phone connection."; platform.SelectedIndex = backends.ToList().IndexOf(selectedBackend); return; }
            if (platform.SelectedIndex < 0) return;
            selectedBackend = backends[platform.SelectedIndex]; backend = selectedBackend.Create(settings.IPhoneEndpoint);
            settings.PhoneBackend = selectedBackend.Key; attemptedDeviceId = null; autoSuppressed = false;
            discovered.Clear(); Save(); Navigate(0); hint.Text = "Checking the selected phone connection…"; await RefreshDevices();
        };
        card.Children.Add(platform);
        if (selectedBackend.UsesAddress) {
            var endpoint = new TextBox { Header = "Server address", Text = settings.IPhoneEndpoint, IsEnabled = session is null && !busy };
            card.Children.Add(endpoint);
            card.Children.Add(Button("Use address", async (_, _) => {
                if (session is not null || busy || repairingDriver) return;
                try {
                    settings.IPhoneEndpoint = selectedBackend.NormalizeAddress?.Invoke(endpoint.Text) ?? endpoint.Text.Trim();
                    backend = selectedBackend.Create(settings.IPhoneEndpoint); discovered.Clear(); attemptedDeviceId = null;
                    Save(); PopulateDevices(); await RefreshDevices();
                } catch (Exception e) { hint.Text = e.Message; }
            }));
        }
        devices = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, PlaceholderText = "Choose a phone" };
        PopulateDevices(); card.Children.Add(devices);
        card.Children.Add(Row(connectButton, Button("Refresh devices", async (_, _) => { attemptedDeviceId = null; await RefreshDevices(); }),
            Button("Show phone", (_, _) => { if (session?.MirrorWindow > 0) Native.SetForegroundWindow(session.MirrorWindow); })));
        card.Children.Add(hint); body.Children.Add(Card(card));
        if (selectedBackend.SupportsRealtimeTouch) {
        var automatic = new ToggleSwitch { Header = "Connect automatically when one authorized USB phone is found", IsOn = settings.AutoConnect };
        automatic.Toggled += (_, _) => { settings.AutoConnect = automatic.IsOn; autoSuppressed = false; attemptedDeviceId = null; Save(); };
        body.Children.Add(automatic);
        var gameplay = new ComboBox { Header = "Control layout", Width = 290 };
        foreach (string value in new[] { "Auto", "On foot", "Driving" }) gameplay.Items.Add(value);
        gameplay.SelectedItem = settings.GameplayMode is "On foot" or "Driving" ? settings.GameplayMode : "Auto";
        gameplay.SelectionChanged += (_, _) => { settings.GameplayMode = (string)gameplay.SelectedItem; input?.SetModeOverride(settings.GameplayMode); Save(); };
        body.Children.Add(gameplay);
        body.Children.Add(Text("Auto checks the active phone view every 50 ms and switches as soon as the vehicle arrows appear. Driving has its own editable mappings. If you change the game HUD, use a manual layout here.", 12));
        }
        var repairs = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        foreach (var driver in driverSupport)
            repairs.Children.Add(Button("Repair " + driver.DisplayName, async (_, _) => await RepairDriver(driver)));
        repairs.Children.Add(Button("Open offline guide", (_, _) => {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "docs", "index.html")) { UseShellExecute = true });
        }));
        body.Children.Add(repairs);
        if (selectedBackend.SupportsRealtimeTouch) body.Children.Add(Card(Text($"02  PLAY\n\nClick the phone view to give it focus. Left Alt captures your mouse for aiming. Move with {settings.Profile.Up} / {settings.Profile.Left} / {settings.Profile.Down} / {settings.Profile.Right}; mouse buttons activate your mapped actions. Press Esc to release your mouse and click menus normally.\n\nF8 puts the phone view into mapping edit mode. Select or drag the circles to match your HUD. F9 pauses all touch controls.", 15)));
        body.Children.Add(Card(Text(selectedBackend.SetupHelp, 14)));
    }
    private async Task RefreshDevices()
    {
        if (closing || refreshing || busy || repairingDriver || session is not null) return;
        refreshing = true;
        var discoveringBackend = backend;
        try {
            var found = await discoveringBackend.DiscoverDevices();
            if (closing || repairingDriver || !ReferenceEquals(discoveringBackend, backend)) return;
            if (attemptedDeviceId is not null && !found.Any(d => d.Id == attemptedDeviceId)) { attemptedDeviceId = null; autoSuppressed = false; }
            string? selectedDeviceId = devices?.SelectedIndex is int index && index >= 0 && index < discovered.Count ? discovered[index].Id : null;
            bool changed = !discovered.SequenceEqual(found);
            discovered.Clear(); discovered.AddRange(found);
            if (changed) { PopulateDevices(); if (selectedDeviceId is not null && devices is not null) { int old = discovered.FindIndex(d => d.Id == selectedDeviceId); if (old >= 0) devices.SelectedIndex = old; } }
            hint.Text = discovered.Count == 0 ? "Waiting for a phone. Check the setup instructions for this connection." :
                discovered.Any(d => d.State == PhoneDeviceState.Ready) ? "Phone ready. Connect to start streaming." :
                discovered.Any(d => d.State == PhoneDeviceState.Unauthorized) ? "Accept ‘Allow USB debugging?’ on the phone. Detection continues automatically." :
                selectedBackend.UsesAddress ? "The phone server is not ready. Check the address and setup instructions." : "Phone is offline. Unlock it and reconnect the USB cable.";
            foreach (var driver in driverSupport) {
                if (closing) return;
                if (discovered.Count == 0 && !driverAttempts.Contains(driver) && await Task.Run(driver.HasMissingDriver) && ReferenceEquals(discoveringBackend, backend))
                    await RepairDriver(driver);
            }
            if (!closing && settings.AutoConnect && !autoSuppressed && ConnectionPolicy.CanAutoConnect(discovered) &&
                attemptedDeviceId != discovered[0].Id && currentPage == 0) {
                attemptedDeviceId = discovered[0].Id;
                await ToggleConnection();
            }
        } catch (Exception e) { if (ReferenceEquals(discoveringBackend, backend)) { hint.Text = e.Message; Log.Write(e.Message); } }
        finally {
            refreshing = false;
            if (!closing && !ReferenceEquals(discoveringBackend, backend)) _ = RefreshDevices();
        }
    }
    private async Task RepairDriver(IUsbDriverSupport driver)
    {
        if (closing || repairingDriver || busy || session is not null) return;
        repairingDriver = true; driverAttempts.Add(driver); connectButton.IsEnabled = false;
        hint.Text = $"Downloading the official {driver.DisplayName}. Windows may ask for administrator approval…";
        try {
            await driver.Install(shutdown.Token);
            if (!closing) hint.Text = "Driver setup finished. Reconnect USB if needed, then approve USB debugging on the handset.";
        } catch (OperationCanceledException) { if (!closing) hint.Text = $"Driver download cancelled. You can retry Repair {driver.DisplayName}."; }
        catch (Exception e) { if (!closing) hint.Text = "Driver repair needs attention: " + e.Message; Log.Write(e.ToString()); }
        finally { repairingDriver = false; if (!closing) connectButton.IsEnabled = true; }
    }
    private void PopulateDevices()
    {
        if (devices is null || currentPage != 0) return;
        devices.Items.Clear(); foreach (var d in discovered) devices.Items.Add($"{d.Id} • {d.State} • {d.Description.Replace('_', ' ')}");
        if (discovered.Count > 0) devices.SelectedIndex = Math.Max(0, discovered.FindIndex(d => d.State == PhoneDeviceState.Ready));
    }
    private async Task ToggleConnection()
    {
        if (busy || repairingDriver || closing) return;
        busy = true; connectButton.IsEnabled = false;
        try {
            if (session is not null) { await Disconnect(); return; }
            int index = devices?.SelectedIndex ?? -1;
            if (index < 0 || index >= discovered.Count) throw new IOException("Select an authorized phone first.");
            settings.Profile.Validate(); settings.Save(); status.Text = "Connecting…"; hint.Text = "Opening the phone view and control session…";
            var pending = backend.CreateSession();
            pending.Statistics += line => DispatcherQueue.TryEnqueue(() => stats.Text = line);
            try { await pending.Connect(discovered[index], new(settings.MaxFps, settings.MaxSize, settings.BitrateMbps, settings.Audio), shutdown.Token); }
            catch { try { await pending.Disconnect(); } finally { pending.Dispose(); } throw; }
            if (closing) { await pending.Disconnect(); pending.Dispose(); return; }
            session = pending;
            session.Fault += error => DispatcherQueue.TryEnqueue(async () => { if (!closing && ReferenceEquals(session, pending) && !busy) { Log.Write(error); await Disconnect(); hint.Text = error; } });
            if (session.SupportsRealtimeTouch) {
                input = new InputEngine(session.RealtimeInput!, settings.Profile);
                input.SetModeOverride(settings.GameplayMode);
                input.StateChanged += () => DispatcherQueue.TryEnqueue(UpdateStatus);
                input.EditPoint += (x, y, first) => DispatcherQueue.TryEnqueue(() => PlaceMapping(x, y, first));
                session.GeometryChanged += input.Reset;
                overlay = new MappingOverlay();
                rawInput = new RawInputReader(WinRT.Interop.WindowNative.GetWindowHandle(this), input); input.Start();
                drivingMonitor = new DrivingModeMonitor(session, session, input);
                hint.Text = "Connected. Click the phone view, then press Left Alt to aim."; UpdateStatus();
            } else {
                status.Text = "iPhone • completed gestures";
                hint.Text = "Connected. Use the iPhone view to tap, drag, long-press or go Home. Continuous gameplay mappings are unavailable.";
            }
            connectButton.Content = "Disconnect";
            Native.SetForegroundWindow(session.MirrorWindow);
        } catch (Exception e) {
            if (session is not null) await Disconnect();
            hint.Text = e.Message; status.Text = "Connection needs attention"; Log.Write(e.ToString());
        }
        finally { busy = false; connectButton.IsEnabled = true; }
    }
    private async Task Disconnect()
    {
        var monitor = drivingMonitor; drivingMonitor = null;
        // Release fingers/capture immediately, then await this session's capture
        // worker before its device resources are removed.
        rawInput?.Dispose(); rawInput = null; input?.Dispose(); input = null; overlay?.Dispose(); overlay = null;
        var old = session; session = null;
        if (monitor is not null) await monitor.Stop();
        if (old is not null) { try { await old.Disconnect(); } finally { old.Dispose(); } }
        connectButton.Content = "Connect phone"; status.Text = "Disconnected"; stats.Text = "Choose a phone connection";
    }
    private void UpdateStatus()
    {
        if (input is null) return;
        if (input.Editing && editingDriving != input.DisplayDriving) {
            editingDriving = input.DisplayDriving; selected = -2;
            if (currentPage == 1) Navigate(1);
        }
        status.Text = input.Editing ? "Editing mappings" : input.Paused ? "Controls paused" :
            (input.Driving ? "Driving" : "On foot") + (input.Aiming ? " • mouse captured" : " • mouse free");
    }
    private void UpdateOverlay()
    {
        if (input is null || session is null || overlay is null) return;
        overlay.Update(input.Viewport, input.Snapshot(), input.Editing, input.Aiming, selected,
            Native.GetForegroundWindow() == session.MirrorWindow && !Native.IsIconic(session.MirrorWindow), input.DisplayDriving);
    }
    private NumberBox Number(string label, double value, double min, double max, Action<double> changed, double width = 110)
    {
        var n = new NumberBox { Header = label, Value = value, Minimum = min, Maximum = max, Width = width, SmallChange = 1,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        n.ValueChanged += (_, e) => { if (double.IsFinite(e.NewValue)) changed(e.NewValue); };
        return n;
    }
    private void ApplyLive()
    {
        try { settings.Profile.Validate(); input?.SetProfile(settings.Profile); }
        catch (InvalidDataException e) { hint.Text = e.Message; }
    }
    private void Edit(int selection)
    {
        selected = selection;
        if (session is not null && !session.SupportsRealtimeTouch) { hint.Text = "This backend uses completed gestures in its viewer. Gameplay mapping is unavailable."; return; }
        if (input is null || session is null) { hint.Text = "Connect your phone first, then use Pick on phone."; return; }
        input.SetEditing(true, editingDriving); Native.SetForegroundWindow(session.MirrorWindow);
    }
    private void ControlsPage()
    {
        Heading("Make every key feel right", "Positions are percentages of the phone screen. Drag markers on the live view or enter coordinates here.");
        if (!selectedBackend.SupportsRealtimeTouch) body.Children.Add(Text("These saved mappings are for continuous gameplay backends. The iPhone viewer uses completed pointer gestures; gameplay mapping is unavailable for this connection.", 14));
        profileName = new TextBox { Header = "Profile name", Text = settings.Profile.Name };
        profileName.TextChanged += (_, _) => settings.Profile.Name = profileName.Text;
        body.Children.Add(profileName);
        var layout = new ComboBox { Header = "Edit control layout", Width = 240 };
        layout.Items.Add("On foot"); layout.Items.Add("Driving"); layout.SelectedIndex = editingDriving ? 1 : 0;
        layout.SelectionChanged += (_, _) => { editingDriving = layout.SelectedIndex == 1; selected = -2; input?.SetEditing(false); Navigate(1); };
        body.Children.Add(layout);
        body.Children.Add(Row(Button("Save profile", (_, _) => Save()), Button("Import…", async (_, _) => await Import()),
            Button("Export…", async (_, _) => await Export()), Button("Edit on phone · F8", (_, _) => Edit(selected))));
        body.Children.Add(hint);
        var movement = new StackPanel { Spacing = 12 };
        movement.Children.Add(Text("MOVEMENT & CAMERA", 12, true));
        joyX = Number("Move X %", settings.Profile.JoystickX * 100, 0, 100, v => { settings.Profile.JoystickX = v / 100; ApplyLive(); });
        joyY = Number("Move Y %", settings.Profile.JoystickY * 100, 0, 100, v => { settings.Profile.JoystickY = v / 100; ApplyLive(); });
        if (!editingDriving) movement.Children.Add(Row(joyX, joyY, Number("Radius %", settings.Profile.JoystickRadius * 100, 1, 30, v => { settings.Profile.JoystickRadius = v / 100; ApplyLive(); }), Button("Pick movement", (_, _) => Edit(-1))));
        lookX = Number("Look X %", settings.Profile.LookX * 100, 0, 100, v => { settings.Profile.LookX = v / 100; ApplyLive(); });
        lookY = Number("Look Y %", settings.Profile.LookY * 100, 0, 100, v => { settings.Profile.LookY = v / 100; ApplyLive(); });
        movement.Children.Add(Row(lookX, lookY, Number("Sensitivity", settings.Profile.Sensitivity, .1, 10, v => { settings.Profile.Sensitivity = v; ApplyLive(); }), Button("Pick camera", (_, _) => Edit(-2))));
        var keys = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        foreach (var spec in new[] { ("Forward", settings.Profile.Up, (Action<string>)(v => settings.Profile.Up = v)), ("Back", settings.Profile.Down, (Action<string>)(v => settings.Profile.Down = v)),
            ("Left", settings.Profile.Left, (Action<string>)(v => settings.Profile.Left = v)), ("Right", settings.Profile.Right, (Action<string>)(v => settings.Profile.Right = v)) }) {
            keys.Children.Add(KeyPicker(spec.Item1, spec.Item2, spec.Item3));
        }
        if (!editingDriving) movement.Children.Add(keys); body.Children.Add(Card(movement));
        var sprint = new StackPanel { Spacing = 12 };
        sprint.Children.Add(Text("SPRINT", 12, true));
        var sprintKey = KeyPicker("Hold key", settings.Profile.SprintKey, v => settings.Profile.SprintKey = v);
        sprint.Children.Add(Row(sprintKey, Number("Sprint reach %", settings.Profile.SprintRadius * 100, 1, 45,
            v => { settings.Profile.SprintRadius = v / 100; ApplyLive(); }, 155)));
        sprint.Children.Add(Text("Hold the sprint key while moving forward to drag the movement joystick farther. Release it to return to normal movement. Autorun is a separate tap action below, bound to = by default.", 12));
        if (!editingDriving) body.Children.Add(Card(sprint));
        else body.Children.Add(Text("Driving uses separate held accelerator, reverse and steering buttons. Movement keys initially follow your walking layout. Shift boosts, Space brakes and E exits by default. Edit driving keys and positions below; walking mappings are saved separately.", 12));
        body.Children.Add(Text("ACTION MAPPINGS", 12, true));
        for (int i = 0; i < EditorBindings.Count; i++) {
            var binding = EditorBindings[i]; int index = i;
            var name = new TextBox { Header = "Action", Text = binding.Name, Width = 150 };
            name.TextChanged += (_, _) => { binding.Name = name.Text; ApplyLive(); };
            var key = ActionKeyPicker(binding);
            var x = Number("X %", binding.X * 100, 0, 100, v => { binding.X = v / 100; ApplyLive(); }, 85);
            var y = Number("Y %", binding.Y * 100, 0, 100, v => { binding.Y = v / 100; ApplyLive(); }, 85);
            positionEditors[binding] = (x, y);
            var mode = new ComboBox { Header = "Behavior", Width = 88 }; mode.Items.Add("Hold"); mode.Items.Add("Tap"); mode.SelectedItem = binding.Mode;
            mode.SelectionChanged += (_, _) => { binding.Mode = (string)mode.SelectedItem; ApplyLive(); };
            var pick = Button("Pick", (_, _) => Edit(index)); pick.VerticalAlignment = VerticalAlignment.Bottom;
            var remove = Button("×", (_, _) => { EditorBindings.Remove(binding); selected = -2; ApplyLive(); Navigate(1); }); remove.VerticalAlignment = VerticalAlignment.Bottom;
            body.Children.Add(Card(Row(name, key, x, y, mode, pick, remove)));
        }
        body.Children.Add(Button("+ Add action", (_, _) => {
            string? key = Enumerable.Range('A', 26).Select(v => ((char)v).ToString()).FirstOrDefault(k =>
                !EditorBindings.Any(b => KeyNames.ParseMany(b.Key).Contains(KeyNames.Parse(k))) && (editingDriving || !new[] { settings.Profile.Up, settings.Profile.Down, settings.Profile.Left, settings.Profile.Right, settings.Profile.SprintKey }.Contains(k)));
            if (key is null || EditorBindings.Count >= 64) { hint.Text = "No unused letter remains. Change a key or remove an action first."; return; }
            EditorBindings.Add(new Binding { Key = key }); ApplyLive(); Navigate(1);
        }));
        body.Children.Add(Text("Select keys from the dropdowns. Action dropdowns allow several keys for the same action, including MouseWheelUp and MouseWheelDown. Each input can be assigned once within each layout. Mouse mappings apply while captured. Hold lasts until all selected keys are released; Tap and wheel notches create 40 ms presses. Up to ten simultaneous touches.\n\nStarter positions match the observed example HUD. Adjust changed or contextual controls with Pick.", 12));
    }
    private void PlaceMapping(double x, double y, bool first)
    {
        if (input?.Editing != true) return;
        var p = settings.Profile;
        // F8 edits the detected layout; Pick can deliberately edit either one.
        bool driving = input.DisplayDriving;
        var bindings = driving ? p.Driving!.Bindings : p.Bindings;
        if (first) {
            var candidates = new List<(int I, double X, double Y)> { (-2, p.LookX, p.LookY) };
            if (!driving) candidates.Add((-1, p.JoystickX, p.JoystickY));
            candidates.AddRange(bindings.Select((b, i) => (i, b.X, b.Y)));
            double aspect = (double)(session?.Width ?? 2340) / (session?.Height ?? 1080);
            var nearest = candidates.OrderBy(c => Math.Pow((x - c.X) * aspect, 2) + Math.Pow(y - c.Y, 2)).First();
            if (Math.Sqrt(Math.Pow((x - nearest.X) * aspect, 2) + Math.Pow(y - nearest.Y, 2)) < .055) selected = nearest.I;
        }
        if (selected == -1 && !driving) { p.JoystickX = x; p.JoystickY = y; if (joyX is not null) joyX.Value = Math.Round(x * 100, 2); if (joyY is not null) joyY.Value = Math.Round(y * 100, 2); }
        else if (selected == -2) { p.LookX = x; p.LookY = y; if (lookX is not null) lookX.Value = Math.Round(x * 100, 2); if (lookY is not null) lookY.Value = Math.Round(y * 100, 2); }
        else if (selected >= 0 && selected < bindings.Count) {
            var b = bindings[selected]; b.X = x; b.Y = y;
            if (positionEditors.TryGetValue(b, out var editors)) { editors.X.Value = Math.Round(x * 100, 2); editors.Y.Value = Math.Round(y * 100, 2); }
        }
        ApplyLive();
    }
    private void SettingsPage()
    {
        Heading("Tune the stream", "Changes apply on the next connection. Start with 1920 px, H.264, and a 120 FPS cap.");
        if (!selectedBackend.SupportsRealtimeTouch) {
            body.Children.Add(Card(Text("iPhone uses sequential PNG screenshots at up to 8 refreshes/s and completed gestures. H.264 size/bitrate and audio settings apply to Android; iPhone has no audio stream or continuous game input. The actual refresh rate depends on WebDriverAgent and the device.", 14)));
            return;
        }
        var card = new StackPanel { Spacing = 18 };
        card.Children.Add(Number("Frame rate cap", settings.MaxFps, 30, 240, v => settings.MaxFps = (int)v, 220));
        card.Children.Add(Number("Maximum edge (pixels)", settings.MaxSize, 720, 3840, v => settings.MaxSize = (int)v, 220));
        card.Children.Add(Number("Video bitrate (Mbps)", settings.BitrateMbps, 2, 64, v => settings.BitrateMbps = (int)v, 220));
        var audio = new ToggleSwitch { Header = "Phone audio", IsOn = settings.Audio }; audio.Toggled += (_, _) => settings.Audio = audio.IsOn; card.Children.Add(audio);
        card.Children.Add(Button("Save settings", (_, _) => Save())); body.Children.Add(Card(card));
        body.Children.Add(Card(Text("Video buffering: 0 ms added\nInput sampling: every 8 ms (125 Hz)\nTransport: USB / persistent scrcpy control socket\nCodec: H.264\n\nFor more detail, increase resolution. If the phone gets hot or frames drop, reduce resolution or the FPS cap. Audio uses a 30 ms buffer; turn it off if unnecessary. The game’s rendering rate and phone encoder limit the stream.\n\nLatency is not a measured end-to-end number here. The frame counter measures delivered frames, not input-to-display delay.", 14)));
    }
    private void DiagnosticsPage()
    {
        Heading("Connection diagnostics", "Local logs help troubleshoot USB, video, and touch control errors.");
        body.Children.Add(Row(Button("Refresh phones", async (_, _) => await RefreshDevices()), Button("Open log folder", (_, _) => {
            Directory.CreateDirectory(Settings.DirectoryPath); System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Settings.DirectoryPath) { UseShellExecute = true });
        })));
        body.Children.Add(hint);
        var trace = new ToggleSwitch { Header = "Log injected touch press/release events", IsOn = Log.TraceTouches };
        trace.Toggled += (_, _) => Log.TraceTouches = trace.IsOn; body.Children.Add(trace);
        body.Children.Add(diagnostics);
        body.Children.Add(Text("Logs and profiles stay in %LOCALAPPDATA%\\PhoneControl. No account, license key, subscription, or network service is required for gameplay.", 12));
    }
    private void AboutPage()
    {
        Heading("About PhoneControl", $"Version {AppInfo.Version}");
        var ownership = new StackPanel { Spacing = 12 };
        ownership.Children.Add(Text(AppInfo.Copyright, 20, true));
        ownership.Children.Add(new HyperlinkButton { Content = "GitHub: jojogaming93", NavigateUri = new Uri("https://github.com/jojogaming93") });
        ownership.Children.Add(Text("PhoneControl is free software under the GNU Affero General Public License version 3 (AGPL-3.0-only). You may use, modify and redistribute it under that license. Distributed derivatives must provide corresponding source, and modified versions used over a network must offer that source to their users. Provided without warranty; see the license for details.", 14));
        ownership.Children.Add(Button("Read PhoneControl license", (_, _) => OpenNotice("LICENSE")));
        ownership.Children.Add(Button("Open corresponding source", (_, _) => {
            try {
                string path = Path.Combine(AppContext.BaseDirectory, "source", "PhoneControl-source.zip");
                if (!File.Exists(path)) throw new FileNotFoundException("Published releases include source/PhoneControl-source.zip. This development build has no release source archive.");
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
            } catch (Exception e) { hint.Text = "Could not open source: " + e.Message; }
        }));
        body.Children.Add(Card(ownership));
        var thirdParty = new StackPanel { Spacing = 12 };
        thirdParty.Children.Add(Text("Third-party software", 20, true));
        thirdParty.Children.Add(Text("Built with WinUI 3, .NET 10 and scrcpy 4.1. Third-party components, including the Apache-2.0 control server, keep their license notices.", 14));
        thirdParty.Children.Add(Button("Read third-party notices", (_, _) => OpenNotice("THIRD-PARTY-NOTICES.md")));
        body.Children.Add(Card(thirdParty));
        body.Children.Add(hint);
    }
    private void OpenNotice(string name)
    {
        try {
            string path = Path.Combine(AppContext.BaseDirectory, name);
            if (!File.Exists(path)) throw new FileNotFoundException("The notice is missing from this installation.", name);
            // Not all Windows installations associate extensionless or Markdown files.
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("notepad.exe") {
                ArgumentList = { path }, UseShellExecute = false
            });
        } catch (Exception e) { hint.Text = "Could not open notice: " + e.Message; }
    }
    private void Save()
    {
        try { settings.Save(); ApplyLive(); hint.Text = "Saved. Your profile will load next time."; }
        catch (Exception e) { hint.Text = e.Message; }
    }
    private async Task Import()
    {
        try {
            var picker = new FileOpenPicker(); WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
            picker.FileTypeFilter.Add(".json"); var file = await picker.PickSingleFileAsync(); if (file is null) return;
            var p = JsonSerializer.Deserialize<Profile>(await File.ReadAllTextAsync(file.Path)) ?? throw new InvalidDataException("Empty profile.");
            p.UpgradeSprintMapping(); p.Validate(); p.EnsureDrivingLayout();
            settings.Profile = p; selected = -1; ApplyLive(); settings.Save(); Navigate(1); hint.Text = "Profile imported.";
        } catch (Exception e) { hint.Text = e.Message; }
    }
    private async Task Export()
    {
        try {
            settings.Profile.Validate();
            var picker = new FileSavePicker(); WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
            picker.SuggestedFileName = "phonecontrol-controls"; picker.FileTypeChoices.Add("PhoneControl profile", new List<string> { ".json" });
            var file = await picker.PickSaveFileAsync(); if (file is null) return;
            await File.WriteAllTextAsync(file.Path, JsonSerializer.Serialize(settings.Profile, new JsonSerializerOptions { WriteIndented = true })); hint.Text = "Profile exported.";
        } catch (Exception e) { hint.Text = e.Message; }
    }
    private void OnLog(string line) => DispatcherQueue.TryEnqueue(() => {
        if (closing) return;
        diagnostics.Text += line + Environment.NewLine;
        if (diagnostics.Text.Length > 16000) diagnostics.Text = diagnostics.Text[^12000..];
    });
}
