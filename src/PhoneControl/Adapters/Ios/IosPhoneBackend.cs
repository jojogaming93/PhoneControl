// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only



using PhoneControl.Application.Ports;
using PhoneControl.Domain.Phones;

namespace PhoneControl.Adapters.Ios;

public sealed class IosPhoneBackend : IPhoneBackend
{
    private readonly string endpoint;
    private readonly Func<IosPhoneSession, IPhoneGestureMirror> createMirror;
    public IosPhoneBackend(string endpoint, Func<IosPhoneSession, IPhoneGestureMirror> createMirror) {
        this.endpoint = endpoint; this.createMirror = createMirror;
    }
    public async Task<IReadOnlyList<PhoneDevice>> DiscoverDevices() {
        using var client = new IosWdaClient(endpoint);
        var status = await client.Status(CancellationToken.None);
        return [new(client.Endpoint.AbsoluteUri, status.Ready ? PhoneDeviceState.Ready : PhoneDeviceState.Offline,
            "iPhone • WebDriverAgent • completed gestures", PhoneTransport.Network)];
    }
    public IPhoneSession CreateSession() => new IosPhoneSession(endpoint, createMirror);
}
