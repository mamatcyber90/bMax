#bMax - borderless maximizer

bMax is a tray utility that can switch Windows applications into a fullscreen-like mode where the window's title and decorations (and optionally, the taskbar) are hidden. There are two distinct methods to achieve the effect:

###Resize method
This is the less intrusive method. It keeps the window decorations and title intact, resizes the window to be slightly larger than the visible screen area, and then moves the content area of the window in place, effectively hiding the title bar and borders outside the visible area.

###Strip method
This method takes a more aggressive approach by forcing a window to hide it's decoration and title. You can try it on cases where the resize method fails to deliver an acceptable result.
