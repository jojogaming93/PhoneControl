// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only





namespace PhoneControl.Application.Ports;

public interface ITouchSink
{
    // Logical IDs belong to gestures, independent of a backend's local IDs.
    // Coordinates are normalized to the visible phone display; 0=DOWN, 1=UP, 2=MOVE.
    void Touch(long pointer, byte action, double x, double y);
    void ReleaseAll();
}
