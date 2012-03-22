BMax - Borderless maximize any Window
=============

This background utility will automatically maximize any configured games/applications. To use it, create a text file named config.txt in the same directory as the compiled application. The formatting is as follows:

    # Comment lines start with a hash symbol
    #
    # To specifiy an application to be maximized, enter the exact window title,
    # followed by an optional WindowClass
    Diablo III,D3 Main Window Class
    Minecraft Launcher,SunAwtFrame

The WindowClass can be obtained by tools like the AutoHotkey WindowSpy and is helpful to clearly identify an application.