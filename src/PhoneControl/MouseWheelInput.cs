// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only

namespace PhoneControl;

internal sealed class MouseWheelInput
{
    private int remainder;
    private readonly Queue<int> notches = new();
    public void Add(int delta)
    {
        remainder += delta;
        while (Math.Abs(remainder) >= 120) {
            notches.Enqueue(remainder > 0 ? KeyNames.WheelUp : KeyNames.WheelDown);
            remainder -= Math.Sign(remainder) * 120;
        }
    }
    public bool TryPeek(out int key) => notches.TryPeek(out key);
    public void Consume() => notches.Dequeue();
    public void Reset() { remainder = 0; notches.Clear(); }
}
