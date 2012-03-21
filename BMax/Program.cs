using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.IO;
using System.Runtime;
using System.Runtime.InteropServices;


namespace BMax {
	class Program {
//--------------------------------------------------------------------------------------------
		[DllImport("user32.dll")]
		static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

		[StructLayout(LayoutKind.Sequential)]
		public struct RECT {
			public int Left; // x position of upper-left corner
			public int Top; // y position of upper-left corner
			public int Right; // x position of lower-right corner
			public int Bottom; // y position of lower-right corner
		}

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
		private enum SetWindowPosFlags : uint {
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
		public static List<Game> games = new List<Game>();
		static void Main(string[] args) {
			
			if(!ReadConfig(ref games)){
				Console.Write("Error: config.txt not found!");
				Console.ReadLine();
			}

			foreach( Game g in games ) {
				DebugPrint("Title: \t\t" + g.Title + "\n");
				DebugPrint("WindowClass: \t" + g.WindowClass + "\n\n");
			}

			if( games.Count > 0 ) {
				DebugPrint("Entering Main Loop...");
				MainLoop();
			}
			Console.ReadLine();
			
		}
//--------------------------------------------------------------------------------------------
		/// <summary>
		/// Read the configuration from config.txt
		/// </summary>
		static bool ReadConfig(ref List<Game> games) {
			string[] lines = File.ReadAllLines("config.txt");
			if( lines[0] == null )
				return false;
			foreach( string line in lines ) {
				if( line.StartsWith("#") ) continue; //ignore comments
				string[] words = line.Split(',');
				Game g = new Game();
				if( words.Length > 1 ) {
					g.Title = words[0];
					g.WindowClass = words[1];
					games.Add(g);
				} else if( words.Length == 1 ) {
					g.WindowClass = words[0];
					g.Title = "";
					games.Add(g);
				}
			}
			return true;
		}
//--------------------------------------------------------------------------------------------
		/// <summary>
		/// Periodically look windows with a matching title/window class and maximize them
		/// </summary>
		static void MainLoop() {
			IntPtr wHandle = (IntPtr)0;
			while( true ) {
				foreach( Game g in games ) {
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
		static void Maxmize(IntPtr wHandle) {
			var bounds = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
			RECT r;
			r.Left = bounds.Left;
			r.Right = bounds.Right;
			r.Top = bounds.Top;
			r.Bottom = bounds.Bottom;

			DebugPrint(" Left=" + r.Left.ToString());
			DebugPrint(", Right=" + r.Right.ToString());
			DebugPrint(", Top=" + r.Top.ToString());
			DebugPrint(", Bottom=" + r.Bottom.ToString());

			int ws = GetWindowLong(wHandle, -16);
			SetWindowLong(wHandle, -16, ws & ~(0x00040000 | 0x00C00000));
			SetWindowPos(wHandle, (IntPtr)(0), r.Left, r.Top, r.Right, r.Bottom -30 , (SetWindowPosFlags)0x0020);
			ShowWindow(wHandle, (ShowWindowCommands)5);

		}

//--------------------------------------------------------------------------------------------
		/// <summary>
		/// Debug Print
		/// </summary>
		static void DebugPrint(string s) {
		#if DEBUG
			Console.Write(s);
		#endif
		}
	}

	public class Game {
		public string Title;
		public string WindowClass;
	}
}
