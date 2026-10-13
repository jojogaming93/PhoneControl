// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only




namespace PhoneControl.Domain.Geometry;

public readonly record struct ScreenRect(int X, int Y, int Width, int Height)
{
    public int Left => X;
    public int Top => Y;
    public int Right => X + Width;
    public int Bottom => Y + Height;
}
