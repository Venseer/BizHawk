﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BizHawk.Emulation.Cores.Computers.SinclairSpectrum
{
    public partial class ZX128 : SpectrumBase
    {
        /* 128k paging controlled by writes to port 0x7ffd
         * 
         * 
         
            #7FFD (32765) - decoded as A15=0, A1=0 and /IORQ=0. Bits 0..5 are latched. Bits 0..2 select RAM bank in secton D. Bit 3 selects RAM bank to dispay screen (0 - RAM5, 1 - RAM7). Bit 4 selects ROM bank (0 - ROM0, 1 - ROM1). Bit 5, when set locks future writing to #7FFD port until reset. Reading #7FFD port is the same as writing #FF into it.
            #BFFD (49149) - write data byte into AY-3-8912 chip.
            #FFFD (65533) - select AY-3-8912 addres (D4..D7 ignored) and reading data byte.

         *  0xffff +--------+--------+--------+--------+--------+--------+--------+--------+
                   | Bank 0 | Bank 1 | Bank 2 | Bank 3 | Bank 4 | Bank 5 | Bank 6 | Bank 7 |
                   |        |        |(also at|        |        |(also at|        |        |
                   |        |        | 0x8000)|        |        | 0x4000)|        |        |
                   |        |        |        |        |        | screen |        | screen |
            0xc000 +--------+--------+--------+--------+--------+--------+--------+--------+
                   | Bank 2 |        Any one of these pages may be switched in.
                   |        |
                   |        |
                   |        |
            0x8000 +--------+
                   | Bank 5 |
                   |        |
                   |        |
                   | screen |
            0x4000 +--------+--------+
                   | ROM 0  | ROM 1  | Either ROM may be switched in.
                   |        |        |
                   |        |        |
                   |        |        |
            0x0000 +--------+--------+
        */

        /// <summary>
        /// Simulates reading from the bus (no contention)
        /// Paging should be handled here
        /// </summary>
        /// <param name="addr"></param>
        /// <returns></returns>
        public override byte ReadBus(ushort addr)
        {
            int divisor = addr / 0x4000;
            byte result = 0xff;

            switch (divisor)
            {
                // ROM 0x000
                case 0:
                    TestForTapeTraps(addr % 0x4000);

                    if (ROMPaged == 0)
                        result = ROM0[addr % 0x4000];
                    else
                        result = ROM1[addr % 0x4000];
                    break;

                // RAM 0x4000 (RAM5 - Bank5)
                case 1:
                    result = RAM5[addr % 0x4000];
                    break;

                // RAM 0x8000 (RAM2 - Bank2)
                case 2:
                    result = RAM2[addr % 0x4000];
                    break;

                // RAM 0xc000 (any ram bank 0 - 7 may be paged in - default bank0)
                case 3:
                    switch (RAMPaged)
                    {
                        case 0:
                            result = RAM0[addr % 0x4000];
                            break;
                        case 1:
                            result = RAM1[addr % 0x4000];
                            break;
                        case 2:
                            result = RAM2[addr % 0x4000];
                            break;
                        case 3:
                            result = RAM3[addr % 0x4000];
                            break;
                        case 4:
                            result = RAM4[addr % 0x4000];
                            break;
                        case 5:
                            result = RAM5[addr % 0x4000];
                            break;
                        case 6:
                            result = RAM6[addr % 0x4000];
                            break;
                        case 7:
                            result = RAM7[addr % 0x4000];
                            break;
                    }
                    break;
                default:
                    break;
            }

            return result;
        }

        /// <summary>
        /// Simulates writing to the bus (no contention)
        /// Paging should be handled here
        /// </summary>
        /// <param name="addr"></param>
        /// <param name="value"></param>
        public override void WriteBus(ushort addr, byte value)
        {
            int divisor = addr / 0x4000;

            switch (divisor)
            {
                // ROM 0x000
                case 0:
                    // cannot write to ROMs
                    /*
                    if (ROMPaged == 0)
                        ROM0[addr % 0x4000] = value;
                    else
                        ROM1[addr % 0x4000] = value;
                        */
                    break;

                // RAM 0x4000 (RAM5 - Bank5 or shadow bank RAM7)
                case 1:
                    RAM5[addr % 0x4000] = value;
                    break;

                // RAM 0x8000 (RAM2 - Bank2)
                case 2:
                    RAM2[addr % 0x4000] = value;
                    break;

                // RAM 0xc000 (any ram bank 0 - 7 may be paged in - default bank0)
                case 3:
                    switch (RAMPaged)
                    {
                        case 0:
                            RAM0[addr % 0x4000] = value;
                            break;
                        case 1:
                            RAM1[addr % 0x4000] = value;
                            break;
                        case 2:
                            RAM2[addr % 0x4000] = value;
                            break;
                        case 3:
                            RAM3[addr % 0x4000] = value;
                            break;
                        case 4:
                            RAM4[addr % 0x4000] = value;
                            break;
                        case 5:
                            RAM5[addr % 0x4000] = value;
                            break;
                        case 6:
                            RAM6[addr % 0x4000] = value;
                            break;
                        case 7:
                            RAM7[addr % 0x4000] = value;
                            break;
                    }
                    break;
                default:
                    break;
            }

            // update ULA screen buffer if necessary
            if ((addr & 49152) == 16384 && _render)
                ULADevice.UpdateScreenBuffer(CurrentFrameCycle);
        }

        /// <summary>
        /// Reads a byte of data from a specified memory address
        /// (with memory contention if appropriate)
        /// </summary>
        /// <param name="addr"></param>
        /// <returns></returns>
        public override byte ReadMemory(ushort addr)
        {
            if (ULADevice.IsContended(addr))
                CPU.TotalExecutedCycles += ULADevice.contentionTable[CurrentFrameCycle];
            
            var data = ReadBus(addr);
            return data;
        }

        /// <summary>
        /// Writes a byte of data to a specified memory address
        /// (with memory contention if appropriate)
        /// </summary>
        /// <param name="addr"></param>
        /// <param name="value"></param>
        public override void WriteMemory(ushort addr, byte value)
        {
            // apply contention if necessary
            if (ULADevice.IsContended(addr))
                CPU.TotalExecutedCycles += ULADevice.contentionTable[CurrentFrameCycle];

            WriteBus(addr, value);
        }
        
        /// <summary>
        /// ULA reads the memory at the specified address
        /// (No memory contention)
        /// Will read RAM5 (screen0) by default, unless RAM7 (screen1) is selected as output
        /// </summary>
        /// <param name="addr"></param>
        /// <returns></returns>
        public override byte FetchScreenMemory(ushort addr)
        {
            byte value = new byte();

            if (SHADOWPaged && !PagingDisabled)
            {
                // shadow screen should be outputted
                // this lives in RAM7
                value = RAM7[addr & 0x3FFF];                
            }
            else
            {
                // shadow screen is not set to display or paging is disabled (probably in 48k mode) 
                // (use screen0 at RAM5)
                value = RAM5[addr & 0x3FFF];
            }

            return value;
        }

        /// <summary>
        /// Sets up the ROM
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="startAddress"></param>
        public override void InitROM(RomData romData)
        {
            RomData = romData;
            // 128k uses ROM0 and ROM1
            // 128k loader is in ROM0, and fallback 48k rom is in ROM1
            for (int i = 0; i < 0x4000; i++)
            {
                ROM0[i] = RomData.RomBytes[i];
                if (RomData.RomBytes.Length > 0x4000)
                    ROM1[i] = RomData.RomBytes[i + 0x4000];
            }
        }
    }
}
