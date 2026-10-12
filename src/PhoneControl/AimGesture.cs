// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only

namespace PhoneControl;

// The camera owns one finger for the entire aim session. Button gestures use
// other IDs and cannot replace or release it.
internal sealed class AimGesture(ITouchSink sink)
{
    private bool active;
    private double x, y;
    public void Begin(Profile p)
    {
        if (active) return;
        x = p.LookX; y = p.LookY; sink.Touch(1, 0, x, y); active = true;
    }
    public void Move(Profile p, double dx, double dy, int width, int height)
    {
        if (dx == 0 && dy == 0) return;
        Begin(p);
        double sx = dx * p.Sensitivity / width, sy = dy * p.Sensitivity / height;
        int steps = (int)Math.Ceiling(Math.Max(Math.Abs(sx) / .05, Math.Abs(sy) / .08));
        for (int i = 0; i < steps; i++) {
            double nx = x + sx / steps, ny = y + sy / steps;
            if (Math.Abs(nx - p.LookX) > .17 || Math.Abs(ny - p.LookY) > .25 || nx is < .01 or > .99 || ny is < .01 or > .99) {
                sink.Touch(1, 1, x, y);
                x = p.LookX; y = p.LookY; sink.Touch(1, 0, x, y);
            }
            // A look anchor near an edge still has to remain on the screen.
            x = Math.Clamp(x + sx / steps, 0, 1); y = Math.Clamp(y + sy / steps, 0, 1);
            sink.Touch(1, 2, x, y);
        }
    }
    public void Reset() => active = false; // The input port's ReleaseAll already lifts it.
}

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
