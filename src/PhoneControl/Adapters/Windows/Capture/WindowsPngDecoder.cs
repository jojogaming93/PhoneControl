// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only

using global::Windows.Graphics.Imaging;
using Windows.Storage.Streams;

using PhoneControl.Application.Ports;
using PhoneControl.Domain.Geometry;

namespace PhoneControl.Adapters.Windows.Capture;

internal sealed class WindowsPngDecoder : IFrameDecoder
{
    public Task<HudFrame> Decode(byte[] png, CancellationToken cancellation) => DecodeFrame(png, cancellation);
    internal static async Task<HudFrame> DecodeFrame(byte[] frame, CancellationToken cancellation = default)
    {
        using var buffer = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(buffer)) {
            writer.WriteBytes(frame); await writer.StoreAsync().AsTask(cancellation); writer.DetachStream();
        }
        buffer.Seek(0);
        var decoder = await BitmapDecoder.CreateAsync(BitmapDecoder.PngDecoderId, buffer).AsTask(cancellation);
        var pixels = await decoder.GetPixelDataAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore,
            new BitmapTransform(), ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage).AsTask(cancellation);
        return new(pixels.DetachPixelData(), (int)decoder.PixelWidth, (int)decoder.PixelHeight);
    }
}
