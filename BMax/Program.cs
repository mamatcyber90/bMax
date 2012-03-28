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
		static List<App> apps = new List<App>();
		static Rectangle bounds = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
		static NotifyIcon trayIcon;
		static ContextMenu trayMenu;
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
		private BMaxApp() {
			trayMenu = new ContextMenu();
			trayMenu.MenuItems.Add("E&xit", Exit);

			trayIcon = new NotifyIcon();
			trayIcon.Text = "BMax";
			trayIcon.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
			trayIcon.ContextMenu = trayMenu;
			trayIcon.Visible = true;

			ReadConfig(ref apps);

			foreach( App a in apps ) {
				DebugPrint("Title: \t\t" + a.Title + "\n");
				DebugPrint("WindowClass: \t" + a.WindowClass + "\n\n");
			}

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

			if( apps.Count > 0 ) {
				DebugPrint("Entering Main Loop...");
				workerThread = new Thread(MainLoop);
				workerThread.Start();
			} else {
				Console.WriteLine("No apps to maximize found, exiting...");
				Console.ReadLine();
				Application.Exit();
			}
		}
//--------------------------------------------------------------------------------------------
		private void Exit(object sender, EventArgs e) {
			workerThread.Abort();
			Application.Exit();
		}
//--------------------------------------------------------------------------------------------
		/// <summary>
		/// Read the configuration from config.txt
		/// </summary>
		private static bool ReadConfig(ref List<App> apps) {
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
				if( line.StartsWith("#") ) continue; //ignore comments
				string[] words = line.Split(',');
				App g = new App();
				if( words.Length > 1 ) {
					g.Title = words[0];
					g.WindowClass = words[1];
					apps.Add(g);
				} else if( words.Length == 1 ) {
					g.Title = words[0];
					g.WindowClass = "";
					apps.Add(g);
				}
			}
			return true;
		}
//--------------------------------------------------------------------------------------------
		/// <summary>
		/// Periodically look windows with a matching title/window class and maximize them
		/// </summary>
		private static void MainLoop() {
			IntPtr wHandle = (IntPtr)0;
			while( true ) {
				foreach( App g in apps ) {
					wHandle = FindWindow(g.WindowClass, g.Title);
					if( wHandle != (IntPtr)0 ) {
						DebugPrint("\nFound: " + g.WindowClass + ", HWND=" + wHandle.ToString());
						Maxmize(wHandle);
					}
				}
				Thread.Sleep(1000);
			}
		}
//--------------------------------------------------------------------------------------------
		/// <summary>
		/// Maximize the window
		/// </summary>
		private static void Maxmize(IntPtr wHandle) {
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
		/// Debug Print
		/// </summary>
		private static void DebugPrint(string s) {
		#if DEBUG
			Console.Write(s);
		#endif
		}
	}
//--------------------------------------------------------------------------------------------
	public class App {
		public string Title;
		public string WindowClass;
	}
}
