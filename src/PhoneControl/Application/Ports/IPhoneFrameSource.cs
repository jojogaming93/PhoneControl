// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only





namespace PhoneControl.Application.Ports;

public interface IPhoneFrameSource
{
    // Encoded PNG; cancellation must stop and drain the capture operation.
    Task<byte[]> CaptureFrame(CancellationToken cancellation);
}
