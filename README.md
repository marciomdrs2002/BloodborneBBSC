# Bloodborne BBSC Launcher

Launcher for Lantern Engine (Bloodborne on Windows, by Awakenchan) with a **BBSC local server**
option: seamless co-op among friends over Tailscale, no public server.

## What it adds to the 0.9.33 launcher
- **Online → Server → BBSC local server**: host on this PC or join a host with an invite code,
  in steps (Tailscale, hosting, invite, account). The host runs the co-op server (shadNet) and
  BBSC's game server on its Tailscale address; guests run nothing extra.
- **Seamless co-op (BBSC mod)**: `bbsc\bbsc.dll` is loaded into the engine (`bb-probe.exe`) as
  soon as `bb-start.exe` starts it. The Bath Messengers then sell the Hunt, Kin, Bloodlust and Duel
  Bells for free.
- A play mode next to Play (Offline / The Hunter's Dream / BBSC co-op), and a "Hosting co-op"
  indicator in the title bar that stops hosting.
- Updates come from this repository's releases, not upstream's.

## Package layout (next to `Bloodborne Launcher.exe`, in a Lantern Engine 0.9.33 folder)
- `coop-server\`: `shadnet.exe` + DLLs, `shadnet-sample.exe` (accounts). Host only.
- `game-server\`: `bbsc_gameserver.py`, `bbhost_service.py`, `ss_info_template.xml`, and
  `python\` (python.org's 3.12 embeddable package, with `..` added to `python312._pth`). Host only.
- `bbsc\`: `bbsc.dll`, `mod.json` and its LICENSE/NOTICE/SUPPORTERS/CREDITS (the mod turns itself
  off if they are missing). Everyone.

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
