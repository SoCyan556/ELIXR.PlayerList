# Build and publish a release

Requirements: Windows 10/11 x64, .NET SDK 10, Node.js 24, npm, Git, PowerShell,
Inno Setup 6.3+ (ISCC.exe), and network access for public build dependencies.
WebView2 is needed to run the app/tests, not to compile it. No credentials are
required to build. Do not place credentials in the project tree.

1. Update the version in the desktop project and frontend package metadata;
   keep release tags in `vMAJOR.MINOR.PATCH` form (stable releases only).
2. Run `./scripts/Build-Release.ps1 -Version 0.1.1`. It runs tests, publishes
   self-contained .NET for win-x64, collects runtime dependency license texts,
   downloads and validates Microsoft's signed full x64 offline WebView2 installer, compiles
   the per-user installer, and creates the portable ZIP. Inno Setup can be
   selected with `-IsccPath`. The script refuses to reuse the existing
   `artifacts/publish-win-x64` folder: move a previous build aside first so stale
   files cannot enter a release. The build does not silently delete user files.
3. Smoke-test the published EXE with `--smoke-test` and an absolute output JSON
   path in an existing directory. This uses isolated settings/session files.
   Test installing/uninstalling on a clean Windows VM before broader rollout,
   including a machine without WebView2 and one with an existing installation.
4. Review `git status` and `git diff --cached`, and commit all intended source
   changes. Never commit `artifacts`, `bin`, `obj`, `node_modules`, local settings,
   sessions, caches, screenshots with real users, secrets or databases.
5. Run `./scripts/Package-Source.ps1 -Version 0.1.1`. This creates an archive from
   **Git HEAD**, not a recursive working-folder ZIP, and SHA256SUMS.txt covering
   the installer, portable ZIP and source ZIP. It refuses dirty/untracked source.
6. Push the commit, tag it `v0.1.1`, and create a normal stable GitHub release:

   ```powershell
   git tag v0.1.1
   git push origin main --tags
   gh release create v0.1.1 artifacts/release/ELIXR-PlayerList-0.1.1-* artifacts/release/SHA256SUMS.txt --title "ELIXR Player List 0.1.1" --notes-file docs/RELEASE_NOTES_0.1.1.md --verify-tag
   ```

The update checker expects exactly:
`ELIXR-PlayerList-VERSION-Setup-x64.exe` in the official
`SoCyan556/ELIXR.PlayerList` release and GitHub's `sha256:` asset digest. Drafts,
prereleases, older versions, other repositories and missing digests do not
trigger updates. Never replace an already-published binary; publish a new
version. Source/portable filenames follow the same versioning convention.

The installer currently is unsigned. Do not claim code-signing or bypass
SmartScreen warnings in scripts. Add a publisher-owned Authenticode signing
step before creating hashes/publishing if a certificate is obtained. Download
integrity verification is not a substitute for a publisher signature.

An optional Windows GitHub Actions build/test workflow is provided at
`docs/ci-build.yml.example`. A repository administrator can copy it to
`.github/workflows/build.yml` with workflow-write permission; publishing source
and downloading releases do not require granting that extra permission.

Installer details: fixed upgrade AppId, per-user LocalAppData/Programs location,
optional desktop shortcut, Start menu entry, uninstall support, .NET included,
Microsoft's complete x64 Evergreen offline installer is embedded and run during
setup, including upgrades. It does not download its runtime payload at install
time. Runtime setup can service an existing installation; a newer runtime is
kept. Build time still requires internet to obtain this Microsoft-signed payload.
The payload must be at least 50 MiB and pass Microsoft Authenticode validation;
the 1-2 MiB online bootstrapper must never replace it. The portable ZIP does not
install prerequisites: recommend the full installer for machines without WebView2.
No Windows startup task or telemetry is installed. Uninstall preserves user data.
