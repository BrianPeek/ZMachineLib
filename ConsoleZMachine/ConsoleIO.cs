#define SIMPLE_IO

using System;
using System.IO;
using ZMachineLib;

namespace ConsoleZMachine
{
	public class ConsoleIO : IZMachineIO
	{
		private int _lines;
		private readonly ConsoleColor _defaultFore;
		private readonly ConsoleColor _defaultBack;

		public ConsoleIO()
		{
			Console.SetBufferSize(Console.WindowWidth, Console.WindowHeight);
			Console.SetCursorPosition(0, Console.WindowHeight-1);
			_defaultFore = Console.ForegroundColor;
			_defaultBack = Console.BackgroundColor;
		}

		public void Print(string s)
		{
			for(int i = 0; i < s.Length; i++)
			{
				if(s[i] == ' ')
				{
					int next = s.IndexOf(' ', i+1);
					if(next == -1)
						next = s.Length;
					if(next >= 0)
					{
						if(Console.CursorLeft + (next - i) >= Console.WindowWidth)
						{
							Console.MoveBufferArea(0, 0, Console.WindowWidth, _lines, 0, 1);
							Console.WriteLine("");

							i++;
						}
					}
				}

				if(i < s.Length && s[i] == Environment.NewLine[0])
					Console.MoveBufferArea(0, 0, Console.WindowWidth, _lines, 0, 1);

				if(i < s.Length)
					Console.Write(s[i]);
			}
		}

		public string Read(int max)
		{
#if SIMPLE_IO
			string s = Console.ReadLine();
			Console.MoveBufferArea(0, 0, Console.WindowWidth, _lines, 0, 1);
			return s?.Substring(0, Math.Min(s.Length, max));
#else
			string s = string.Empty;
			ConsoleKeyInfo key = new ConsoleKeyInfo();

			do
			{
				if(Console.KeyAvailable)
				{
					key = Console.ReadKey(true);
					switch(key.Key)
					{
						case ConsoleKey.Backspace:
							if(s.Length > 0)
							{
								s = s.Remove(s.Length-1, 1);
								Console.Write(key.KeyChar);
							}
							break;
						case ConsoleKey.Enter:
							break;
						default:
							s += key.KeyChar;
							Console.Write(key.KeyChar);
							break;
					}
				}
			}
			while(key.Key != ConsoleKey.Enter);

			Console.MoveBufferArea(0, 0, Console.WindowWidth, _lines, 0, 1);
			Console.WriteLine(string.Empty);
			return s;
#endif
		}

		public char ReadChar()
		{
			return Console.ReadKey(true).KeyChar;
		}

		public void SetCursor(ushort line, ushort column, ushort window)
		{
			Console.SetCursorPosition(column-1, line-1);
		}

		public void SetWindow(ushort window)
		{
			if(window == 0)
				Console.SetCursorPosition(0, Console.WindowHeight-1);
		}

		public void EraseWindow(ushort window)
		{
			ConsoleColor c = Console.BackgroundColor;
			Console.BackgroundColor = _defaultBack;
			Console.Clear();
			Console.BackgroundColor = c;
		}

		public void BufferMode(bool buffer)
		{
		}

		public void SplitWindow(ushort lines)
		{
			_lines = lines;
		}

		public void ShowStatus()
		{
		}

		public void SetTextStyle(TextStyle textStyle)
		{
			switch(textStyle)
			{
				case TextStyle.Roman:
					Console.ResetColor();
					break;
				case TextStyle.Reverse:
					ConsoleColor temp = Console.BackgroundColor;
					Console.BackgroundColor = Console.ForegroundColor;
					Console.ForegroundColor = temp;
					break;
				case TextStyle.Bold:
					break;
				case TextStyle.Italic:
					break;
				case TextStyle.FixedPitch:
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(textStyle), textStyle, null);
			}
		}

		public void SetColor(ZColor foreground, ZColor background)
		{
			Console.ForegroundColor = ZColorToConsoleColor(foreground, true);
			Console.BackgroundColor = ZColorToConsoleColor(background, false);
		}

		public void SoundEffect(ushort number)
		{
			if(number == 1)
				Console.Beep(2000, 300);
			else if(number == 2)
				Console.Beep(250, 300);
			else
				throw new Exception("Sound > 2");
		}

		public void Quit()
		{
		}

		private ConsoleColor ZColorToConsoleColor(ZColor c, bool fore)
		{
			switch(c)
			{
				case ZColor.PixelUnderCursor:
				case ZColor.Current:
					return fore ? Console.ForegroundColor : Console.BackgroundColor;
				case ZColor.Default:
					return fore ? _defaultFore : _defaultBack;
				case ZColor.Black:
					return ConsoleColor.Black;
				case ZColor.Red:
					return ConsoleColor.Red;
				case ZColor.Green:
					return ConsoleColor.Green;
				case ZColor.Yellow:
					return ConsoleColor.Yellow;
				case ZColor.Blue:
					return ConsoleColor.Blue;
				case ZColor.Magenta:
					return ConsoleColor.Magenta;
				case ZColor.Cyan:
					return ConsoleColor.Cyan;
				case ZColor.White:
					return ConsoleColor.White;
				case ZColor.DarkishGrey:
					return ConsoleColor.DarkGray;
				case ZColor.LightGrey:
					return ConsoleColor.Gray;
				case ZColor.MediumGrey:
					return ConsoleColor.Gray;
				case ZColor.DarkGrey:
					return ConsoleColor.DarkGray;
			}
			return Console.ForegroundColor;
		}

		public bool Save(Stream s)
		{
			FileStream fs = File.Create(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "save"));
			s.CopyTo(fs);
			fs.Close();
			return true;
		}

		public Stream Restore()
		{
			FileStream fs = File.OpenRead(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "save"));
			return fs;
		}
	}
}
