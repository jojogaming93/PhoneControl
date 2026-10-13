// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only



using PhoneControl.Domain.Phones;

namespace PhoneControl.Application.Ports;

// Completed gestures are distinct from a backend that can keep fingers down.
public interface IPhoneGestureInput
{
    Task Gesture(PhoneGesture gesture, CancellationToken cancellation = default);
    Task Home(CancellationToken cancellation = default);
}
