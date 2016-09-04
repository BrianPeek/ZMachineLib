using System;
using System.IO;

namespace ZMachineLib
{
	[Flags]
	public enum TextStyle
	{
		Roman = 0,
		Reverse = 1,
		Bold = 2,
		Italic = 4,
		FixedPitch = 8
	}

	public interface IZMachineIO
	{
		void Print(string s);
		string Read(int max);
		char ReadChar();
		void SetCursor(ushort line, ushort column, ushort window);
		void SetWindow(ushort window);
		void EraseWindow(ushort window);
		void BufferMode(bool buffer);
		void SplitWindow(ushort lines);
		void ShowStatus();
		void SetTextStyle(TextStyle textStyle);
		bool Save(Stream stream);
		Stream Restore();
		void SetColor(ZColor foreground, ZColor background);
		void SoundEffect(ushort number);
		void Quit();
	}
}
