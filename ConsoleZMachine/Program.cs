using System.IO;
using ZMachineLib;

namespace ConsoleZMachine
{
	class Program
	{
		static void Main(string[] args)
		{
			ZMachine zMachine = new ZMachine(new ConsoleIO());

			FileStream fs = File.OpenRead(@"zork1.dat");
			zMachine.LoadFile(fs);
			zMachine.Run();
		}
	}
}
