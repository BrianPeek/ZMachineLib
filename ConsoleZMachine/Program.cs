using System.IO;
using ZMachineLib;

namespace ConsoleZMachine
{
	class Program
	{
		static void Main(string[] args)
		{
			ZMachine zMachine = new ZMachine(new ConsoleIO());

			FileStream fs = File.OpenRead(args[0]);
			zMachine.LoadFile(fs);
			zMachine.Run();
		}
	}
}
