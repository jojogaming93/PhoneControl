// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only





namespace PhoneControl.Application.Input;

internal sealed class MouseMotion
{
    private double x, y;
    private (double X, double Y)? absolute;
    public void Add(ushort flags, int dx, int dy, int desktopWidth, int desktopHeight)
    {
        if ((flags & 1) == 0) { x += dx; y += dy; absolute = null; return; }
        var point = (X: dx * (desktopWidth - 1) / 65535d, Y: dy * (desktopHeight - 1) / 65535d);
        if (absolute is { } last) { x += point.X - last.X; y += point.Y - last.Y; }
        absolute = point;
    }
    public (double X, double Y) Drain() { var result = (x, y); x = y = 0; return result; }
    public void Reset() { x = y = 0; absolute = null; }
}
