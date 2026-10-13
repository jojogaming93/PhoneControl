// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only




namespace PhoneControl.Domain.Profiles;

public sealed class Binding
{
    public string Name { get; set; } = "Action";
    public string Key { get; set; } = "E";
    public double X { get; set; } = .8;
    public double Y { get; set; } = .5;
    public string Mode { get; set; } = "Hold";
    public Binding Copy() => new() { Name = Name, Key = Key, X = X, Y = Y, Mode = Mode };
}
