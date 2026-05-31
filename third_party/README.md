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
launches it for private (Tor) windows.
