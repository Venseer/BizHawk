﻿using BizHawk.Common.NumberExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BizHawk.Common;

namespace BizHawk.Emulation.Cores.Computers.SinclairSpectrum
{
    /// <summary>
    /// Logical object representing a standard +3 disk image
    /// </summary>
    public class CPCFloppyDisk : FloppyDisk
    {
        /// <summary>
        /// The format type
        /// </summary>
        public override DiskType DiskFormatType => DiskType.CPC;

        /// <summary>
        /// Attempts to parse incoming disk data 
        /// </summary>
        /// <param name="diskData"></param>
        /// <returns>
        /// TRUE:   disk parsed
        /// FALSE:  unable to parse disk
        /// </returns>
        public override bool ParseDisk(byte[] data)
        {
            // look for standard magic string
            string ident = Encoding.ASCII.GetString(data, 0, 16);

            if (!ident.ToUpper().Contains("MV - CPC"))
            {
                // incorrect format
                return false;
            }

            // read the disk information block
            DiskHeader.DiskIdent = ident;
            DiskHeader.DiskCreatorString = Encoding.ASCII.GetString(data, 0x22, 14);
            DiskHeader.NumberOfTracks = data[0x30];
            DiskHeader.NumberOfSides = data[0x31];
            DiskHeader.TrackSizes = new int[DiskHeader.NumberOfTracks * DiskHeader.NumberOfSides];
            DiskTracks = new Track[DiskHeader.NumberOfTracks * DiskHeader.NumberOfSides];
            DiskData = data;
            int pos = 0x32;

            // standard CPC format all track sizes are the same in the image
            for (int i = 0; i < DiskHeader.NumberOfTracks * DiskHeader.NumberOfSides; i++)
            {
                DiskHeader.TrackSizes[i] = MediaConverter.GetWordValue(data, pos);
            }

            // move to first track information block
            pos = 0x100;

            // parse each track
            for (int i = 0; i < DiskHeader.NumberOfTracks * DiskHeader.NumberOfSides; i++)
            {
                // check for unformatted track
                if (DiskHeader.TrackSizes[i] == 0)
                {
                    DiskTracks[i] = new Track();
                    DiskTracks[i].Sectors = new Sector[0];
                    continue;
                }

                int p = pos;
                DiskTracks[i] = new Track();

                // track info block
                DiskTracks[i].TrackIdent = Encoding.ASCII.GetString(data, p, 12);
                p += 16;
                DiskTracks[i].TrackNumber = data[p++];
                DiskTracks[i].SideNumber = data[p++];
                p += 2;
                DiskTracks[i].SectorSize = data[p++];
                DiskTracks[i].NumberOfSectors = data[p++];
                DiskTracks[i].GAP3Length = data[p++];
                DiskTracks[i].FillerByte = data[p++];

                int dpos = pos + 0x100;

                // sector info list
                DiskTracks[i].Sectors = new Sector[DiskTracks[i].NumberOfSectors];
                for (int s = 0; s < DiskTracks[i].NumberOfSectors; s++)
                {
                    DiskTracks[i].Sectors[s] = new Sector();

                    DiskTracks[i].Sectors[s].TrackNumber = data[p++];
                    DiskTracks[i].Sectors[s].SideNumber = data[p++];
                    DiskTracks[i].Sectors[s].SectorID = data[p++];
                    DiskTracks[i].Sectors[s].SectorSize = data[p++];
                    DiskTracks[i].Sectors[s].Status1 = data[p++];
                    DiskTracks[i].Sectors[s].Status2 = data[p++];
                    DiskTracks[i].Sectors[s].ActualDataByteLength = MediaConverter.GetWordValue(data, p);
                    p += 2;

                    // actualdatabytelength value is calculated now
                    if (DiskTracks[i].Sectors[s].SectorSize == 0)
                    {
                        // no sectorsize specified - DTL will be used at runtime
                        DiskTracks[i].Sectors[s].ActualDataByteLength = DiskHeader.TrackSizes[i];
                    }
                    else if (DiskTracks[i].Sectors[s].SectorSize > 6)
                    {
                        // invalid - wrap around to 0
                        DiskTracks[i].Sectors[s].ActualDataByteLength = DiskHeader.TrackSizes[i];
                    }
                    else if (DiskTracks[i].Sectors[s].SectorSize == 6)
                    {
                        // only 0x1800 bytes are stored
                        DiskTracks[i].Sectors[s].ActualDataByteLength = 0x1800;
                    }
                    else
                    {
                        // valid sector size for this format
                        DiskTracks[i].Sectors[s].ActualDataByteLength = 0x80 << DiskTracks[i].Sectors[s].SectorSize;
                    }

                    // sector data - begins at 0x100 offset from the start of the track info block (in this case dpos)
                    DiskTracks[i].Sectors[s].SectorData = new byte[DiskTracks[i].Sectors[s].ActualDataByteLength];

                    // copy the data
                    for (int b = 0; b < DiskTracks[i].Sectors[s].ActualDataByteLength; b++)
                    {
                        DiskTracks[i].Sectors[s].SectorData[b] = data[dpos + b];
                    }                    

                    // move dpos to the next sector data postion
                    dpos += DiskTracks[i].Sectors[s].ActualDataByteLength;
                }

                // move to the next track info block
                pos += DiskHeader.TrackSizes[i];
            }

            // run protection scheme detector
            ParseProtection();

            return true;
        }

        /// <summary>
        /// State serlialization
        /// </summary>
        /// <param name="ser"></param>
        public override void SyncState(Serializer ser)
        {
            ser.BeginSection("Plus3FloppyDisk");

            ser.Sync("CylinderCount", ref CylinderCount);
            ser.Sync("SideCount", ref SideCount);
            ser.Sync("BytesPerTrack", ref BytesPerTrack);
            ser.Sync("WriteProtected", ref WriteProtected);
            ser.SyncEnum("Protection", ref Protection);

            ser.Sync("DirtyData", ref DirtyData);
            if (DirtyData)
            {

            }

            ser.EndSection();
        }
    }
}
