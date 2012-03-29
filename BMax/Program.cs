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
		static bool KEEP_TASKBAR_VISIBLE = true;
		static int BORDER_LEFT = 0;
		static int BORDER_RIGHT = 0;
		static int BORDER_TOP = 0;
		static int BORDER_BOTTOM = 0;
		//--------------------------------------------------------------------------------------------
		static List<Window> cWindows = new List<Window>();
		static Rectangle bounds = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
		static NotifyIcon trayIcon;
		static ContextMenuStrip trayMenu;
		static ToolStripMenuItem windowList;
		static Thread workerThread;
		//--------------------------------------------------------------------------------------------
		[StructLayout(LayoutKind.Sequential)]
		struct RECT {
			public int Left; // x position of upper-left corner
			public int Top; // y position of upper-left corner
			public int Right; // x position of lower-right corner
			public int Bottom; // y position of lower-right corner
		}

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

		[Flags()]
		enum SetWindowPosFlags : uint {
			SynchronousWindowPosition = 0x4000,
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
		/// <summary>
		/// Hide the main form of the Application when loading
		/// </summary>
		/// <param name="e"></param>
		protected override void OnLoad(EventArgs e) {
			Visible = false;
			ShowInTaskbar = false;
			base.OnLoad(e);
		}
		//--------------------------------------------------------------------------------------------
		/// <summary>
		/// Dispose the tray icon
		/// </summary>
		/// <param name="isDisposing"></param>
		protected override void Dispose(bool isDisposing) {
			if( isDisposing )
				trayIcon.Dispose();
			base.Dispose(isDisposing);
		}
		//--------------------------------------------------------------------------------------------
		/// <summary>
		/// Main entry point. Creates the tray icon, processes the options and starts the maximizing worker thread.
		/// </summary>
		private BMaxApp() {
			trayMenu = new ContextMenuStrip();
			windowList = new ToolStripMenuItem("&Active Windows");
			windowList.DropDownItems.Add("");
			trayMenu.Items.Add(windowList);
			trayMenu.Items.Add("-", null);
			trayMenu.Items.Add("&Exit", null);

			trayMenu.Items[0].MouseEnter += new EventHandler(PopulateWindowList);
			trayMenu.Items[2].Click += new EventHandler(Exit);

			trayIcon = new NotifyIcon();
			trayIcon.Text = "BMax";
			trayIcon.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
			trayIcon.ContextMenuStrip = trayMenu;
			trayIcon.Visible = true;

			ReadConfig(ref cWindows);

			// Calculate space to keep the taskbar visible
			if( KEEP_TASKBAR_VISIBLE ) {
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

			workerThread = new Thread(MainLoop);
			workerThread.Start();
		}
		//--------------------------------------------------------------------------------------------
		/// <summary>
		/// Exit the application and close all threads
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void Exit(object sender, EventArgs e) {
			workerThread.Abort();
			Application.Exit();
		}
		//--------------------------------------------------------------------------------------------
		/// <summary>
		/// Populate the dropdown with entries for all current active windows
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void PopulateWindowList(object sender, EventArgs e) {
			windowList.DropDownItems.Clear();
			foreach( Window w in GetActiveWindows.Get() ) {
				windowList.DropDownItems.Add(w.Title);
				windowList.DropDownItems[windowList.DropDownItems.Count - 1].ToolTipText = w.WindowClass;
				windowList.DropDown.ItemClicked += new ToolStripItemClickedEventHandler(DropDownClick);
			}
		}
		//--------------------------------------------------------------------------------------------
		/// <summary>
		/// Maximize the selected Window
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void DropDownClick(object sender, ToolStripItemClickedEventArgs e) {
			string wClass = e.ClickedItem.ToolTipText;
			string wTitle = e.ClickedItem.Text;
			Maximize((IntPtr)0, wTitle, wClass);
		}
		//--------------------------------------------------------------------------------------------
		/// <summary>
		/// Read the configuration from config.txt
		/// </summary>
		/// <param name="cWindows">List of configured Windows</param>
		private static void ReadConfig(ref List<Window> cWindows) {
			string file = "config.txt";

			//write default config if no config exists
			if( !File.Exists(file) ) {
				string[] defaultCfg = {
					"# BMax configuration file",
					"# -----------------------",
					"#",
					"# Comment lines start with a hash symbol. To specifiy an application to",
					"# be maximized, enter the exact window title, followed by a comma and the",
					"# optional, but recommended, WindowClass.",
					"Diablo III,D3 Main Window Class",
					"Minecraft Launcher,SunAwtFrame",
					"# You can also specify Windows without the WindowClass, but this can lead",
					"# to false positives. The comma is omitted in this case.",
					"Warcraft III",
				};
				File.WriteAllLines(file, defaultCfg);
			}

			// read config
			string[] lines = File.ReadAllLines(file);
			foreach( string line in lines ) {
				if( line.StartsWith("#") )
					continue; //ignore comments
				string[] words = line.Split(',');
				Window g = new Window();
				if( words.Length > 1 ) {
					g.Title = words[0];
					g.WindowClass = words[1];
					cWindows.Add(g);
				} else if( words.Length == 1 ) {
					g.Title = words[0];
					g.WindowClass = "";
					cWindows.Add(g);
				}
			}
			return;
		}
		//--------------------------------------------------------------------------------------------
		/// <summary>
		/// Periodically look windows with a matching title/window class and maximize them
		/// </summary>
		private static void MainLoop() {
			IntPtr wHandle = (IntPtr)0;
			while( true ) {
				foreach( Window g in cWindows ) {
					wHandle = FindWindow(g.WindowClass, g.Title);
					if( wHandle != (IntPtr)0 ) {
						Maximize(wHandle);
					}
				}
				Thread.Sleep(1000);
			}
		}
		//--------------------------------------------------------------------------------------------
		/// <summary>
		/// Maximize the window
		/// </summary>
		/// <param name="wHandle">the HWND of the window</param>
		/// <param name="wTitle">optional Window Title</param>
		/// <param name="wClass">optional Window Class</param>
		private static void Maximize(IntPtr wHandle, string wTitle = "", string wClass = "") {

			if( wTitle != "" && wClass != "" )
				wHandle = FindWindow(wClass, wTitle);

			RECT r;
			r.Left = bounds.Left;
			r.Right = bounds.Right;
			r.Top = bounds.Top;
			r.Bottom = bounds.Bottom;

			int ws = GetWindowLong(wHandle, -16);
			SetWindowLong(wHandle, -16, ws & ~(0x00040000 | 0x00C00000));

			SetWindowPos(wHandle, (IntPtr)(0), r.Left + BORDER_LEFT,
												r.Top + BORDER_TOP,
												r.Right - BORDER_RIGHT - BORDER_LEFT,
												r.Bottom - BORDER_BOTTOM - BORDER_TOP,
												(SetWindowPosFlags)0x0020);

			ShowWindow(wHandle, (ShowWindowCommands)5);
		}
		//--------------------------------------------------------------------------------------------
		/// <summary>
		/// Helper Class
		/// </summary>
		public class Window {
			public IntPtr handle;
			public string Title;
			public string WindowClass;
		}
		//--------------------------------------------------------------------------------------------
		/// <summary>
		/// Helper Class to get the list of all active Windows
		/// </summary>
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
