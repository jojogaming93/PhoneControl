// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only



namespace PhoneControl.Domain.Profiles;

public sealed class Settings
{
    public string PhoneBackend { get; set; } = "android";
    public string IPhoneEndpoint { get; set; } = "http://127.0.0.1:8100/";
    public bool AutoConnect { get; set; } = true;
    public int MaxFps { get; set; } = 120;
    public int MaxSize { get; set; } = 1920;
    public int BitrateMbps { get; set; } = 16;
    public bool Audio { get; set; } = true;
    public Profile Profile { get; set; } = Profile.Starter();
    public string GameplayMode { get; set; } = "Auto";
}
