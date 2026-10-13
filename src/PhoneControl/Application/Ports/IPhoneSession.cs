// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only



using PhoneControl.Domain.Phones;

namespace PhoneControl.Application.Ports;

public interface IPhoneSession : IPhoneDisplay, IPhoneFrameSource, IDisposable
{
    // Continuous touch is an optional port. Completed-gesture adapters do not
    // implement ITouchSink or expose fake DOWN/MOVE/UP operations.
    IPhoneInput? RealtimeInput { get; }
    bool SupportsRealtimeTouch => RealtimeInput is not null;
    bool Connected { get; }
    bool MirrorAlive { get; }
    event Action<string>? Statistics;
    // Release held touches before publishing new physical/texture dimensions.
    event Action? GeometryChanged;
    event Action<string>? Fault;
    Task Connect(PhoneDevice device, PhoneConnectionOptions options, CancellationToken cancellation = default);
    // Release touches and clean up only resources owned by this session.
    Task Disconnect();
}
