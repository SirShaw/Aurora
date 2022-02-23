using HidLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aurora.Devices.CoolerMaster.HidLibrary
{
    public class MP750 : HidDeviceBase
    {
        private int packageSize = 65; // Number of bytes to write

        public MP750()
        {
            VendorID = 0x2516;
            ProductIDs = new[] { 0x0109 };
            UsagePage = unchecked((short)0xff00);
        }

        public override void SetColor(byte r, byte g, byte b)
        {
            byte[] data = new byte[packageSize];

            for (var i = 0; i < packageSize; i++)
            {
                data[i] = 0x00;
            }

            data[1] = 0x01;
            data[2] = 0x04;
            data[3] = r;
            data[4] = g;
            data[5] = b;

            device?.Write(data);
        }
    }
}
