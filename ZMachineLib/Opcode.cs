using System.Collections.Generic;

namespace ZMachineLib
{
	internal delegate void OpcodeHandler(List<ushort> args);

	internal struct Opcode
	{
		public string Name { get; set; }
		public OpcodeHandler Handler { get; set; }
	}
}