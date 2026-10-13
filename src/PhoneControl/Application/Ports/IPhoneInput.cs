// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only





namespace PhoneControl.Application.Ports;

public interface IPhoneInput : ITouchSink, IPhoneDisplay
{
    void Back();
}
