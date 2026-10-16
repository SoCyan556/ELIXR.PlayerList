# ELIXR Player List 0.1.0

First public MIT-licensed release of the standalone Windows player list.

- Compact Vue 3/WebView2 interface in a lightweight .NET Windows host; no Electron.
- VRChat sign-in, 2FA, Windows-encrypted remembered sessions and automatic restore.
- Live local-log roster, real player portraits and the original FewTags/ETags/group feeds.
- Full/compact tags with source colors and supported animations.
- Separate always-on-top desktop overlay, real ELIXR logo, search and player actions.
- Animated dimmed-screen resize editor with saved position/size.
- GitHub update checks with Update now / Maybe later; native size/SHA-256 verification.

Download **Setup-x64.exe** for normal installation, **Portable-x64.zip** for a
portable app folder, or **Source.zip** to build/develop it. The source is also
available directly in the repository. SHA256SUMS.txt covers these three downloads.

Windows 10 2004+ / Windows 11 x64. The installer includes .NET and checks/installs
WebView2 if needed (Microsoft's prerequisite download requires internet).

Known limitations: unsigned publisher/possible SmartScreen warning; no native
SteamVR rendering, persistent Recent Players history, or extra profile metadata
beyond the implemented portrait/tag/group details. Private profiles/memberships,
network failures and VRChat rate limits may leave fallback icons or missing tags.
Community tags are third-party labels, not verified claims. VRChat is not
affiliated with this project. Saved sessions and settings remain on your computer.
