# UDPWiiHook

*Allows UDPWii apps to be used with the Dolphin Emulator again.*

## Introduction

UDPWiiHook is the exact opposite of [BetterJoyForDolphin][betterjoy] - it acts as a proxy from UDPWii protocol to DSU(Cemuhook), allowing existing UDPWii apps (e.g. [my fork of UDPMote][udpmote]) to work with recent versions of Dolphin Emulator. Also, MotionPlus support is implemented via a [extended UDPWii protocol][protocol], and is enabled as long as you're using my fork of UDPMote.

## Usage

Run UDPWiiHook along with Dolphin Emulator, then [enable DSU client on Dolphin][dsu_client].

Launch the UDPWii app of your choice and connect to UDPWiiHook (you should see its broadcast; if not, the ports are 4434-4437). A 'controller' will appear in Dolphin's Controller Settings screen. Configure controll mappings as you like, or [use our reference settings for UDPMote][dolphin_settings]. Enjoy.

NOTE: This is currently alpha quality software; expect things to break. Also, a GUI for status and settings is *planned*, as that's my whole reason to use C#, but I no longer have time or passion to keep maintaining this project.

[betterjoy]: https://github.com/yuk27/BetterJoyForDolphin
[udpmote]: https://github.com/EZForever/UDPMote
[protocol]: https://github.com/EZForever/UDPWiiHook/wiki/Protocol
[dsu_client]: https://wiki.dolphin-emu.org/index.php?title=DSU_Client#Dolphin
[settings]: https://github.com/EZForever/UDPWiiHook/wiki/DolphinSettings

