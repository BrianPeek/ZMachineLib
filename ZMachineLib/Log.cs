using System.Diagnostics;
using System.Text;

namespace ZMachineLib
{
	internal static class Log
	{
		private static readonly StringBuilder _output = new StringBuilder();

		public static void Write(string s)
		{
			_output.Append(s);
		}

		public static void WriteLine(string s)
		{
			_output.AppendLine(s);
		}

		public static void Flush()
		{
			Print();
			_output.Clear();
		}

		public static void Print()
		{
			Debug.WriteLine(_output.ToString());
		}
	}
}
