# third_party

## Tor (bundled into private windows)

The `tor/` folder holds the Tor Expert Bundle and is **not committed** (binaries, ~36 MB).
To restore it for a build, download the Windows x64 Expert Bundle and extract so the layout is:

```
third_party/tor/tor/tor.exe
third_party/tor/data/geoip
third_party/tor/data/geoip6
third_party/tor/data/torrc-defaults
```

Download from: https://www.torproject.org/download/tor/  (Windows x86_64 Expert Bundle)

The app project copies this tree into the build output under `tor\`, where `TorManager`
launches it for Incognito (Tor) windows.

## uBlock Origin (built-in ad blocker)

The `ublock/` folder holds the unpacked uBlock Origin Chromium extension and is **not
committed** (~16 MB). To restore it for a build, download the latest
`uBlock0_*.chromium.zip` from the releases below and extract so the layout is:

```
third_party/ublock/manifest.json
third_party/ublock/...   (the rest of the unpacked extension)
```

Download from: https://github.com/gorhill/uBlock/releases/latest  (the `*.chromium.zip` asset)

The app project copies this tree into the build output under `ublock\`, and the browser
loads it on startup via WebView2's extension API so ad/tracker blocking is on by default.
