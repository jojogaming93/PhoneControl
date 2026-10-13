// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

using PhoneControl.Application.Ports;
using PhoneControl.Domain.Phones;

namespace PhoneControl.Presentation.Windows;

// The viewer runs on the dashboard's UI thread. Only complete, explicit user
// gestures are sent; drawing a screenshot never injects an input event.
internal sealed class IosMirrorWindow : IPhoneGestureMirror
{
    private readonly Window window = new();
    private readonly Image image = new() { Stretch = Stretch.Uniform };
    private readonly Grid surface = new() { Background = new SolidColorBrush(Microsoft.UI.Colors.Black) };
    private readonly TextBlock message = new() { Text = "Click to tap; drag to swipe; hold up to 2 s for a long press.", TextWrapping = TextWrapping.Wrap };
    private readonly IPhoneDisplay session;
    private readonly IPhoneGestureInput gestures;
    private readonly Func<int> geometryRevision;
    private readonly Action viewClosed;
    private readonly Microsoft.UI.Dispatching.DispatcherQueue dispatcher;
    private volatile bool alive = true;
    private bool closing, presenting, interacting;
    private readonly object frameGate = new();
    private PhoneScreenshot? pending;
    private PhoneScreenshot? displayed;
    private (double X, double Y, long Started, int Revision, uint Pointer)? press;
    public nint Window { get; }
    public bool Alive => alive;
    internal IosMirrorWindow(IPhoneDisplay session, IPhoneGestureInput gestures, Func<int> geometryRevision, Action viewClosed) {
        this.session = session; this.gestures = gestures; this.geometryRevision = geometryRevision; this.viewClosed = viewClosed;
        dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        window.Title = "PhoneControl | iPhone (experimental)";
        window.AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "assets", "phonecontrol.ico"));
        window.AppWindow.Resize(new global::Windows.Graphics.SizeInt32(600, 850));
        Window = WinRT.Interop.WindowNative.GetWindowHandle(window);
        var layout = new Grid { RequestedTheme = ElementTheme.Dark };
        layout.RowDefinitions.Add(new() { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var home = new Button { Content = "Home", Margin = new Thickness(8) };
        home.Click += async (_, _) => await Interact(() => gestures.Home());
        surface.Children.Add(image); Grid.SetRow(surface, 1); Grid.SetRow(message, 2);
        message.Margin = new Thickness(8);
        layout.Children.Add(home); layout.Children.Add(surface); layout.Children.Add(message); window.Content = layout;
        surface.PointerPressed += (_, e) => {
            var point = e.GetCurrentPoint(surface);
            if (!alive || interacting || !point.Properties.IsLeftButtonPressed || displayed is null ||
                displayed.PointWidth != session.Width || displayed.PointHeight != session.Height) return;
            if (ImagePoint(point.Position.X, point.Position.Y) is { } pos) {
                press = (pos.X, pos.Y, Environment.TickCount64, geometryRevision(), point.PointerId);
                surface.CapturePointer(e.Pointer); e.Handled = true;
            }
        };
        surface.PointerReleased += async (_, e) => {
            var point = e.GetCurrentPoint(surface);
            if (press is not { } start || start.Pointer != point.PointerId) return;
            press = null; surface.ReleasePointerCapture(e.Pointer);
            if (start.Revision != geometryRevision() || ImagePoint(point.Position.X, point.Position.Y) is not { } end) return;
            int duration = (int)Math.Clamp(Environment.TickCount64 - start.Started, 40, 2000);
            bool tap = Math.Abs(start.X - end.X) < .01 && Math.Abs(start.Y - end.Y) < .01;
            var gesture = new PhoneGesture(start.X, start.Y, tap ? start.X : end.X, tap ? start.Y : end.Y, duration);
            await Interact(() => gestures.Gesture(gesture)); e.Handled = true;
        };
        surface.PointerCaptureLost += (_, _) => press = null;
        window.Activated += (_, e) => { if (e.WindowActivationState == WindowActivationState.Deactivated) press = null; };
        window.Closed += (_, _) => { alive = false; press = null; if (!closing) viewClosed(); };
        window.Activate();
    }
    internal static (double X, double Y)? MapPoint(double x, double y, double boxWidth, double boxHeight, int imageWidth, int imageHeight) {
        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(boxWidth) || !double.IsFinite(boxHeight) ||
            boxWidth <= 0 || boxHeight <= 0 || imageWidth <= 0 || imageHeight <= 0) return null;
        double scale = Math.Min(boxWidth / imageWidth, boxHeight / imageHeight);
        double w = imageWidth * scale, h = imageHeight * scale, left = (boxWidth - w) / 2, top = (boxHeight - h) / 2;
        if (x < left || y < top || x >= left + w || y >= top + h) return null;
        return ((x - left) / w, (y - top) / h);
    }
    private (double X, double Y)? ImagePoint(double x, double y) => displayed is { } frame ?
        MapPoint(x, y, surface.ActualWidth, surface.ActualHeight, frame.PixelWidth, frame.PixelHeight) : null;
    private async Task Interact(Func<Task> action) {
        if (!alive || interacting) return;
        interacting = true;
        try { message.Text = "Sending iPhone gestureâ€¦"; await action(); message.Text = "Click to tap; drag to swipe; hold up to 2 s for a long press."; }
        catch (Exception e) { message.Text = e.Message; }
        finally { interacting = false; }
    }
    public void Present(PhoneScreenshot frame) {
        // Bound both the dispatcher queue and retained PNGs to the current
        // decode plus the newest pending image, even if the UI is busy.
        lock (frameGate) {
            if (!alive) return;
            pending = frame;
            if (presenting) return;
            presenting = true;
        }
        if (!dispatcher.TryEnqueue(async () => {
            try {
                while (true) {
                    PhoneScreenshot next;
                    lock (frameGate) {
                        if (!alive || pending is null) { presenting = false; return; }
                        next = pending; pending = null;
                    }
                    using var stream = new InMemoryRandomAccessStream();
                    using (var writer = new DataWriter(stream)) { writer.WriteBytes(next.Png); await writer.StoreAsync(); writer.DetachStream(); }
                    stream.Seek(0);
                    var bitmap = new BitmapImage(); await bitmap.SetSourceAsync(stream);
                    if (alive) { image.Source = bitmap; displayed = next; }
                }
            } catch (Exception e) {
                lock (frameGate) { pending = null; presenting = false; }
                if (alive) message.Text = "iPhone image unavailable: " + e.Message;
            }
        })) { lock (frameGate) { pending = null; presenting = false; } }
    }
    public void Dispose() {
        if (!alive) return;
        closing = true; alive = false; press = null;
        lock (frameGate) pending = null;
        if (dispatcher.HasThreadAccess) window.Close(); else dispatcher.TryEnqueue(window.Close);
    }
}
