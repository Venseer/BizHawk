﻿using BizHawk.Common;

namespace BizHawk.Emulation.Cores.Nintendo.NES
{
	public sealed class Mapper208 : MMC3Board_Base
	{
		private ByteBuffer exRegs = new ByteBuffer(6);

		private readonly byte[] lut = {
			0x59, 0x59, 0x59, 0x59, 0x59, 0x59, 0x59, 0x59, 0x59, 0x49, 0x19, 0x09, 0x59, 0x49, 0x19, 0x09,
			0x59, 0x59, 0x59, 0x59, 0x59, 0x59, 0x59, 0x59, 0x51, 0x41, 0x11, 0x01, 0x51, 0x41, 0x11, 0x01,
			0x59, 0x59, 0x59, 0x59, 0x59, 0x59, 0x59, 0x59, 0x59, 0x49, 0x19, 0x09, 0x59, 0x49, 0x19, 0x09,
			0x59, 0x59, 0x59, 0x59, 0x59, 0x59, 0x59, 0x59, 0x51, 0x41, 0x11, 0x01, 0x51, 0x41, 0x11, 0x01,
			0x00, 0x10, 0x40, 0x50, 0x00, 0x10, 0x40, 0x50, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
			0x08, 0x18, 0x48, 0x58, 0x08, 0x18, 0x48, 0x58, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
			0x00, 0x10, 0x40, 0x50, 0x00, 0x10, 0x40, 0x50, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
			0x08, 0x18, 0x48, 0x58, 0x08, 0x18, 0x48, 0x58, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
			0x59, 0x59, 0x59, 0x59, 0x59, 0x59, 0x59, 0x59, 0x58, 0x48, 0x18, 0x08, 0x58, 0x48, 0x18, 0x08,
			0x59, 0x59, 0x59, 0x59, 0x59, 0x59, 0x59, 0x59, 0x50, 0x40, 0x10, 0x00, 0x50, 0x40, 0x10, 0x00,
			0x59, 0x59, 0x59, 0x59, 0x59, 0x59, 0x59, 0x59, 0x58, 0x48, 0x18, 0x08, 0x58, 0x48, 0x18, 0x08,
			0x59, 0x59, 0x59, 0x59, 0x59, 0x59, 0x59, 0x59, 0x50, 0x40, 0x10, 0x00, 0x50, 0x40, 0x10, 0x00,
			0x01, 0x11, 0x41, 0x51, 0x01, 0x11, 0x41, 0x51, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
			0x09, 0x19, 0x49, 0x59, 0x09, 0x19, 0x49, 0x59, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
			0x01, 0x11, 0x41, 0x51, 0x01, 0x11, 0x41, 0x51, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
			0x09, 0x19, 0x49, 0x59, 0x09, 0x19, 0x49, 0x59, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
		};

		public override bool Configure(NES.EDetectionOrigin origin)
		{
			switch (Cart.board_type)
			{
				case "MAPPER208":
					break;
				default:
					return false;
			}

			BaseSetup();
			return true;
		}

		public override void Dispose()
		{
			exRegs.Dispose();
			base.Dispose();
		}

		public override void SyncState(Serializer ser)
		{
			base.SyncState(ser);
			ser.Sync("expregs", ref exRegs);
		}

		public override byte ReadPRG(int addr)
		{
			return ROM[(exRegs[5] << 15) + addr];
		}

		public override byte ReadEXP(int addr)
		{
			if (addr >= 0x1800) // 0x5800-0x5FFF
			{
				return exRegs[addr & 3];
			}

			return base.ReadEXP(addr);
		}

		public override void WriteEXP(int addr, byte value)
		{
			if (addr >= 0x800 && addr < 0x1000) // 0x4800-0x4FFF
			{
				WriteReg(addr, value);
			}

			// Copy protection crap
			else if (addr >= 0x1000) // 0x5000-0x5FFF
			{
				if (addr <= 0x17FF) // 0x5000 - 0x57FF
				{
					exRegs[4] = value;
				}
				else // 0x5800-0x5FFF
				{
					exRegs[addr & 3] = (byte)(value ^ lut[exRegs[4]]);
				}
			}

			else
			{
				base.WriteEXP(addr, value);
			}
		}

		public override void WriteWRAM(int addr, byte value)
		{
			if (addr >= 0x800 && addr < 0x1000) // 0x6800 - 0x6FFF
			{
				WriteReg(addr, value);
			}
			else
			{
				base.WriteWRAM(addr, value);
			}
		}

		private void WriteReg(int addr, byte value)
		{
			exRegs[5] = (byte)((value & 1) | ((value >> 3) & 2));
		}
	}
}
