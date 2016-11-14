﻿using System;

namespace BizHawk.Emulation.Cores.Intellivision
{
	public sealed class PSG
	{
		public ushort[] Register = new ushort[16];

		public int TotalExecutedCycles;
		public int PendingCycles;

		public Func<ushort, ushort> ReadMemory;
		public Func<ushort, ushort, bool> WriteMemory;

		public ushort? ReadPSG(ushort addr)
		{
			if (addr >= 0x01F0 && addr <= 0x01FF)
			{
				return Register[addr - 0x01F0];
			}
			return null;
		}

		public bool WritePSG(ushort addr, ushort value)
		{
			if (addr >= 0x01F0 && addr <= 0x01FF)
			{
				Register[addr - 0x01F0] = value;
				return true;
			}
			return false;
		}
	}
}
