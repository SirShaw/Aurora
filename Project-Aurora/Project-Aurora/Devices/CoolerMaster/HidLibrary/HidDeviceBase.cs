using HidLibrary;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aurora.Devices.CoolerMaster.HidLibrary
{
    public abstract class HidDeviceBase
    {
        protected HidDevice device = null;

        public Color LastColor { get; set; } = Color.Empty;

        public int VendorID { get; protected set; }
        public int[] ProductIDs { get; protected set; }
        public short UsagePage { get; protected set; }

        public virtual bool IsConnected => device?.IsOpen ?? false;

        public virtual bool Connect()
        {
            if (!IsConnected)
            {
                try
                {
                    IEnumerable<HidDevice> devices = HidDevices.Enumerate(VendorID, ProductIDs);

                    if (devices.Count() > 0)
                    {
                        device = devices.First(x => x.Capabilities.UsagePage == UsagePage);
                        device?.OpenDevice();
                    }
                }
                catch (Exception) { }
            }

            return IsConnected;
        }

        public virtual bool Disconnect()
        {
            if (IsConnected)
            {
                try
                {
                    device?.CloseDevice();
                }
                catch (Exception) { }
            }

            return !IsConnected;
        }

        public abstract void SetColor(byte r, byte g, byte b);
    }
}
