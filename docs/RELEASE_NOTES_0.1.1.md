# ELIXR Player List 0.1.1

- The Windows installer now includes Microsoft's **complete x64 WebView2 offline
  installer**, not a small online bootstrapper. .NET and the compiled frontend
  remain included. No separate runtime/package download is required during setup.
- Setup runs the embedded prerequisite during installs/upgrades. The package is
  validated against Microsoft's Authenticode signature when building the release.
- Prevent overlapping desktop-browser initialization and stop initialization
  cleanly if the window closes while WebView2 is starting.
- A second normal launch focuses the existing window instead of starting another
  main process against the same browser profile. Smoke tests use an isolated
  browser profile as well as isolated login/settings files.
- Use software composition for the small native WPF host/editor to avoid a
  graphics-driver composition stall before browser initialization. WebView2's
  rendering/animations retain their normal independent pipeline.
- Report WebView2 disconnects separately from missing runtimes. End users are no
  longer told to rebuild the frontend. Minimal local startup diagnostics help
  identify remaining failures without recording credentials or profile data.

Use **ELIXR-PlayerList-0.1.1-Setup-x64.exe**, especially on a fresh PC. Its larger
download contains the WebView2 prerequisite. Internet is still needed to sign in
to VRChat and retrieve community feeds/updates. The portable ZIP needs an existing
WebView2 Runtime. Windows 10 2004+ / Windows 11 x64. The ELIXR installer remains
unsigned; Windows may show an unknown-publisher warning. Microsoft signs its own
bundled WebView2 installer. Source remains MIT-licensed.
