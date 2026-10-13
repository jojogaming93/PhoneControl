// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only

using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;



namespace PhoneControl.Adapters.Ios;

internal readonly record struct WdaStatus(bool Ready, string? ActiveSession);
