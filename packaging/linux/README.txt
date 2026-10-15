MonWin for Linux (experimental)
===============================

A small always-on-top overlay showing CPU, RAM, GPU, network and disk usage.
Runs entirely on your machine: no telemetry, no network connections, no root,
no kernel modules.

Install (per user, no root)
---------------------------
    ./install.sh              install to ~/.local
    ./install.sh --uninstall  remove it again

Or just run ./monwin from this folder.

Requirements
------------
* 64-bit Linux (x86-64) with a graphical session (X11, or Wayland with XWayland).
* Standard desktop libraries: fontconfig and the X11 client libraries
  (libX11, libICE, libSM). Every mainstream desktop already has them.
* No .NET installation is needed; the runtime is bundled.

Good to know
------------
* There is no universal "taskbar" on Linux, so MonWin floats along the bottom of
  your screen's usable area. You can drag it anywhere. Wayland compositors may
  ignore the requested position or always-on-top; see the project documentation.
* The tray icon needs a StatusNotifier host: KDE has one built in; GNOME needs the
  "AppIndicator and KStatusNotifierItem Support" extension. Without it, right-click
  the overlay for the same menu.
* GPU usage: AMD (amdgpu) and NVIDIA (proprietary driver) are supported; Intel and
  most other drivers don't expose usage, and then MonWin shows N/A (never a fake 0%).
* Settings: ~/.config/monwin/settings.json    Logs: ~/.local/state/monwin/logs

Project: https://github.com/deepsingh245/MonWin   License: MIT (see LICENSE)
