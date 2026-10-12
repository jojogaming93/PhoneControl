// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only

namespace PhoneControl;

public enum PhoneDeviceState { Ready, Unauthorized, Offline }
public enum PhoneTransport { Usb, Network, Emulator }
public sealed record PhoneDevice(string Id, PhoneDeviceState State, string Description, PhoneTransport Transport);
public sealed record PhoneConnectionOptions(int MaxFps, int MaxSize, int BitrateMbps, bool Audio);

// Ports describe application needs. Wire concrete adapters in Program/App.
public interface IPhoneBackend
{
    Task<IReadOnlyList<PhoneDevice>> DiscoverDevices();
    IPhoneSession CreateSession();
}

public interface ITouchSink
{
    // Logical IDs belong to gestures, independent of a backend's local IDs.
    // Coordinates are normalized to the visible phone display; 0=DOWN, 1=UP, 2=MOVE.
    void Touch(long pointer, byte action, double x, double y);
    void ReleaseAll();
}

// The host remains Windows: mirrors expose a native window for focus/overlays.
public interface IMirrorSurface
{
    nint MirrorWindow { get; }
    int TextureWidth { get; }
    int TextureHeight { get; }
}

public interface IPhoneInput : ITouchSink, IMirrorSurface
{
    int Width { get; }
    int Height { get; }
    void Back();
}

public interface IPhoneFrameSource
{
    // Encoded PNG; cancellation must stop and drain the capture operation.
    Task<byte[]> CaptureFrame(CancellationToken cancellation);
}

public interface IPhoneSession : IPhoneInput, IPhoneFrameSource, IDisposable
{
    bool Connected { get; }
    bool MirrorAlive { get; }
    event Action<string>? Statistics;
    // Release held touches before publishing new physical/texture dimensions.
    event Action? GeometryChanged;
    event Action<string>? Fault;
    Task Connect(PhoneDevice device, PhoneConnectionOptions options);
    // Release touches and clean up only resources owned by this session.
    Task Disconnect();
}

public interface IUsbDriverSupport
{
    string DisplayName { get; }
    bool HasMissingDriver();
    Task Install(CancellationToken cancellation);
}
