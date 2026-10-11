# Copyright (c) 2026 jojogaming93
# SPDX-License-Identifier: AGPL-3.0-only
param(
    [Parameter(Mandatory)][string]$Path,
    [Parameter(Mandatory)][string]$SignTool,
    [Parameter(Mandatory)][string]$CertificateThumbprint,
    [switch]$VerifyOnly
)
$ErrorActionPreference = 'Stop'
$certificate = Get-Item -LiteralPath "Cert:\CurrentUser\My\$CertificateThumbprint"
if ($certificate.Subject -eq $certificate.Issuer) { throw 'Self-signed certificates are not allowed for public releases.' }
if (-not $VerifyOnly) {
    & $SignTool sign /sha1 $CertificateThumbprint /fd SHA256 /tr 'http://timestamp.digicert.com' /td SHA256 $Path
    if ($LASTEXITCODE -ne 0) { throw "Signing failed: $Path" }
}
$signature = Get-AuthenticodeSignature -LiteralPath $Path
if ($signature.SignatureType -ne 'Authenticode' -or $signature.SignerCertificate.Thumbprint -ne $CertificateThumbprint) { throw "Unexpected or missing embedded signer: $Path" }
& $SignTool verify /pa /all $Path
if ($LASTEXITCODE -ne 0 -or $signature.Status -ne 'Valid') { throw "Trusted signature verification failed: $Path" }
if (-not $signature.TimeStamperCertificate) { throw "Missing release timestamp: $Path" }
