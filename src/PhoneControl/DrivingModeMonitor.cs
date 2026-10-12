// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only

using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace PhoneControl;

public sealed class DrivingModeMonitor
{
    private readonly CancellationTokenSource stop = new();
    private readonly Task loop;
    internal const int PollMilliseconds = 50;
    public DrivingModeMonitor(IMirrorSurface surface, IPhoneFrameSource frames, InputEngine input)
    {
        loop = Task.Run(async () => {
            using var mirror = new MirrorFrameCapture();
            await Run(input, () => mirror.TryCapture(surface), async cancellation => {
                var pixels = await DecodeFrame(await frames.CaptureFrame(cancellation), cancellation);
                return new HudFrame(pixels.Pixels, pixels.Width, pixels.Height);
            }, stop.Token);
        });
    }
    internal DrivingModeMonitor(InputEngine input, Func<HudFrame?> mirror, Func<CancellationToken, Task<HudFrame>> phone)
        => loop = Task.Run(() => Run(input, mirror, phone, stop.Token));

    private static async Task Run(InputEngine input, Func<HudFrame?> mirror, Func<CancellationToken, Task<HudFrame>> phone, CancellationToken cancellation)
    {
        var detector = new DrivingHudDetector();
        Task<HudFrame>? pending = null;
        long nextPhoneCapture = 0;
        int failures = 0;
        try {
            while (!cancellation.IsCancellationRequested) {
                long started = Environment.TickCount64;
                bool automatic = !input.Editing && input.ModeOverride == "Auto";
                HudFrame? frame = null;
                try {
                    if (automatic) frame = mirror();
                    if (pending is { IsCompleted: true }) {
                        var completed = pending;
                        pending = null;
                        nextPhoneCapture = Environment.TickCount64 + 1000;
                        // A phone screenshot may be older than the visible mirror.
                        try {
                            var captured = await completed;
                            if (automatic && frame is null) frame = captured;
                        } catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                        catch (Exception error) { if (failures++ == 0) Log.Write("Driving HUD detection unavailable: " + error.Message); }
                    }
                    if (frame is HudFrame pixels && !cancellation.IsCancellationRequested && !input.Editing && input.ModeOverride == "Auto") {
                        input.SetDetectedDriving(detector.Observe(detector.IsDrivingFrame(pixels.Pixels, pixels.Width, pixels.Height)));
                        failures = 0;
                    }
                } catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                catch (Exception error) { if (failures++ == 0) Log.Write("Driving HUD detection unavailable: " + error.Message); }
                if (automatic && frame is null && pending is null && Environment.TickCount64 >= nextPhoneCapture) {
                    // Never await the slow fallback here: focus can return while
                    // an ADB screenshot is still in flight. One request at a time.
                    pending = Task.Run(() => phone(cancellation), cancellation);
                }
                int remaining = PollMilliseconds - (int)(Environment.TickCount64 - started);
                await Task.Delay(Math.Max(1, remaining), cancellation);
            }
        } catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        finally {
            if (pending is not null) {
                try { await pending; }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
                catch (Exception error) { Log.Write("Driving HUD capture stopped: " + error.Message); }
            }
        }
    }
    internal static async Task<(byte[] Pixels, int Width, int Height)> DecodeFrame(byte[] frame, CancellationToken cancellation = default)
    {
        using var buffer = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(buffer)) {
            writer.WriteBytes(frame); await writer.StoreAsync().AsTask(cancellation); writer.DetachStream();
        }
        buffer.Seek(0);
        var decoder = await BitmapDecoder.CreateAsync(BitmapDecoder.PngDecoderId, buffer).AsTask(cancellation);
        var pixels = await decoder.GetPixelDataAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore,
            new BitmapTransform(), ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage).AsTask(cancellation);
        return (pixels.DetachPixelData(), (int)decoder.PixelWidth, (int)decoder.PixelHeight);
    }
    public async Task Stop()
    {
        stop.Cancel(); await loop; stop.Dispose();
    }
}
