﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BizHawk.Emulation.Cores.Computers.SinclairSpectrum
{
    public partial class ZX128Plus3 : SpectrumBase
    {
        /// <summary>
        /// Reads a byte of data from a specified port address
        /// </summary>
        /// <param name="port"></param>
        /// <returns></returns>
        public override byte ReadPort(ushort port)
        {
            bool deviceAddressed = true;

            // process IO contention
            ContendPortAddress(port);

            int result = 0xFF;

            // check AY
            if (AYDevice.ReadPort(port, ref result))
                return (byte)result;

            // Kempston joystick input takes priority over all other input
            // if this is detected just return the kempston byte
            if ((port & 0xe0) == 0 || (port & 0x20) == 0)
            {
                if (LocateUniqueJoystick(JoystickType.Kempston) != null)
                    return (byte)((KempstonJoystick)LocateUniqueJoystick(JoystickType.Kempston) as KempstonJoystick).JoyLine;

                InputRead = true;
            }
            else if (UPDDiskDevice.ReadPort(port, ref result))
            {
                return (byte)result;
            }
            else
            {
                if (KeyboardDevice.ReadPort(port, ref result))
                {
                    // not a lagframe
                    InputRead = true;

                    // process tape INs
                    TapeDevice.ReadPort(port, ref result);
                }
                else
                    deviceAddressed = false;
            }

            if (!deviceAddressed)
            {
                // If this is an unused port the floating memory bus should be returned
                // Floating bus is read on the previous cycle
                long _tStates = CurrentFrameCycle - 1;

                // if we are on the top or bottom border return 0xff
                if ((_tStates < ULADevice.contentionStartPeriod) || (_tStates > ULADevice.contentionEndPeriod))
                {
                    result = 0xff;
                }
                else
                {
                    if (ULADevice.floatingBusTable[_tStates] < 0)
                    {
                        result = 0xff;
                    }
                    else
                    {
                        result = ReadBus((ushort)ULADevice.floatingBusTable[_tStates]);
                    }
                }
            }

            return (byte)result;
        }

        /// <summary>
        /// Writes a byte of data to a specified port address
        /// </summary>
        /// <param name="port"></param>
        /// <param name="value"></param>
        public override void WritePort(ushort port, byte value)
        {
            // process IO contention
            ContendPortAddress(port);

            // get a BitArray of the port
            BitArray portBits = new BitArray(BitConverter.GetBytes(port));
            // get a BitArray of the value byte
            BitArray bits = new BitArray(new byte[] { value });

            // Check whether the low bit is reset
            bool lowBitReset = !portBits[0]; // (port & 0x01) == 0;

            AYDevice.WritePort(port, value);

            UPDDiskDevice.WritePort(port, value);

            // port 0x7ffd - hardware should only respond when bits 1 & 15 are reset and bit 14 is set
            if (port == 0x7ffd)
            {
                if (!PagingDisabled)
                {
                    // bits 0, 1, 2 select the RAM page
                    var rp = value & 0x07;
                    if (rp < 8)
                        RAMPaged = rp;

                    // bit 3 controls shadow screen
                    SHADOWPaged = bits[3];

                    // Bit 5 set signifies that paging is disabled until next reboot
                    PagingDisabled = bits[5];

                    // portbit 4 is the LOW BIT of the ROM selection
                    ROMlow = bits[4];
                }                         
            }
            // port 0x1ffd - hardware should only respond when bits 1, 13, 14 & 15 are reset and bit 12 is set
            if (port == 0x1ffd)
            {
                if (!PagingDisabled)
                {
                    if (!bits[0])
                    {
                        // special paging is not enabled - get the ROMpage high byte
                        ROMhigh = bits[2];

                        // set the special paging mode flag
                        SpecialPagingMode = false;
                    }
                    else
                    {
                        // special paging is enabled
                        // this is decided based on combinations of bits 1 & 2
                        // Config 0 = Bit1-0 Bit2-0
                        // Config 1 = Bit1-1 Bit2-0
                        // Config 2 = Bit1-0 Bit2-1
                        // Config 3 = Bit1-1 Bit2-1
                        BitArray confHalfNibble = new BitArray(2);
                        confHalfNibble[0] = bits[1];
                        confHalfNibble[1] = bits[2];

                        // set special paging configuration
                        PagingConfiguration = ZXSpectrum.GetIntFromBitArray(confHalfNibble);

                        // set the special paging mode flag
                        SpecialPagingMode = true;
                    }
                }

                // bit 4 is the printer port strobe
                PrinterPortStrobe = bits[4];
            }
            
            // Only even addresses address the ULA
            if (lowBitReset)
            {
                // store the last OUT byte
                LastULAOutByte = value;

                /*
                    Bit   7   6   5   4   3   2   1   0
                        +-------------------------------+
                        |   |   |   | E | M |   Border  |
                        +-------------------------------+
                */

                // Border - LSB 3 bits hold the border colour
                if (ULADevice.borderColour != (value & BORDER_BIT))
                    ULADevice.UpdateScreenBuffer(CurrentFrameCycle);

                ULADevice.borderColour = value & BORDER_BIT;

                // Buzzer
                BuzzerDevice.ProcessPulseValue((value & EAR_BIT) != 0);

                // Tape
                TapeDevice.WritePort(port, value);

                // Tape
                //TapeDevice.ProcessMicBit((value & MIC_BIT) != 0);
            }

           
            LastULAOutByte = value;
        }

        /// <summary>
        /// +3 and 2a overidden method
        /// </summary>
        public override int _ROMpaged
        {
            get
            {
                // calculate the ROMpage from the high and low bits
                var rp = ZXSpectrum.GetIntFromBitArray(new BitArray(new bool[] { ROMlow, ROMhigh }));

                if (rp != 0)
                {

                }

                return rp;
            }
            set { ROMPaged = value; }
        }

        /// <summary>
        /// Override port contention
        /// +3/2a does not have the same ULA IO contention
        /// </summary>
        /// <param name="addr"></param>
        public override void ContendPortAddress(ushort addr)
        {
            //CPU.TotalExecutedCycles += 4;
        }
    }
}
