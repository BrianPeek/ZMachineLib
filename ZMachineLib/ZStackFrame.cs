using System.Collections.Generic;
using System.Runtime.Serialization;

namespace ZMachineLib
{
	[DataContract]
	internal class ZStackFrame
	{
		[DataMember]
		public uint PC { get; set; }
		[DataMember]
		public Stack<ushort> RoutineStack { get; set; }
		[DataMember]
		public ushort[] Variables { get; set; }
		[DataMember]
		public bool StoreResult { get; set; }
		[DataMember]
		public int ArgumentCount { get; set; }

		public ZStackFrame()
		{
			Variables = new ushort[0x10];
			RoutineStack = new Stack<ushort>();
			StoreResult = true;
		}
	}
}