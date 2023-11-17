# xml2make
Convert KEIL uVision .uvproj files to Visual Studio 2022 XML + Makefile

Since ARM is too lazy to fix KEIL to properly use GCC toolchain, but I still like using it as IDE (don't ask why),
I made this tool to address a weird kink where someone would want to organize the project and debug it inside KEIL, 
but build externally using latest GNU ARM toolchain. As a bonus, it also outputs Visual Studio-compatible vcproj file
which can be opened and source edited inside VS (but no debugging, of course). It handles file groups properly and passes
along useful things like defines/includes from the settings UI to the Makefile.

As of this moment, it's pre-set for Cortex-M4 (STM32F4) in terms of build options, if targeting M0 or M3 some hard-coded
things would need to be changed. I may consider pulling target info out of the project file and auto-generating platform
stuff as well but that's a low priority as I'm the only user of this right now and it only needs to work for Cortex-M4.

Makefile itself assumes you have the GNU ARM toolchain in %PATH% and at least have `make.exe` and `rm.exe` avialable in `%PATH%`.
