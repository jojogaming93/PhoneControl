// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only

using System.Reflection;

namespace PhoneControl;

internal static class AppInfo
{
    private static readonly Assembly assembly = typeof(AppInfo).Assembly;
    // Publish.ps1 can override Version; show the running build, without its Git suffix.
    public static string Version { get; } = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
        .InformationalVersion.Split('+')[0] ?? assembly.GetName().Version?.ToString(3) ?? "Unknown";
    public static string Copyright { get; } = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? "";
}
