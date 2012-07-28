using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.IO;
using System.Drawing;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace BMax {
	public class BMaxApp : Form {
		[STAThread]
		static void Main() {
			Application.EnableVisualStyles();
			Application.Run(new BMaxApp());
		}
		//--------------------------------------------------------------------------------------------
		[StructLayout(LayoutKind.Sequential)]
		public struct RECT {
			public int Left; // x position of upper-left corner
			public int Top; // y position of upper-left corner
			public int Right; // x position of lower-right corner
			public int Bottom; // y position of lower-right corner
		}
		//--------------------------------------------------------------------------------------------
		static bool CFG_KEEP_TASKBAR_VISIBLE = true;
		static int CFG_BORDER_LEFT = 0;
		static int CFG_BORDER_RIGHT = 0;
		static int CFG_BORDER_TOP = 0;
		static int CFG_BORDER_BOTTOM = 0;
		static bool CFG_USE_STRIP_METHOD = false;
		//--------------------------------------------------------------------------------------------
		static int BORDER_LEFT, BORDER_RIGHT, BORDER_TOP, BORDER_BOTTOM;
		static List<Window> cWindows = new List<Window>();
		static List<SavedWindow> savedWindows = new List<SavedWindow>();

		static Rectangle bounds = Screen.PrimaryScreen.Bounds;
		static NotifyIcon trayIcon;
		static ContextMenuStrip trayMenu;
		static ToolStripMenuItem windowList, chkTaskbar, switchMethod, strip, resize;
		//--------------------------------------------------------------------------------------------
		[DllImport("user32.dll")]
		static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

		[DllImport("user32.dll", SetLastError = true)]
		static extern int GetWindowLong(IntPtr hWnd, int nIndex);

		[DllImport("user32.dll")]
		static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, SetWindowPosFlags uFlags);

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		static extern bool ShowWindow(IntPtr hWnd, ShowWindowCommands nCmdShow);

		[DllImport("user32.dll", SetLastError = true)]
		static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

		[DllImport("user32.dll")]
		static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

		[DllImport("user32.dll", SetLastError = true)]
		internal static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

		[Flags()]
		enum SetWindowPosFlags : uint {
			SynchronouswindowRectition = 0x4000,
			DeferErase = 0x2000,
			DrawFrame = 0x0020,
			FrameChanged = 0x0020,
			HideWindow = 0x0080,
			DoNotActivate = 0x0010,
			DoNotCopyBits = 0x0100,
			IgnoreMove = 0x0002,
			DoNotChangeOwnerZOrder = 0x0200,
			DoNotRedraw = 0x0008,
			DoNotReposition = 0x0200,
			DoNotSendChangingEvent = 0x0400,
			IgnoreResize = 0x0001,
			IgnoreZOrder = 0x0004,
			ShowWindow = 0x0040,
		}

		enum ShowWindowCommands : int {
			Hide = 0,
			Normal = 1,
			ShowMinimized = 2,
			Maximize = 3,
			ShowMaximized = 3,
			ShowNoActivate = 4,
			Show = 5,
			Minimize = 6,
			ShowMinNoActive = 7,
			ShowNA = 8,
			Restore = 9,
			ShowDefault = 10,
			ForceMinimize = 11
		}

		//--------------------------------------------------------------------------------------------
		public const int GCL_HICONSM = -34;
		public const int GCL_HICON = -14;

		public const int ICON_SMALL = 0;
		public const int ICON_BIG = 1;
		public const int ICON_SMALL2 = 2;

		public const int WM_GETICON = 0x7F;

		public static IntPtr GetClassLongPtr(IntPtr hWnd, int nIndex) {
			if( IntPtr.Size > 4 )
				return GetClassLongPtr64(hWnd, nIndex);
			else
				return new IntPtr(GetClassLongPtr32(hWnd, nIndex));
		}

		[DllImport("user32.dll", EntryPoint = "GetClassLong")]
		public static extern uint GetClassLongPtr32(IntPtr hWnd, int nIndex);

		[DllImport("user32.dll", EntryPoint = "GetClassLongPtr")]
		public static extern IntPtr GetClassLongPtr64(IntPtr hWnd, int nIndex);

		[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = false)]
		static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
		//--------------------------------------------------------------------------------------------
		private BMaxApp() {
			CreateIconAndMenus();
			CalculateBorders();
		}
		//--------------------------------------------------------------------------------------------
		private void CreateIconAndMenus() {
			trayMenu = new ContextMenuStrip();

			windowList = new ToolStripMenuItem("&Toggle Maximize");
			windowList.DropDownItems.Add("");
			windowList.DropDown.ItemClicked += new ToolStripItemClickedEventHandler(ToggleMaximized);
			trayMenu.Items.Add(windowList);
			trayMenu.Items[0].MouseEnter += new EventHandler(PopulateWindowList);

			chkTaskbar = new ToolStripMenuItem("&Keep Taskbar visible");
			chkTaskbar.Checked = CFG_KEEP_TASKBAR_VISIBLE;
			chkTaskbar.CheckOnClick = true;
			chkTaskbar.CheckedChanged += new EventHandler(chkTaskBar_clicked);
			trayMenu.Items.Add(chkTaskbar);

			switchMethod = new ToolStripMenuItem("&Switch maximizing method");

			strip = new ToolStripMenuItem("&Strip window");
			resize = new ToolStripMenuItem("&Resize window");

			strip.Checked = CFG_USE_STRIP_METHOD;
			resize.Checked = !CFG_USE_STRIP_METHOD;

			strip.Click += new EventHandler(chkStrip_Clicked);
			resize.Click += new EventHandler(chkResize_Clicked);

			switchMethod.DropDownItems.Add(strip);
			switchMethod.DropDownItems.Add(resize);
			trayMenu.Items.Add(switchMethod);

			trayMenu.Items.Add("-", null);
			trayMenu.Items.Add("&Exit", null);

			trayMenu.Items[4].Click += new EventHandler(Exit);

			trayIcon = new NotifyIcon();
			trayIcon.Text = "BMax";
			trayIcon.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
			trayIcon.ContextMenuStrip = trayMenu;
			trayIcon.Visible = true;
		}
		//--------------------------------------------------------------------------------------------
		private void PopulateWindowList(object sender, EventArgs e) {
			windowList.DropDownItems.Clear();
			foreach( Window w in GetActiveWindows.Get() ) {
				windowList.DropDownItems.Add(w.Title);
				windowList.DropDownItems[windowList.DropDownItems.Count - 1].ToolTipText = w.WindowClass;
				windowList.DropDownItems[windowList.DropDownItems.Count - 1].Image = GetAppIcon(w.handle).ToBitmap();
			}
		}

		private void chkStrip_Clicked(object sender, EventArgs e) {
			CFG_USE_STRIP_METHOD = true;
			strip.Checked = CFG_USE_STRIP_METHOD;
			resize.Checked = !CFG_USE_STRIP_METHOD;
		}

		private void chkResize_Clicked(object sender, EventArgs e) {
			CFG_USE_STRIP_METHOD = false;
			strip.Checked = CFG_USE_STRIP_METHOD;
			resize.Checked = !CFG_USE_STRIP_METHOD;
		}

		private void chkTaskBar_clicked(object sender, EventArgs e) {
			CFG_KEEP_TASKBAR_VISIBLE = chkTaskbar.Checked;
			CalculateBorders();
		}
		//--------------------------------------------------------------------------------------------
		private void ToggleMaximized(object sender, ToolStripItemClickedEventArgs e) {
			string wClass = e.ClickedItem.ToolTipText;
			string wTitle = e.ClickedItem.Text;
			Maximize((IntPtr)0, wTitle, wClass);
		}
		//--------------------------------------------------------------------------------------------
		private void CalculateBorders() {

			BORDER_LEFT = CFG_BORDER_LEFT;
			BORDER_RIGHT = CFG_BORDER_RIGHT;
			BORDER_TOP = CFG_BORDER_TOP;
			BORDER_BOTTOM = CFG_BORDER_BOTTOM;

			if( CFG_KEEP_TASKBAR_VISIBLE ) {
				IntPtr taskBar = FindWindow("Shell_TrayWnd", "");
				RECT tr = new RECT();
				GetWindowRect(taskBar, out tr);

				if( tr.Left == bounds.Left ) {
					if( tr.Top == bounds.Top ) {
						if( tr.Right == bounds.Right ) {
							BORDER_TOP += tr.Bottom - tr.Top;
						} else {
							BORDER_LEFT += tr.Right - tr.Left;
						}
					} else {
						BORDER_BOTTOM += tr.Bottom - tr.Top;
					}
				} else {
					BORDER_RIGHT += tr.Right - tr.Left;
				}
			}
		}
		//--------------------------------------------------------------------------------------------
		private static void Maximize(IntPtr wHandle, string wTitle = "", string wClass = "") {
			if( wTitle != "" && wClass != "" )
				wHandle = FindWindow(wClass, wTitle);

			bool isMaximized = false;
			int index = 0;
			foreach( SavedWindow wd in savedWindows ) {
				if( wd.handle == wHandle ) {
					isMaximized = true;
					break;
				}
				++index;
			}

			if ( isMaximized )
			{
					//restore style
					SetWindowLong(wHandle, -16, savedWindows[index].windowStyle);
					//restore position
					SetWindowPos(wHandle, (IntPtr)(0),
						savedWindows[index].windowRect.Left,
						savedWindows[index].windowRect.Top,
						savedWindows[index].windowRect.Right - savedWindows[index].windowRect.Left,
						savedWindows[index].windowRect.Bottom - savedWindows[index].windowRect.Top,
						(SetWindowPosFlags)0x0020);

					ShowWindow(wHandle, (ShowWindowCommands)5);

					MoveWindow(wHandle,
						savedWindows[index].windowRect.Left,
						savedWindows[index].windowRect.Top,
						savedWindows[index].windowRect.Right - savedWindows[index].windowRect.Left,
						savedWindows[index].windowRect.Bottom - savedWindows[index].windowRect.Top,
						true);
					savedWindows.RemoveAt(index);

			} else {
				//Save data
				int wStyle = GetWindowLong(wHandle, -16);
				RECT rWind;
				GetWindowRect(wHandle, out rWind);

				SavedWindow w = new SavedWindow();
				w.handle = wHandle;
				w.windowRect = rWind;
				w.windowStyle = wStyle;
				savedWindows.Add(w);

				if( CFG_USE_STRIP_METHOD ) {
				//Strip Method
					//SetWindowLong(wHandle, -16, wStyle & ~(0x00040000 | 0x00C00000));
					SetWindowLong(wHandle, -16, wStyle & ~(0x00040000 | 0x00C00000));

					int LEFT = bounds.Left + BORDER_LEFT;
					int TOP = bounds.Top + BORDER_TOP;
					int RIGHT = bounds.Right - BORDER_RIGHT - BORDER_LEFT;
					int BOTTOM = bounds.Bottom - BORDER_BOTTOM - BORDER_TOP;

					SetWindowPos(wHandle, (IntPtr)(0),	LEFT, TOP, RIGHT, BOTTOM, (SetWindowPosFlags)0x0020);

					ShowWindow(wHandle, (ShowWindowCommands)5);
				} else {
				//Resize Method
					RECT rClient;
					GetWindowRect(wHandle, out rWind);
					GetClientRect(wHandle, out rClient);
					int border_thickness = ((rWind.Right - rWind.Left) - rClient.Right) / 2;
					int titlebar_height = rWind.Bottom - rWind.Top - rClient.Bottom - border_thickness;

					int LEFT = -border_thickness;
					int TOP = -titlebar_height;
					int RIGHT = bounds.Right + (border_thickness * 2);
					int BOTTOM = bounds.Bottom - BORDER_BOTTOM + titlebar_height + border_thickness;

					MoveWindow(wHandle, LEFT, TOP, RIGHT, BOTTOM, true);
				}
			}
		}
		//--------------------------------------------------------------------------------------------
		protected override void OnLoad(EventArgs e) {
			Visible = false;
			ShowInTaskbar = false;
			base.OnLoad(e);
		}
		//--------------------------------------------------------------------------------------------
		protected override void Dispose(bool isDisposing) {
			if( isDisposing )
				trayIcon.Dispose();
			base.Dispose(isDisposing);
		}
		//--------------------------------------------------------------------------------------------
		private void Exit(object sender, EventArgs e) {
			Application.Exit();
		}
		//--------------------------------------------------------------------------------------------
		public class Window {
			public IntPtr handle;
			public string Title;
			public string WindowClass;
		}

		public class SavedWindow {
			public IntPtr handle;
			public int windowStyle;
			public RECT windowRect;
		}

		//--------------------------------------------------------------------------------------------
		public Icon GetAppIcon(IntPtr hwnd)
		{
		  IntPtr iconHandle = SendMessage(hwnd,WM_GETICON,ICON_SMALL2,0);
		  if(iconHandle == IntPtr.Zero)
		    iconHandle = SendMessage(hwnd,WM_GETICON,ICON_SMALL,0);
		  if(iconHandle == IntPtr.Zero)
		    iconHandle = SendMessage(hwnd,WM_GETICON,ICON_BIG,0);
		  if (iconHandle == IntPtr.Zero)
		    iconHandle = GetClassLongPtr(hwnd, GCL_HICON);
		  if (iconHandle == IntPtr.Zero)
		    iconHandle = GetClassLongPtr(hwnd, GCL_HICONSM);

		  if(iconHandle == IntPtr.Zero)
		    return null;

		  Icon icn = Icon.FromHandle(iconHandle);

		  return icn;
		}
		//--------------------------------------------------------------------------------------------
		public static class GetActiveWindows {
			delegate bool EnumWindowsProc(IntPtr hWnd, int lParam);

			[DllImport("user32.dll")]
			static extern bool EnumWindows(EnumWindowsProc enumFunc, int lParam);

			[DllImport("user32.dll")]
			static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

			[DllImport("user32.dll")]
			static extern int GetWindowTextLength(IntPtr hWnd);

			[DllImport("user32.dll")]
			static extern bool IsWindowVisible(IntPtr hWnd);

			[DllImport("user32.dll")]
			static extern IntPtr GetShellWindow();

			[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
			static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

			public static List<Window> Get() {
				IntPtr lShellWindow = GetShellWindow();
				List<Window> windows = new List<Window>();

				EnumWindows(delegate(IntPtr hWnd, int lParam) {
					if( hWnd == lShellWindow )
						return true;
					if( !IsWindowVisible(hWnd) )
						return true;

					int lLength = GetWindowTextLength(hWnd);
					if( lLength == 0 )
						return true;

					StringBuilder WindowTitle = new StringBuilder(lLength);
					GetWindowText(hWnd, WindowTitle, lLength + 1);

					StringBuilder ClassName = new StringBuilder(256);
					GetClassName(hWnd, ClassName, ClassName.Capacity);

					Window win = new Window();
					win.handle = hWnd;
					win.Title = WindowTitle.ToString();
					win.WindowClass = ClassName.ToString();
					windows.Add(win);
					return true;
				}, 0);
				return windows;
			}
		}
	}
}
