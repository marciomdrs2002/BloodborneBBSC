# Bloodborne BBSC Launcher

Launcher for Lantern Engine (Bloodborne on Windows, by Awakenchan) with a **BBSC local server**
option: seamless co-op among friends over Tailscale, no public server.

## Origin
- `launcher/BloodborneLauncher`: Lantern Engine's Bloodborne Launcher **0.9.33**, recovered with
  ILSpy from the released binary (the published sources at github.com/Awakenchan/LanternEngine
  stop at 0.9.29). The window layouts are the release's compiled BAML. GPL, see LICENSE.
- BBSC game server: github.com/Classmg22/BBSC (CLS_AMG, GPL-2.0-or-later).
- shadNet server: shadnet-p2p by Wozzardman (GPL-2.0-or-later).

## Build
```
dotnet publish launcher/BloodborneLauncher -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o dist/launcher
```
