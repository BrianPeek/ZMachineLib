using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace ZMachineLib
{
	public class ZMachine
	{
		private const int ParentOffsetV3   = 4;
		private const int SiblingOffsetV3  = 5;
		private const int ChildOffsetV3    = 6;
		private const int PropertyOffsetV3 = 7;
		private const int ObjectSizeV3     = 9;
		private const int PropertyDefaultTableSizeV3 = 62;

		private const int ParentOffsetV5   = 6;
		private const int SiblingOffsetV5  = 8;
		private const int ChildOffsetV5    = 10;
		private const int PropertyOffsetV5 = 12;
		private const int ObjectSizeV5     = 14;
		private const int PropertyDefaultTableSizeV5 = 126;

		private int ParentOffset;
		private int SiblingOffset;
		private int ChildOffset;
		private int PropertyOffset;
		private int ObjectSize;
		private int PropertyDefaultTableSize;

		private const int Version = 0x00;
		private const int InitialPC = 0x06;
		private const int DictionaryOffset = 0x08;
		private const int ObjectTableOffset = 0x0a;
		private const int GlobalVarOffset = 0x0c;
		private const int StaticMemoryOffset = 0x0e;
		private const int AbbreviationTableOffset = 0x18;

		private readonly IZMachineIO _io;
		private byte[] _memory;
		private Stream _file;
		private bool _running;
		private Random _random = new Random();
		private string[] _dictionaryWords;

		private byte _version;
		private ushort _pc;
		private ushort _globals;
		private ushort _objectTable;
		private ushort _abbreviationsTable;
		private ushort _dictionary;
		private ushort _dynamicMemory;
		private ushort _readTextAddr;
		private ushort _readParseAddr;
		private bool _terminateOnInput;

		private Stack<ZStackFrame> _stack = new Stack<ZStackFrame>();
		private readonly Opcode[] _0Opcodes   = new Opcode[0x10];
		private readonly Opcode[] _1Opcodes   = new Opcode[0x10];
		private readonly Opcode[] _2Opcodes   = new Opcode[0x20];
		private readonly Opcode[] _varOpcodes = new Opcode[0x20];
		private readonly Opcode[] _extOpcodes = new Opcode[0x20];

		private readonly Opcode UnknownOpcode = new Opcode { Handler = delegate { Log.Flush(); throw new Exception("Unknown opcode"); }, Name = "UNKNOWN" };

		private readonly string _table = @" ^0123456789.,!?_#'""/\-:()";
		private byte _entryLength;
		private ushort _wordStart;

		public ZMachine(IZMachineIO io)
		{
			_io = io;

			InitOpcodes(_0Opcodes);
			InitOpcodes(_1Opcodes);
			InitOpcodes(_2Opcodes);
			InitOpcodes(_varOpcodes);
			InitOpcodes(_extOpcodes);
		}

		private void InitOpcodes(Opcode[] opcodes)
		{
			for(int i = 0; i < opcodes.Length; i++)
				opcodes[i] = UnknownOpcode;
		}

		private void SetupOpcodes()
		{
			_0Opcodes[0x00] = new Opcode { Handler = RTrue,           Name = "RTRUE" };
			_0Opcodes[0x01] = new Opcode { Handler = RFalse,          Name = "RFALSE" };
			_0Opcodes[0x02] = new Opcode { Handler = Print,           Name = "PRINT" };
			_0Opcodes[0x03] = new Opcode { Handler = PrintRet,        Name = "PRINT_RET" };
			_0Opcodes[0x04] = new Opcode { Handler = Nop,             Name = "NOP" };
			_0Opcodes[0x05] = new Opcode { Handler = Save,            Name = "SAVE" };
			_0Opcodes[0x06] = new Opcode { Handler = Restore,         Name = "RESTORE" };
			_0Opcodes[0x07] = new Opcode { Handler = Restart,         Name = "RESTART" };
			_0Opcodes[0x08] = new Opcode { Handler = RetPopped,       Name = "RET_POPPED" };
			_0Opcodes[0x09] = new Opcode { Handler = Pop,             Name = "POP" };
			_0Opcodes[0x0a] = new Opcode { Handler = Quit,            Name = "QUIT" };
			_0Opcodes[0x0b] = new Opcode { Handler = NewLine,         Name = "NEW_LINE" };
			_0Opcodes[0x0c] = new Opcode { Handler = ShowStatus,      Name = "SHOW_STATUS" };
			_0Opcodes[0x0d] = new Opcode { Handler = Verify,          Name = "VERIFY" };
			_0Opcodes[0x0f] = new Opcode { Handler = Piracy,          Name = "PIRACY" };

			_1Opcodes[0x00] = new Opcode { Handler = Jz,              Name = "JZ" };
			_1Opcodes[0x01] = new Opcode { Handler = GetSibling,      Name = "GET_SIBLING" };
			_1Opcodes[0x02] = new Opcode { Handler = GetChild,        Name = "GET_CHILD" };
			_1Opcodes[0x03] = new Opcode { Handler = GetParent,       Name = "GET_PARENT" };
			_1Opcodes[0x04] = new Opcode { Handler = GetPropLen,      Name = "GET_PROP_LEN" };
			_1Opcodes[0x05] = new Opcode { Handler = Inc,             Name = "INC" };
			_1Opcodes[0x06] = new Opcode { Handler = Dec,             Name = "DEC" };
			_1Opcodes[0x07] = new Opcode { Handler = PrintAddr,       Name = "PRINT_ADDR" };
			_1Opcodes[0x08] = new Opcode { Handler = Call1S,          Name = "CALL_1S" };
			_1Opcodes[0x09] = new Opcode { Handler = RemoveObj,       Name = "REMOVE_OBJ" };
			_1Opcodes[0x0a] = new Opcode { Handler = PrintObj,        Name = "PRINT_OBJ" };
			_1Opcodes[0x0b] = new Opcode { Handler = Ret,             Name = "RET" };
			_1Opcodes[0x0c] = new Opcode { Handler = Jump,            Name = "JUMP" };
			_1Opcodes[0x0d] = new Opcode { Handler = PrintPAddr,      Name = "PRINT_PADDR" };
			_1Opcodes[0x0e] = new Opcode { Handler = Load,            Name = "LOAD" };
		if(_version <= 4)
			_1Opcodes[0x0f] = new Opcode { Handler = Not,             Name = "NOT" };
		else
			_1Opcodes[0x0f] = new Opcode { Handler = Call1N,          Name = "CALL_1N" };

			_2Opcodes[0x01] = new Opcode { Handler = Je,              Name = "JE" };
			_2Opcodes[0x02] = new Opcode { Handler = Jl,              Name = "JL" };
			_2Opcodes[0x03] = new Opcode { Handler = Jg,              Name = "JG" };
			_2Opcodes[0x04] = new Opcode { Handler = DecCheck,        Name = "DEC_CHECK" };
			_2Opcodes[0x05] = new Opcode { Handler = IncCheck,        Name = "INC_CHECK" };
			_2Opcodes[0x06] = new Opcode { Handler = Jin,             Name = "JIN" };
			_2Opcodes[0x07] = new Opcode { Handler = Test,            Name = "TEST" };
			_2Opcodes[0x08] = new Opcode { Handler = Or,              Name = "OR" };
			_2Opcodes[0x09] = new Opcode { Handler = And,             Name = "AND" };
			_2Opcodes[0x0a] = new Opcode { Handler = TestAttribute,   Name = "TEST_ATTR" };
			_2Opcodes[0x0b] = new Opcode { Handler = SetAttribute,    Name = "SET_ATTR" };
			_2Opcodes[0x0c] = new Opcode { Handler = ClearAttribute,  Name = "CLEAR_ATTR" };
			_2Opcodes[0x0d] = new Opcode { Handler = Store,           Name = "STORE" };
			_2Opcodes[0x0e] = new Opcode { Handler = InsertObj,       Name = "INSERT_OBJ" };
			_2Opcodes[0x0f] = new Opcode { Handler = LoadW,           Name = "LOADW" };
			_2Opcodes[0x10] = new Opcode { Handler = LoadB,           Name = "LOADB" };
			_2Opcodes[0x11] = new Opcode { Handler = GetProp,         Name = "GET_PROP" };
			_2Opcodes[0x12] = new Opcode { Handler = GetPropAddr,     Name = "GET_PROP_ADDR" };
			_2Opcodes[0x13] = new Opcode { Handler = GetNextProp,     Name = "GET_NEXT_PROP" };
			_2Opcodes[0x14] = new Opcode { Handler = Add,             Name = "ADD" };
			_2Opcodes[0x15] = new Opcode { Handler = Sub,             Name = "SUB" };
			_2Opcodes[0x16] = new Opcode { Handler = Mul,             Name = "MUL" };
			_2Opcodes[0x17] = new Opcode { Handler = Div,             Name = "DIV" };
			_2Opcodes[0x18] = new Opcode { Handler = Mod,             Name = "MOD" };
			_2Opcodes[0x19] = new Opcode { Handler = Call2S,          Name = "CALL_2S" };
			_2Opcodes[0x1a] = new Opcode { Handler = Call2N,          Name = "CALL_2N" };
			_2Opcodes[0x1b] = new Opcode { Handler = SetColor,        Name = "SET_COLOR" };

			_varOpcodes[0x00] = new Opcode { Handler = Call,          Name = "CALL(_VS)" };
			_varOpcodes[0x01] = new Opcode { Handler = StoreW,        Name = "STOREW" };
			_varOpcodes[0x02] = new Opcode { Handler = StoreB,        Name = "STOREB" };
			_varOpcodes[0x03] = new Opcode { Handler = PutProp,       Name = "PUT_PROP" };
			_varOpcodes[0x04] = new Opcode { Handler = Read,          Name = "READ" };
			_varOpcodes[0x05] = new Opcode { Handler = PrintChar,     Name = "PRINT_CHAR" };
			_varOpcodes[0x06] = new Opcode { Handler = PrintNum,      Name = "PRINT_NUM" };
			_varOpcodes[0x07] = new Opcode { Handler = Random,        Name = "RANDOM" };
			_varOpcodes[0x08] = new Opcode { Handler = Push,          Name = "PUSH" };
			_varOpcodes[0x09] = new Opcode { Handler = Pull,          Name = "PULL" };
			_varOpcodes[0x0a] = new Opcode { Handler = SplitWindow,   Name = "SPLIT_WINDOW" };
			_varOpcodes[0x0b] = new Opcode { Handler = SetWindow,     Name = "SET_WINDOW" };
			_varOpcodes[0x0c] = new Opcode { Handler = CallVS2,       Name = "CALL_VS2" };
			_varOpcodes[0x0d] = new Opcode { Handler = EraseWindow,   Name = "ERASE_WINDOW" };
			_varOpcodes[0x0f] = new Opcode { Handler = SetCursor,     Name = "SET_CURSOR" };
			_varOpcodes[0x11] = new Opcode { Handler = SetTextStyle,  Name = "SET_TEXT_STYLE" };
			_varOpcodes[0x12] = new Opcode { Handler = BufferMode,    Name = "BUFFER_MODE" };
			_varOpcodes[0x13] = new Opcode { Handler = OutputStream,  Name = "OUTPUT_STREAM" };
			_varOpcodes[0x15] = new Opcode { Handler = SoundEffect,   Name = "SOUND_EFFECT" };
			_varOpcodes[0x16] = new Opcode { Handler = ReadChar,      Name = "READ_CHAR" };
			_varOpcodes[0x17] = new Opcode { Handler = ScanTable,     Name = "SCAN_TABLE" };
			_varOpcodes[0x18] = new Opcode { Handler = Not,           Name = "NOT" };
			_varOpcodes[0x19] = new Opcode { Handler = CallVN,        Name = "CALL_VN" };
			_varOpcodes[0x1a] = new Opcode { Handler = CallVN2,       Name = "CALL_VN2" };
			_varOpcodes[0x1d] = new Opcode { Handler = CopyTable,     Name = "COPY_TABLE" };
			_varOpcodes[0x1e] = new Opcode { Handler = PrintTable,    Name = "PRINT_TABLE" };
			_varOpcodes[0x1f] = new Opcode { Handler = CheckArgCount, Name = "CHECK_ARG_COUNT" };

			_extOpcodes[0x00] = new Opcode { Handler = Save,          Name = "SAVE" };
			_extOpcodes[0x01] = new Opcode { Handler = Restore,       Name = "RESTORE" };
			_extOpcodes[0x02] = new Opcode { Handler = LogShift,      Name = "LOG_SHIFT" };
			_extOpcodes[0x03] = new Opcode { Handler = ArtShift,      Name = "ART_SHIFT" };
			_extOpcodes[0x04] = new Opcode { Handler = SetFont,       Name = "SET_FONT" };
		}

		public void LoadFile(Stream stream)
		{
			_file = stream;

			_memory = new byte[stream.Length];
			stream.Seek(0, SeekOrigin.Begin);
			stream.Read(_memory, 0, (int)stream.Length);

			_version            = _memory[Version];
			_pc                 = GetWord(InitialPC);
			_dictionary         = GetWord(DictionaryOffset);
			_objectTable        = GetWord(ObjectTableOffset);
			_globals            = GetWord(GlobalVarOffset);
			_abbreviationsTable = GetWord(AbbreviationTableOffset);
			_dynamicMemory      = GetWord(StaticMemoryOffset);

			// TODO: set these via IZMachineIO
			_memory[0x01] = 0x01;
			_memory[0x20] = 25;
			_memory[0x21] = 80;

			SetupOpcodes();
			ParseDictionary();

			if(_version <= 3)
			{
				ParentOffset = ParentOffsetV3;
				SiblingOffset = SiblingOffsetV3;
				ChildOffset = ChildOffsetV3;
				PropertyOffset = PropertyOffsetV3;
				ObjectSize = ObjectSizeV3;
				PropertyDefaultTableSize = PropertyDefaultTableSizeV3;
			}
			else if (_version <= 5)
			{
				ParentOffset = ParentOffsetV5;
				SiblingOffset = SiblingOffsetV5;
				ChildOffset = ChildOffsetV5;
				PropertyOffset = PropertyOffsetV5;
				ObjectSize = ObjectSizeV5;
				PropertyDefaultTableSize = PropertyDefaultTableSizeV5;
			}

			ZStackFrame zsf = new ZStackFrame { PC = _pc };
			_stack.Push(zsf);
		}

		public void Run(bool terminateOnInput = false)
		{
			_terminateOnInput = terminateOnInput;

			_running = true;

			while(_running)
			{
				Opcode? opcode;

				Log.Write($"PC: {_stack.Peek().PC:X5}");
				byte o = _memory[_stack.Peek().PC++];
				if(o == 0xbe)
				{
					o = _memory[_stack.Peek().PC++];
					opcode = _extOpcodes?[o & 0x1f];
					// TODO: hack to make this a VAR opcode...
					o |= 0xc0;
				}
				else if(o < 0x80)
					opcode = _2Opcodes?[o & 0x1f];
				else if (o < 0xb0)
					opcode = _1Opcodes?[o & 0x0f];
				else if (o < 0xc0)
					opcode = _0Opcodes?[o & 0x0f];
				else if (o < 0xe0)
					opcode = _2Opcodes?[o & 0x1f];
				else
					opcode = _varOpcodes?[o & 0x1f];
				Log.Write($" Op ({o:X2}): {opcode?.Name} ");
				var args = GetOperands(o);
				opcode?.Handler(args);
				Log.Flush();
			}
		}

		private void Nop(List<ushort> args)
		{
		}

		private void Save(List<ushort> args)
		{
			Stream s = SaveState();
			bool val = _io.Save(s);

			if(_version < 5)
				Jump(val);
			else
			{
				byte dest = _memory[_stack.Peek().PC++];
				StoreWordInVariable(dest, (ushort)(val ? 1 : 0));
			}
		}

		public Stream SaveState()
		{
			MemoryStream ms = new MemoryStream();
			BinaryWriter bw = new BinaryWriter(ms);
			bw.Write(_readParseAddr);
			bw.Write(_readTextAddr);
			bw.Write(_memory, 0, _dynamicMemory - 1);
			DataContractJsonSerializer dcs = new DataContractJsonSerializer(typeof(Stack<ZStackFrame>));
			dcs.WriteObject(ms, _stack);
			ms.Position = 0;
			return ms;
		}

		private void Restore(List<ushort> args)
		{
			Stream stream = _io.Restore();
			if(stream != null)
				RestoreState(stream);

			if(_version < 5)
				Jump(stream != null);
			else
			{
				byte dest = _memory[_stack.Peek().PC++];
				StoreWordInVariable(dest, (ushort)(stream != null ? 1 : 0));
			}
		}

		public void RestoreState(Stream stream)
		{
			BinaryReader br = new BinaryReader(stream);
			stream.Position = 0;
			_readParseAddr = br.ReadUInt16();
			_readTextAddr = br.ReadUInt16();
			stream.Read(_memory, 0, _dynamicMemory - 1);
			DataContractJsonSerializer dcs = new DataContractJsonSerializer(typeof(Stack<ZStackFrame>));
			_stack = (Stack<ZStackFrame>)dcs.ReadObject(stream);
			stream.Dispose();
		}

		private void Restart(List<ushort> args)
		{
			LoadFile(_file);
		}

		private void Quit(List<ushort> args)
		{
			_running = false;
			_io.Quit();
		}

		private void Verify(List<ushort> args)
		{
			// TODO: checksum
			Jump(true);
		}

		private void Piracy(List<ushort> args)
		{
			Jump(true);
		}

		private void ScanTable(List<ushort> args)
		{
			byte dest = _memory[_stack.Peek().PC++];
			byte len = 0x02;

			if(args.Count == 4)
				len = (byte)(args[3] & 0x7f);

			for(int i = 0; i < args[2]; i++)
			{
				ushort addr = (ushort)(args[1] + i*len);
				ushort val;

				if(args.Count == 3 || (args[3] & 0x80) == 0x80)
					val = GetWord(addr);
				else
					val = _memory[addr];

				if(val == args[0])
				{
					StoreWordInVariable(dest, addr);
					Jump(true);
					return;
				}
			}

			StoreWordInVariable(dest, 0);
			Jump(false);
		}

		private void CopyTable(List<ushort> args)
		{
			if(args[1] == 0)
			{
				for(int i = 0; i < args[2]; i++)
					_memory[args[0] + i] = 0;
			}
			else if((short)args[1] < 0)
			{
				for(int i = 0; i < Math.Abs(args[2]); i++)
					_memory[args[1] + i] = _memory[args[0] + i];
			}
			else
			{
				for(int i = Math.Abs(args[2])-1; i >=0 ; i--)
					_memory[args[1] + i] = _memory[args[0] + i];
			}
		}

		private void PrintTable(List<ushort> args)
		{
			// TODO: print properly

			List<byte> chars = GetZsciiChars(args[0]);
			string s = DecodeZsciiChars(chars);
			_io.Print(s);
			Log.Write($"[{s}]");
		}

		private void Print(List<ushort> args)
		{
			List<byte> chars = GetZsciiChars(_stack.Peek().PC);
			_stack.Peek().PC += (ushort)(chars.Count/3*2);
			string s = DecodeZsciiChars(chars);
			_io.Print(s);
			Log.Write($"[{s}]");
		}

		private string DecodeZsciiChars(List<byte> chars)
		{
			StringBuilder sb = new StringBuilder();
			for(int i = 0; i < chars.Count; i++)
			{
				if(chars[i] == 0x00)
					sb.Append(" ");
				else if(chars[i] >= 0x01 && chars[i] <= 0x03)
				{
					ushort offset = (ushort)(32*(chars[i]-1) + chars[++i]);
					ushort lookup = (ushort)(_abbreviationsTable + (offset*2));
					ushort wordAddr = GetWord(lookup);
					List<byte> abbrev = GetZsciiChars((ushort)(wordAddr*2));
					sb.Append(DecodeZsciiChars(abbrev));
				}
				else if(chars[i] == 0x04)
					sb.Append(Convert.ToChar((chars[++i]-6)+'A'));
				else if(chars[i] == 0x05)
				{
					if(i == chars.Count-1 || chars[i+1] == 0x05)
						break;

					if(chars[i+1] == 0x06)
					{
						ushort x = (ushort)(chars[i+2] << 5 | chars[i+3]);
						i+= 3;
						sb.Append(Convert.ToChar(x));
					}
					else if(chars[i+1] == 0x07)
					{
						sb.AppendLine("");
						i++;
					}
					else
						sb.Append(_table[chars[++i]-6]);
				}
				else
					sb.Append(Convert.ToChar((chars[i]-6)+'a'));
			}
			return sb.ToString();
		}

		private List<byte> GetZsciiChars(uint address)
		{
			List<byte> chars = new List<byte>();
			ushort word;
			do
			{
				word = GetWord(address);
				chars.AddRange(GetZsciiChar(address));
				address += 2;
			}
			while((word & 0x8000) != 0x8000);

			return chars;
		}

		private List<byte> GetZsciiChar(uint address)
		{
			List<byte> chars = new List<byte>();

			var word = GetWord(address);

			byte c = (byte)(word >> 10 & 0x1f);
			chars.Add(c);
			c = (byte)(word >>  5 & 0x1f);
			chars.Add(c);
			c = (byte)(word >>  0 & 0x1f);
			chars.Add(c);

			return chars;
		}

		private void NewLine(List<ushort> args)
		{
			_io.Print(Environment.NewLine);
		}

		private void PrintNum(List<ushort> args)
		{
			string s = args[0].ToString();
			_io.Print(s);
			Log.Write($"[{s}]");
		}

		private void PrintChar(List<ushort> args)
		{
			string s = Convert.ToChar(args[0]).ToString();
			_io.Print(s);
			Log.Write($"[{s}]");
		}

		private void PrintRet(List<ushort> args)
		{
			List<byte> chars = GetZsciiChars(_stack.Peek().PC);
			_stack.Peek().PC += (ushort)(chars.Count/3*2);
			string s = DecodeZsciiChars(chars);
			_io.Print(s + Environment.NewLine);
			Log.Write($"[{s}]");
			RTrue(null);
		}

		private void PrintObj(List<ushort> args)
		{
			ushort addr = GetPropertyHeaderAddress(args[0]);
			var chars = GetZsciiChars((ushort)(addr+1));
			string s = DecodeZsciiChars(chars);
			_io.Print(s);
			Log.Write($"[{s}]");
		}

		private void PrintAddr(List<ushort> args)
		{
			List<byte> chars = GetZsciiChars(args[0]);
			string s = DecodeZsciiChars(chars);
			_io.Print(s);
			Log.Write($"[{s}]");
		}

		private void PrintPAddr(List<ushort> args)
		{
			List<byte> chars = GetZsciiChars(GetPackedAddress(args[0]));
			string s = DecodeZsciiChars(chars);
			_io.Print(s);
			Log.Write($"[{s}]");
		}

		private void ShowStatus(List<ushort> args)
		{
			_io.ShowStatus();
		}

		private void SplitWindow(List<ushort> args)
		{
			_io.SplitWindow(args[0]);
		}

		private void SetWindow(List<ushort> args)
		{
			_io.SetWindow(args[0]);
		}

		private void EraseWindow(List<ushort> args)
		{
			_io.EraseWindow(args[0]);
		}

		private void SetCursor(List<ushort> args)
		{
			_io.SetCursor(args[0], args[1], (ushort)(args.Count == 3 ? args[2] : 0));
		}

		private void SetTextStyle(List<ushort> args)
		{
			_io.SetTextStyle((TextStyle)args[0]);
		}

		private void SetFont(List<ushort> args)
		{
			// TODO

			byte dest = _memory[_stack.Peek().PC++];
			StoreWordInVariable(dest, 0);
		}

		private void SetColor(List<ushort> args)
		{
			_io.SetColor((ZColor)args[0], (ZColor)args[1]);
		}

		private void SoundEffect(List<ushort> args)
		{
			// TODO - the rest of the params

			_io.SoundEffect(args[0]);
		}

		private void BufferMode(List<ushort> args)
		{
			_io.BufferMode(args[0] == 1);
		}

		private void OutputStream(List<ushort> args)
		{
			// TODO
		}

		private void Read(List<ushort> args)
		{
			_readTextAddr = args[0];
			_readParseAddr = args[1];

			if(_terminateOnInput)
				_running = false;
			else
			{
				byte max = _memory[_readTextAddr];
				string input = _io.Read(max);
				FinishRead(input);
			}
		}

		public void FinishRead(string input)
		{
			if(input != null && _readTextAddr != 0 && _readParseAddr != 0)
			{
				int textMax = _memory[_readTextAddr];
				int wordMax = _memory[_readParseAddr];

				input = input.ToLower().Substring(0, Math.Min(input.Length, textMax));
				Log.Write($"[{input}]");

				int ix = 1;

				if(_version >= 5)
					_memory[_readTextAddr + ix++] = (byte)input.Length;

				for(int j = 0; j < input.Length; j++, ix++)
					_memory[_readTextAddr + ix] = (byte)input[j];

				if(_version < 5)
					_memory[_readTextAddr + ++ix] = 0;

				string[] tokenised = input.Split(' ');

				_memory[_readParseAddr + 1] = (byte)tokenised.Length;

				int len = (_version <= 3) ? 6 : 9;
				int last = 0;
				int max = Math.Min(tokenised.Length, wordMax);

				for(int i = 0; i < max; i++)
				{
					if(tokenised[i].Length > len)
						tokenised[i] = tokenised[i].Substring(0, len);

					ushort wordIndex = (ushort)(Array.IndexOf(_dictionaryWords, tokenised[i]));
					ushort addr = (ushort)(wordIndex == 0xffff ? 0 : _wordStart + wordIndex * _entryLength);
					StoreWord((ushort)(_readParseAddr + 2 + i*4), addr);
					_memory[_readParseAddr + 4 + i*4] = (byte)tokenised[i].Length;
					int index = input.IndexOf(tokenised[i], last, StringComparison.Ordinal);
					_memory[_readParseAddr + 5 + i*4] = (byte)(index + (_version < 5 ? 1 : 2));
					last = index + tokenised[i].Length;
				}

				if(_version >= 5)
				{
					byte dest = _memory[_stack.Peek().PC++];
					StoreByteInVariable(dest, 10);
				}

				_readTextAddr = 0;
				_readParseAddr = 0;
			}
		}

		private void ReadChar(List<ushort> args)
		{
			char key = _io.ReadChar();

			byte dest = _memory[_stack.Peek().PC++];
			StoreByteInVariable(dest, (byte)key);
		}

		private void InsertObj(List<ushort> args)
		{
			if(args[0] == 0 || args[1] == 0)
				return;

			Log.Write($"[{GetObjectName(args[0])}] [{GetObjectName(args[1])}] ");

			ushort obj1 = args[0];
			ushort obj2 = args[1];

			ushort obj1Addr = GetObjectAddress(args[0]);
			ushort obj2Addr = GetObjectAddress(args[1]);

			ushort parent1 = GetObjectNumber((ushort)(obj1Addr+ParentOffset));
			ushort sibling1 = GetObjectNumber((ushort)(obj1Addr+SiblingOffset));
			ushort child2 = GetObjectNumber((ushort)(obj2Addr+ChildOffset));

			ushort parent1Addr = GetObjectAddress(parent1);

			ushort parent1Child = GetObjectNumber((ushort)(parent1Addr+ChildOffset));
			ushort parent1ChildAddr = GetObjectAddress(parent1Child);
			ushort parent1ChildSibling = GetObjectNumber((ushort)(parent1ChildAddr+SiblingOffset));

			if(parent1 == obj2 && child2 == obj1)
				return;

			// if parent1's child is obj1 we need to assign the sibling
			if(parent1Child == obj1)
			{
				// set parent1's child to obj1's sibling
				SetObjectNumber((ushort)(parent1Addr+ChildOffset), sibling1);
			}
			else  // else if I'm not the child but there is a child, we need to link the broken sibling chain
			{
				ushort addr = parent1ChildAddr;
				ushort currentSibling = parent1ChildSibling;

				// while sibling of parent1's child has siblings
				while(currentSibling != 0)
				{
					// if obj1 is the sibling of the current object
					if(currentSibling == obj1)
					{
						// set the current object's sibling to the next sibling
						SetObjectNumber((ushort)(addr+SiblingOffset), sibling1);
						break;
					}

					addr = GetObjectAddress(currentSibling);
					currentSibling = GetObjectNumber((ushort)(addr+SiblingOffset));
				}
			}

			// set obj1's parent to obj2
			SetObjectNumber((ushort)(obj1Addr+ParentOffset), obj2);

			// set obj2's child to obj1
			SetObjectNumber((ushort)(obj2Addr+ChildOffset), obj1);

			// set obj1's sibling to obj2's child
			SetObjectNumber((ushort)(obj1Addr+SiblingOffset), child2);
		}

		private void RemoveObj(List<ushort> args)
		{
			if(args[0] == 0)
				return;

			Log.Write($"[{GetObjectName(args[0])}] ");
			ushort objAddr = GetObjectAddress(args[0]);
			ushort parent = GetObjectNumber((ushort)(objAddr+ParentOffset));
			ushort parentAddr = GetObjectAddress(parent);
			ushort parentChild = GetObjectNumber((ushort)(parentAddr+ChildOffset));
			ushort sibling = GetObjectNumber((ushort)(objAddr+SiblingOffset));

			// if object is the first child, set first child to the sibling
			if(parent == args[0])
				SetObjectNumber((ushort)(parentAddr+ChildOffset), sibling);
			else if(parentChild != 0)
			{
				ushort addr = GetObjectAddress(parentChild);
				ushort currentSibling = GetObjectNumber((ushort)(addr+SiblingOffset));

				// while sibling of parent1's child has siblings
				while(currentSibling != 0)
				{
					// if obj1 is the sibling of the current object
					if(currentSibling == args[0])
					{
						// set the current object's sibling to the next sibling
						SetObjectNumber((ushort)(addr+SiblingOffset), sibling);
						break;
					}

					addr = GetObjectAddress(currentSibling);
					currentSibling = GetObjectNumber((ushort)(addr+SiblingOffset));
				}
			}

			// set the object's parent to nothing
			SetObjectNumber((ushort)(objAddr+ParentOffset), 0);
		}

		private void GetProp(List<ushort> args)
		{
			Log.Write($"[{GetObjectName(args[0])}] ");

			byte dest = _memory[_stack.Peek().PC++];
			ushort val = 0;

			ushort addr = GetPropertyAddress(args[0], (byte)args[1]);
			if(addr > 0)
			{
				byte propInfo = _memory[addr++];
				byte len;

				if(_version > 3 && (propInfo & 0x80) == 0x80)
					len = (byte)(_memory[addr++] & 0x3f);
				else
					len = (byte)((propInfo >> (_version <= 3 ? 5 : 6)) + 1);

				for(int i = 0; i < len; i++)
					val |= (ushort)(_memory[addr+i] << (len-1-i)*8);
			}
			else
				val = GetWord((ushort)(_objectTable + (args[1]-1)*2));

			StoreWordInVariable(dest, val);
		}

		private void GetPropAddr(List<ushort> args)
		{
			Log.Write($"[{GetObjectName(args[0])}] ");

			byte dest = _memory[_stack.Peek().PC++];
			ushort addr = GetPropertyAddress(args[0], (byte)args[1]);

			if(addr > 0)
			{
				byte propInfo = _memory[addr+1];

				if(_version > 3 && (propInfo & 0x80) == 0x80)
					addr+=2;
				else
					addr+=1;
			}

			StoreWordInVariable(dest, addr);
		}

		private void GetNextProp(List<ushort> args)
		{
			Log.Write($"[{GetObjectName(args[0])}] ");

			bool next = false;

			byte dest = _memory[_stack.Peek().PC++];
			if(args[1] == 0)
				next = true;

			ushort propHeaderAddr = GetPropertyHeaderAddress(args[0]);
			byte size = _memory[propHeaderAddr];
			propHeaderAddr += (ushort)(size * 2+1);

			while(_memory[propHeaderAddr] != 0x00)
			{
				byte propInfo = _memory[propHeaderAddr];
				byte len;
				if(_version > 3 && (propInfo & 0x80) == 0x80)
				{
					len = (byte)(_memory[++propHeaderAddr] & 0x3f);
					if(len == 0)
						len = 64;
				}
				else
					len = (byte)((propInfo >> (_version <= 3 ? 5 : 6)) + 1);
				byte propNum = (byte)(propInfo & (_version <= 3 ? 0x1f : 0x3f));

				if(next)
				{
					StoreByteInVariable(dest, propNum);
					return;
				}
	
				if(propNum == args[1])
					next = true;

				propHeaderAddr += (ushort)(len+1);
			}

			StoreByteInVariable(dest, 0);	
		}

		private void GetPropLen(List<ushort> args)
		{
			byte dest = _memory[_stack.Peek().PC++];
			byte propInfo = _memory[args[0]-1];
			byte len;
			if(_version > 3 && (propInfo & 0x80) == 0x80)
			{
				len = (byte)(_memory[args[0]-1] & 0x3f);
				if(len == 0)
					len = 64;
			}
			else
				len = (byte)((propInfo >> (_version <= 3 ? 5 : 6)) + 1);

			StoreByteInVariable(dest, len);
		}

		private void PutProp(List<ushort> args)
		{
			Log.Write($"[{GetObjectName(args[0])}] ");

			ushort prop = GetPropertyHeaderAddress(args[0]);
			byte size = _memory[prop];
			prop += (ushort)(size * 2+1);

			while(_memory[prop] != 0x00)
			{
				byte propInfo = _memory[prop++];
				byte len;
				if(_version > 3 && (propInfo & 0x80) == 0x80)
				{
					len = (byte)(_memory[prop++] & 0x3f);
					if(len == 0)
						len = 64;
				}
				else
					len = (byte)((propInfo >> (_version <= 3 ? 5 : 6)) + 1);
				byte propNum = (byte)(propInfo & (_version <= 3 ? 0x1f : 0x3f));
				if(propNum == args[1])
				{
					if(len == 1)
						_memory[prop+1] = (byte)args[2];
					else
						StoreWord(prop, args[2]);

					break;
				}
				prop += len;
			}
		}

		private void TestAttribute(List<ushort> args)
		{
			Log.Write($"[{GetObjectName(args[0])}] ");
			PrintObjectInfo(args[0], false);

			ushort objectAddr = GetObjectAddress(args[0]);
			ulong attributes;
			ulong flag;

			if(_version <= 3)
			{
				attributes = GetUint(objectAddr);
				flag = 0x80000000 >> args[1];
			}
			else
			{
				attributes = (ulong)GetUint(objectAddr) << 16 | GetWord((uint)(objectAddr+4));
				flag = (ulong)(0x800000000000 >> args[1]);
			}

			bool branch = (flag & attributes) == flag;
			Jump(branch);
		}

		private void SetAttribute(List<ushort> args)
		{
			if(args[0] == 0)
				return;

			Log.Write($"[{GetObjectName(args[0])}] ");

			ushort objectAddr = GetObjectAddress(args[0]);
			ulong attributes;
			ulong flag;

			if(_version <= 3)
			{
				attributes = GetUint(objectAddr);
				flag = 0x80000000 >> args[1];
				attributes |= flag;
				StoreUint(objectAddr, (uint)attributes);
			}
			else
			{
				attributes = (ulong)GetUint(objectAddr) << 16 | GetWord((uint)(objectAddr+4));
				flag = (ulong)(0x800000000000 >> args[1]);
				attributes |= flag;
				StoreUint(objectAddr, (uint)(attributes >> 16));
				StoreWord((ushort)(objectAddr+4), (ushort)attributes);
			}
		}

		private void ClearAttribute(List<ushort> args)
		{
			Log.Write($"[{GetObjectName(args[0])}] ");

			ushort objectAddr = GetObjectAddress(args[0]);
			ulong attributes;
			ulong flag;

			if(_version <= 3)
			{
				attributes = GetUint(objectAddr);
				flag = 0x80000000 >> args[1];
				attributes &= ~flag;
				StoreUint(objectAddr, (uint)attributes);
			}
			else
			{
				attributes = (ulong)GetUint(objectAddr) << 16 | GetWord((uint)(objectAddr+4));
				flag = (ulong)(0x800000000000 >> args[1]);
				attributes &= ~flag;
				StoreUint(objectAddr, (uint)attributes >> 16);
				StoreWord((ushort)(objectAddr+4), (ushort)attributes);
			}
		}

		private void GetParent(List<ushort> args)
		{
			Log.Write($"[{GetObjectName(args[0])}] ");

			ushort addr = GetObjectAddress(args[0]);
			ushort parent = GetObjectNumber((ushort)(addr + ParentOffset));

			Log.Write($"[{GetObjectName(parent)}] ");

			byte dest = _memory[_stack.Peek().PC++];

			if(_version <= 3)
				StoreByteInVariable(dest, (byte)parent);
			else
				StoreWordInVariable(dest, parent);
		}

		private void GetChild(List<ushort> args)
		{
			Log.Write($"[{GetObjectName(args[0])}] ");

			ushort addr = GetObjectAddress(args[0]);
			ushort child = GetObjectNumber((ushort)(addr + ChildOffset));

			Log.Write($"[{GetObjectName(child)}] ");

			byte dest = _memory[_stack.Peek().PC++];

			if(_version <= 3)
				StoreByteInVariable(dest, (byte)child);
			else
				StoreWordInVariable(dest, child);

			Jump(child != 0);
		}

		private void GetSibling(List<ushort> args)
		{
			Log.Write($"[{GetObjectName(args[0])}] ");

			ushort addr = GetObjectAddress(args[0]);
			ushort sibling = GetObjectNumber((ushort)(addr + SiblingOffset));

			Log.Write($"[{GetObjectName(sibling)}] ");

			byte dest = _memory[_stack.Peek().PC++];

			if(_version <= 3)
				StoreByteInVariable(dest, (byte)sibling);
			else
				StoreWordInVariable(dest, sibling);

			Jump(sibling != 0);
		}

		private void Load(List<ushort> args)
		{
			byte dest = _memory[_stack.Peek().PC++];
			ushort val = GetVariable((byte)args[0], false);
			StoreByteInVariable(dest, (byte)val);
		}

		private void Store(List<ushort> args)
		{
			StoreWordInVariable((byte)args[0], args[1], false);
		}

		private void StoreB(List<ushort> args)
		{
			ushort addr = (ushort)(args[0] + args[1]);
			_memory[addr] = (byte)args[2];
		}

		private void StoreW(List<ushort> args)
		{
			ushort addr = (ushort)(args[0] + 2 * args[1]);
			StoreWord(addr, args[2]);
		}

		private void LoadB(List<ushort> args)
		{
			ushort addr = (ushort)(args[0] + args[1]);
			byte b = _memory[addr];
			byte dest = _memory[_stack.Peek().PC++];
			StoreByteInVariable(dest, b);
		}

		private void LoadW(List<ushort> args)
		{
			ushort addr = (ushort)(args[0] + 2 * args[1]);
			ushort word = GetWord(addr);
			byte dest = _memory[_stack.Peek().PC++];
			StoreWordInVariable(dest, word);
		}

		private void Jump(List<ushort> args)
		{
			_stack.Peek().PC = (uint)(_stack.Peek().PC + (short)(args[0] - 2));
			Log.Write($"-> {_stack.Peek().PC:X5}");
		}

		private void Je(List<ushort> args)
		{
			bool equal = false;
			for(int i = 1; i < args.Count; i++)
			{
				if(args[0] == args[i])
				{
					equal = true;
					break;
				}
			}

			Jump(equal);
		}

		private void Jz(List<ushort> args)
		{
			Jump(args[0] == 0);
		}

		private void Jl(List<ushort> args)
		{
			Jump((short)args[0] < (short)args[1]);
		}

		private void Jg(List<ushort> args)
		{
			Jump((short)args[0] > (short)args[1]);
		}

		private void Jin(List<ushort> args)
		{
			Log.Write($"C[{GetObjectName(args[0])}] P[{GetObjectName(args[1])}] ");

			ushort addr = GetObjectAddress(args[0]);
			ushort parent = GetObjectNumber((ushort)(addr+ParentOffset));
			Jump(parent == args[1]);
		}

		private void Jump(bool flag)
		{
			bool branch;

			byte offset = _memory[_stack.Peek().PC++];
			short newOffset;

			if((offset & 0x80) == 0x80)
			{
				Log.Write(" [TRUE] ");
				branch = true;
			}
			else
			{
				Log.Write(" [FALSE] ");
				branch = false;
			}

			bool executeBranch = branch && flag || !branch && !flag;

			if((offset & 0x40) == 0x40)
			{
				offset = (byte)(offset & 0x3f);

				if(offset == 0 && executeBranch)
				{
					Log.Write(" RFALSE ");
					RFalse(null);
					return;
				}

				if(offset == 1 && executeBranch)
				{
					Log.Write(" RTRUE ");
					RTrue(null);
					return;
				}
				
				newOffset = (short)(offset - 2);
			}
			else
			{
				byte offset2 = _memory[_stack.Peek().PC++];
				ushort final = (ushort)((offset & 0x3f) << 8 | offset2);

				// this is a 14-bit number, so set the sign bit properly because we can jump backwards
				if((final & 0x2000) == 0x2000)
					final |= 0xc000;

				newOffset = (short)(final - 2);
			}

			if(executeBranch)
				_stack.Peek().PC += (uint)newOffset;

			Log.Write($"-> {_stack.Peek().PC:X5}");
		}

		private void Add(List<ushort> args)
		{
			short val = (short)(args[0] + args[1]);
			byte dest = _memory[_stack.Peek().PC++];
			StoreWordInVariable(dest, (ushort)val);
		}

		private void Sub(List<ushort> args)
		{
			short val = (short)(args[0] - args[1]);
			byte dest = _memory[_stack.Peek().PC++];
			StoreWordInVariable(dest, (ushort)val);
		}

		private void Mul(List<ushort> args)
		{
			short val = (short)(args[0] * args[1]);
			byte dest = _memory[_stack.Peek().PC++];
			StoreWordInVariable(dest, (ushort)val);
		}

		private void Div(List<ushort> args)
		{
			byte dest = _memory[_stack.Peek().PC++];

			if(args[1] == 0)
				return;

			short val = (short)((short)args[0] / (short)args[1]);
			StoreWordInVariable(dest, (ushort)val);
		}

		private void Mod(List<ushort> args)
		{
			short val = (short)((short)args[0] % (short)args[1]);
			byte dest = _memory[_stack.Peek().PC++];
			StoreWordInVariable(dest, (ushort)val);
		}

		private void Inc(List<ushort> args)
		{
			short val = (short)(GetVariable((byte)args[0])+1);
			StoreWordInVariable((byte)args[0], (ushort)val);
		}

		private void Dec(List<ushort> args)
		{
			short val = (short)(GetVariable((byte)args[0])-1);
			StoreWordInVariable((byte)args[0], (ushort)val);
		}

		private void ArtShift(List<ushort> args)
		{
			// keep the sign bit, so make it a short
			short val = (short)args[0];
			if((short)args[1] > 0)
				val <<= args[1];
			else if((short)args[1] < 0)
				val >>= -args[1];

			byte dest = _memory[_stack.Peek().PC++];
			StoreWordInVariable(dest, (ushort)val);
		}

		private void LogShift(List<ushort> args)
		{
			// kill the sign bit, so make it a ushort
			ushort val = args[0];
			if((short)args[1] > 0)
				val <<= args[1];
			else if((short)args[1] < 0)
				val >>= -args[1];

			byte dest = _memory[_stack.Peek().PC++];
			StoreWordInVariable(dest, (ushort)val);
		}

		private void Random(List<ushort> args)
		{
			ushort val = 0;

			if((short)args[0] <= 0)
				_random = new Random(-args[0]);
			else
				val = (ushort)(_random.Next(0, args[0])+1);

			byte dest = _memory[_stack.Peek().PC++];
			StoreWordInVariable(dest, val);
		}

		private void Or(List<ushort> args)
		{
			ushort or = (ushort)(args[0] | args[1]);
			byte dest = _memory[_stack.Peek().PC++];
			StoreWordInVariable(dest, or);
		}

		private void And(List<ushort> args)
		{
			ushort and = (ushort)(args[0] & args[1]);
			byte dest = _memory[_stack.Peek().PC++];
			StoreWordInVariable(dest, and);
		}

		private void Not(List<ushort> args)
		{
			byte dest = _memory[_stack.Peek().PC++];
			StoreWordInVariable(dest, (ushort)~args[0]);
		}

		private void Test(List<ushort> args)
		{
			Jump((args[0] & args[1]) == args[1]);
		}

		private void DecCheck(List<ushort> args)
		{
			short val = (short)GetVariable((byte)args[0]);
			val--;
			StoreWordInVariable((byte)args[0], (ushort)val);
			Jump(val < (short)args[1]);
		}

		private void IncCheck(List<ushort> args)
		{
			short val = (short)GetVariable((byte)args[0]);
			val++;
			StoreWordInVariable((byte)args[0], (ushort)val);
			Jump(val > (short)args[1]);
		}

		private void Call(List<ushort> args)
		{
			Call(args, true);
		}

		private void Call(List<ushort> args, bool storeResult)
		{
			if(args[0] == 0)
			{
				if(storeResult)
				{
					byte dest = _memory[_stack.Peek().PC++];
					StoreWordInVariable(dest, 0);
				}
				return;
			}

			uint pc = GetPackedAddress(args[0]);
			Log.Write($"New PC: {pc:X5}");

			ZStackFrame zsf = new ZStackFrame { PC = pc, StoreResult = storeResult };
			_stack.Push(zsf);

			byte count = _memory[_stack.Peek().PC++];

			if(_version <= 4)
			{
				for(int i = 0; i < count; i++)
				{
					zsf.Variables[i] = GetWord(_stack.Peek().PC);
					_stack.Peek().PC += 2;
				}
			}

			for(int i = 0; i < args.Count-1; i++)
				zsf.Variables[i] = args[i+1];

			zsf.ArgumentCount = args.Count-1;
		}

		private void Call1N(List<ushort> args)
		{
			Call(args, false);
		}

		private void Call1S(List<ushort> args)
		{
			Call(args, true);
		}

		private void Call2S(List<ushort> args)
		{
			Call(args, true);
		}

		private void Call2N(List<ushort> args)
		{
			Call(args, false);
		}

		private void CallVN(List<ushort> args)
		{
			Call(args, false);
		}

		private void CallVN2(List<ushort> args)
		{
			Call(args, false);
		}

		private void CallVS2(List<ushort> args)
		{
			Call(args, true);
		}

		private void Ret(List<ushort> args)
		{
			ZStackFrame sf = _stack.Pop();
			if(sf.StoreResult)
			{
				byte dest = _memory[_stack.Peek().PC++];
				StoreWordInVariable(dest, args[0]);
			}
		}

		private void RetPopped(List<ushort> args)
		{
			ushort val = _stack.Peek().RoutineStack.Pop();
			ZStackFrame sf = _stack.Pop();
			if(sf.StoreResult)
			{
				byte dest = _memory[_stack.Peek().PC++];
				StoreWordInVariable(dest, val);
			}
		}

		private void Pop(List<ushort> args)
		{
			if(_stack.Peek().RoutineStack.Count > 0)
				_stack.Peek().RoutineStack.Pop();
			else
				_stack.Pop();
		}

		private void RTrue(List<ushort> args)
		{
			ZStackFrame sf = _stack.Pop();
			if(sf.StoreResult)
			{
				byte dest = _memory[_stack.Peek().PC++];
				StoreWordInVariable(dest, 1);
			}
		}

		private void RFalse(List<ushort> args)
		{
			ZStackFrame sf = _stack.Pop();
			if(sf.StoreResult)
			{
				byte dest = _memory[_stack.Peek().PC++];
				StoreWordInVariable(dest, 0);
			}
		}

		private void CheckArgCount(List<ushort> args)
		{
			Jump(args[0] <= _stack.Peek().ArgumentCount);
		}

		private void Push(List<ushort> args)
		{
			_stack.Peek().RoutineStack.Push(args[0]);
		}

		private void Pull(List<ushort> args)
		{
			ushort val = _stack.Peek().RoutineStack.Pop();
			StoreWordInVariable((byte)args[0], val, false);
		}

		private List<ushort> GetOperands(byte opcode)
		{
			List<ushort> args = new List<ushort>();
			ushort arg;

			// Variable
			if((opcode & 0xc0) == 0xc0)
			{
				byte types = _memory[_stack.Peek().PC++];
				byte types2 = 0;

				if(opcode == 0xec || opcode == 0xfa)
					types2 = _memory[_stack.Peek().PC++];

				GetVariableOperands(types, args);
				if(opcode == 0xec || opcode == 0xfa)
					GetVariableOperands(types2, args);
			}
			// Short
			else if((opcode & 0x80) == 0x80)
			{
				byte type = (byte)(opcode >> 4 & 0x03);
				arg = GetOperand((OperandType)type);
				args.Add(arg);	
			}
			// Long
			else
			{
				arg = GetOperand((opcode & 0x40) == 0x40 ? OperandType.Variable : OperandType.SmallConstant);
				args.Add(arg);

				arg = GetOperand((opcode & 0x20) == 0x20 ? OperandType.Variable : OperandType.SmallConstant);
				args.Add(arg);
			}

			return args;
		}

		private void GetVariableOperands(byte types, List<ushort> args)
		{
			for(int i = 6; i >= 0; i -= 2)
			{
				byte type = (byte)((types >> i) & 0x03);

				// omitted
				if(type == 0x03)
					break;

				ushort arg = GetOperand((OperandType)type);
				args.Add(arg);
			}
		}

		private ushort GetOperand(OperandType type)
		{
			ushort arg = 0;

			switch(type)
			{
				case OperandType.LargeConstant:
					arg = GetWord(_stack.Peek().PC);
					_stack.Peek().PC+=2;
					Log.Write($"#{arg:X4}, ");
					break;
				case OperandType.SmallConstant:
					arg = _memory[_stack.Peek().PC++];
					Log.Write($"#{arg:X2}, ");
					break;
				case OperandType.Variable:
					byte b = _memory[_stack.Peek().PC++];
					arg = GetVariable(b);
					break;
			}

			return arg;
		}

		private void StoreByteInVariable(byte dest, byte value)
		{
			if(dest == 0)
			{
				Log.Write($"-> SP ({value:X4}), ");
				_stack.Peek().RoutineStack.Push(value);
			}
			else if(dest < 0x10)
			{
				Log.Write($"-> L{dest-1:X2} ({value:X4}), ");
				_stack.Peek().Variables[dest-1] = value;
			}
			else
			{
				// this still gets written as a word...write the byte to addr+1
				Log.Write($"-> G{dest - 0x10:X2} ({value:X4}), ");
				_memory[_globals + 2 * (dest - 0x10)] = 0;
				_memory[_globals + 2 * (dest - 0x10)+1] = value;
			}
		}

		private void StoreWordInVariable(byte dest, ushort value, bool push=true)
		{
			if(dest == 0)
			{
				Log.Write($"-> SP ({value:X4}), ");
				if(!push)
					_stack.Peek().RoutineStack.Pop();
				_stack.Peek().RoutineStack.Push(value);
			}
			else if(dest < 0x10)
			{
				Log.Write($"-> L{dest-1:X2} ({value:X4}), ");
				_stack.Peek().Variables[dest-1] = value;
			}
			else
			{
				Log.Write($"-> G{dest - 0x10:X2} ({value:X4}), ");
				StoreWord((ushort)(_globals + 2 * (dest - 0x10)), value);
			}
		}

		private ushort GetVariable(byte variable, bool pop=true)
		{
			ushort val;

			if(variable == 0)
			{
				if(pop)
					val = _stack.Peek().RoutineStack.Pop();
				else
					val = _stack.Peek().RoutineStack.Peek();
				Log.Write($"SP ({val:X4}), ");
			}
			else if(variable < 0x10)
			{
				val = _stack.Peek().Variables[variable-1];
				Log.Write($"L{variable-1:X2} ({val:X4}), ");
			}
			else
			{
				val = GetWord((ushort)(_globals + 2 * (variable - 0x10)));
				Log.Write($"G{variable - 0x10:X2} ({val:X4}), ");
			}
			return val;
		}

		private ushort GetWord(uint address)
		{
			return (ushort)(_memory[address] << 8 | _memory[address+1]);
		}

		private void StoreWord(ushort address, ushort value)
		{
			_memory[address+0] = (byte)(value >> 8);
			_memory[address+1] = (byte)value;
		}

		private uint GetUint(uint address)
		{
			return (uint)(_memory[address] << 24 | _memory[address+1] << 16 | _memory[address+2] << 8 | _memory[address+3]);
		}

		private void StoreUint(uint address, uint val)
		{
			_memory[address+0] = (byte)(val >> 24);
			_memory[address+1] = (byte)(val >> 16);
			_memory[address+2] = (byte)(val >>  8);
			_memory[address+3] = (byte)(val >>  0);
		}

		private uint GetPackedAddress(ushort address)
		{
			if(_version <= 3)
				return (uint)(address * 2);
			if(_version <= 5)
				return (uint)(address * 4);

			return 0;
		}

		private ushort GetObjectAddress(ushort obj)
		{
			ushort objectAddr = (ushort)(_objectTable + PropertyDefaultTableSize + (obj-1) * ObjectSize);
			return objectAddr;
		}

		private ushort GetObjectNumber(ushort objectAddr)
		{
			if(_version <= 3)
				return _memory[objectAddr];
			return GetWord(objectAddr);
		}

		private void SetObjectNumber(ushort objectAddr, ushort obj)
		{
			if(_version <= 3)
				_memory[objectAddr] = (byte)obj;
			else
				StoreWord(objectAddr, obj);
		}

		private ushort GetPropertyHeaderAddress(ushort obj)
		{
			ushort objectAddr = GetObjectAddress(obj);
			ushort propAddr = (ushort)(objectAddr + PropertyOffset);
			ushort prop = GetWord(propAddr);
			return prop;
		}

		private ushort GetPropertyAddress(ushort obj, byte prop)
		{
			ushort propHeaderAddr = GetPropertyHeaderAddress(obj);

			// skip past text
			byte size = _memory[propHeaderAddr];
			propHeaderAddr += (ushort)(size * 2+1);

			while(_memory[propHeaderAddr] != 0x00)
			{
				byte propInfo = _memory[propHeaderAddr];
				byte propNum = (byte)(propInfo & (_version <= 3 ? 0x1f : 0x3f));

				if(propNum == prop)
					return propHeaderAddr;

				byte len;

				if(_version > 3 && (propInfo & 0x80) == 0x80)
				{
					len = (byte)(_memory[++propHeaderAddr] & 0x3f);
					if(len == 0)
						len = 64;
				}
				else
					len = (byte)((propInfo >> (_version <= 3 ? 5 : 6)) + 1);

				propHeaderAddr += (ushort)(len+1);
			}

			return 0;
		}

		private string GetObjectName(ushort obj)
		{
			string s = string.Empty;

			if(obj != 0)
			{
				ushort addr = GetPropertyHeaderAddress(obj);
				if(_memory[addr] != 0)
				{
					List<byte> chars = GetZsciiChars((uint)(addr+1));
					s = DecodeZsciiChars(chars);
				}
			}

			return s;
		}

		private void PrintObjects()
		{
			ushort lowest = 0xffff;
	
			for(ushort i = 1; i < 255 && (_objectTable + i*ObjectSize) < lowest; i++)
			{
				ushort addr = PrintObjectInfo(i, true);
				if(addr < lowest)
					lowest = addr;
			}
		}

		private void PrintObjectTree()
		{
			for(ushort i = 1; i < 255; i++)
			{
				ushort addr = GetObjectAddress(i);
				ushort parent = GetObjectNumber((ushort)(addr+ParentOffset));
				if(parent == 0)
					PrintTree(i, 0);
			}
		}

		private void PrintTree(ushort obj, int depth)
		{
			while(obj != 0)
			{
				for(int i = 0; i < depth; i++)
					Log.Write(" . ");

				PrintObjectInfo(obj, false);
				ushort addr = GetObjectAddress(obj);
				ushort child = GetObjectNumber((ushort)(addr+ChildOffset));
				obj = GetObjectNumber((ushort)(addr+SiblingOffset));
				if(child != 0)
					PrintTree(child, depth + 1);
			}
		}

		private ushort PrintObjectInfo(ushort obj, bool properties)
		{
			if(obj == 0)
				return 0;

			ushort startAddr = GetObjectAddress(obj);

			ulong attributes = (ulong)GetUint(startAddr) << 16 | GetWord((uint)(startAddr+4));
			ushort parent = GetObjectNumber((ushort)(startAddr+ParentOffset));
			ushort sibling = GetObjectNumber((ushort)(startAddr+SiblingOffset));
			ushort child = GetObjectNumber((ushort)(startAddr+ChildOffset));
			ushort propAddr = GetWord((uint)(startAddr+PropertyOffset));

			Log.Write($"{obj} ({obj:X2}) at {propAddr:X5}: ");

			byte size = _memory[propAddr++];
			string s = string.Empty;
			if(size > 0)
			{
				var name = GetZsciiChars(propAddr);
				s = DecodeZsciiChars(name);
			}

			propAddr += (ushort)(size*2);

			Log.WriteLine($"[{s}] A:{attributes:X12} P:{parent}({parent:X2}) S:{sibling}({sibling:X2}) C:{child}({child:X2})");

			if(properties)
			{
				string ss = string.Empty;
				for(int i = 47; i >= 0; i--)
				{
					if(((attributes >> i) & 0x01) == 0x01)
					{
						ss += 47-i + ", ";
					}
				}

				Log.WriteLine("Attributes: " + ss);

				while(_memory[propAddr] != 0x00)
				{
					byte propInfo = _memory[propAddr];
					byte len;
					if(_version > 3 && (propInfo & 0x80) == 0x80)
						len = (byte)(_memory[propAddr+1] & 0x3f);
					else
						len = (byte)((propInfo >> (_version <= 3 ? 5 : 6)) + 1);
					byte propNum = (byte)(propInfo & (_version <= 3 ? 0x1f : 0x3f));

					Log.Write($"  P:{propNum:X2} at {propAddr:X4}: ");
					for(int i = 0; i < len; i++)
						Log.Write($"{_memory[propAddr++]:X2} ");
					Log.WriteLine("");
					propAddr++;
				}
			}

			return propAddr;
		}

		private void ParseDictionary()
		{
			ushort address = _dictionary;

			byte len = _memory[address++];
			address += len;

			_entryLength = _memory[address++];
			ushort numEntries = GetWord(address);	
			address+=2;

			_wordStart = address;

			_dictionaryWords = new string[numEntries];

			for(int i = 0; i < numEntries; i++)
			{
				ushort wordAddress = (ushort)(address + i*_entryLength);
				var chars = GetZsciiChar(wordAddress);
				chars.AddRange(GetZsciiChar((uint)(wordAddress+2)));
				if(_entryLength == 9)
					chars.AddRange(GetZsciiChar((uint)(wordAddress+4)));
				string s = DecodeZsciiChars(chars);
				_dictionaryWords[i] = s;
			}
		}

		private void PrintDictionary()
		{
			ushort address = _dictionary;

			byte len = _memory[address++];

			Log.Write("Separators: [" );
			for(int i = 0; i < len; i++)
				Log.Write(Convert.ToChar(_memory[address++]).ToString());
			Log.WriteLine("]");

			byte entryLength = _memory[address++];
			ushort numEntries = GetWord(address);	
			address+=2;
			Log.WriteLine($"Entry Length: {entryLength}, Num Entries: {numEntries}");

			for(int i = 0; i < numEntries; i++)
			{
				ushort wordAddress = (ushort)(address + i*entryLength);
				var chars = GetZsciiChar(wordAddress);
				chars.AddRange(GetZsciiChar((uint)(wordAddress+2)));
				if(_entryLength == 9)
					chars.AddRange(GetZsciiChar((uint)(wordAddress+4)));
				string s = DecodeZsciiChars(chars);
				Log.WriteLine($"{i+1} ({wordAddress:X4}): {s}");
			}

			Log.Flush();
		}
	}
}
