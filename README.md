BMax - Borderless maximize any Window
=============

This background utility will automatically maximize any configured games/applications. To use it, create a text file named config.txt in the same directory as the compiled application. The formatting is as follows:

    # Comment lines start with a hash symbol. To specifiy an application to
    # be maximized, enter the exact window title, followed by a comma and the
    # optional, but recommended WindowClass.
    Diablo III,D3 Main Window Class
    Minecraft Launcher,SunAwtFrame
    # You can also specify Windows without the WindowClass, but this can lead
    # to false positives. The comma is omitted in this case.
    Warcraft III

The WindowClass can be obtained by tools like the AutoHotkey Window Spy and is helpful to clearly identify an application.