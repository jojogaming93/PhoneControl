// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only




namespace PhoneControl.Domain.Phones;

public sealed record PhoneScreenshot(byte[] Png, int PointWidth, int PointHeight, int PixelWidth, int PixelHeight);
