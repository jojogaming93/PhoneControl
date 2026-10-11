# SignPath Foundation application draft

Prepared 11 October 2026. **Status: draft only; not submitted or approved.** Public source/download pages must be populated before submission. No trusted signing identity is currently configured.

## Application fields

Use the [SignPath Foundation application](https://signpath.org/apply). The repository below is the maintainer-selected public destination; keep the app name PhoneControl.

| Field | Prepared value |
|---|---|
| Project name | PhoneControl |
| Repository URL | https://github.com/jojogaming93/PhoneControl |
| Homepage URL | https://github.com/jojogaming93/PhoneControl |
| Download URL | https://github.com/jojogaming93/PhoneControl/releases — pending a public download |
| Privacy policy URL | https://github.com/jojogaming93/PhoneControl#privacy — pending publication of the README |
| Wikipedia URL | Leave blank |
| Tagline | Free USB Android mirroring and editable keyboard and mouse touch mappings for Windows. |
| Category | Choose the available utility/productivity category that best matches a desktop phone-control tool. |
| Maintainer type | Individual open-source maintainer |
| Build system | GitHub Actions — workflow prepared locally; confirm a public successful run before submission |
| First name, last name, email | Enter your own account/contact details directly in the form; never commit them to this project |
| Company name | Leave blank unless applicable |
| Primary discovery channel | Choose the available AI assistant/web-search option |
| Exact discovery source | OpenAI Codex, via Microsoft's Windows code-signing documentation |

**Description**

PhoneControl is a free, open-source Windows desktop application for mirroring an Android phone over USB and mapping keyboard and mouse inputs to editable touch controls. Users can configure and save their own layouts, switch between walking and driving mappings, and control when input capture is active. The application operates locally, retains profiles on the user's PC, and releases held touches when control stops. It is distributed under the GNU Affero General Public License version 3 only, with corresponding editable source supplied alongside binaries.

**Reputation — honest draft**

PhoneControl is a new project. Its maintainer has tested local builds, installer upgrades and deterministic regressions, but we do not yet have verified public download statistics, independent reviews, media coverage or broad adoption. We are publishing the full source, build workflow, documentation and source archive for review. We understand that SignPath Foundation requires verifiable project reputation and that approval is discretionary. Please advise whether the project is eligible at this early stage or what evidence is needed before applying again.

Replace or supplement this paragraph only with real public evidence. Local self-tests are engineering evidence, not a claim of community reputation.

## Submission prerequisites

1. Publish the audited source to the selected public repository. On 11 October 2026, the public repository contained only a LICENSE file; the old PhoneControl URL returned 404. Use the clean checkout procedure in `PUBLIC-RELEASE.md` to avoid publishing private development history.
2. Publish project documentation and a clearly labelled initial download with corresponding source. SignPath's terms require an already released, documented and maintained open-source project. Existing unsigned validation builds must not be described as trusted or warning-free.
3. Publish the README's proposed Code signing policy and Privacy sections. The download page should link to them and identify the program as pending. After approval, use the provider's required attribution wording and validated team roles; do not claim sponsorship beforehand.
4. Provide real reputation evidence if it becomes available. The project may not qualify yet. Confirm all redistributed component licenses against the terms, including bundled system runtimes and upstream tools.
5. Review [SignPath's terms](https://signpath.org/terms), enable multi-factor authentication on maintainer accounts, and enter the contact details. The form requires Code of Conduct acceptance and personal-data processing consent; the optional marketing checkbox is separate. Submission, agreements and any CAPTCHA remain for the maintainer to complete.

## Integration plan after approval

SignPath is managed signing, not a local exportable certificate. The current `-SignTool`/`-CertificateThumbprint` interface supports a locally available trusted certificate; it does not claim to integrate SignPath already.

After SignPath approves the project, use its assigned organization/project/policy identifiers and artifact configuration to connect the approved public GitHub Actions build. Keep service credentials in GitHub Actions secrets and retain manual release approval. Sign only PhoneControl-owned PE files, retain upstream binaries and notices, and enforce product/version metadata. Obfuscate before signing; sign and verify the app EXE/DLL, compile and sign the payload installer, then embed that signed payload in the launcher and sign/verify the launcher. Recompute hashes/manifests only after final signatures and verify the signed payload still passes regressions. Bind every signed artifact to the exact public source commit and bundled source archive.

The current CI artifact is expressly **unsigned validation** and must not be uploaded as a trusted release. Implement managed signing only with the assigned service configuration; no placeholder secrets or invented identifiers are active in CI.

Trusted Authenticode addresses certificate trust, while new direct downloads can still trigger SmartScreen reputation warnings. [Microsoft's guidance](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation) does not promise immediate warning-free downloads, including for EV certificates.
