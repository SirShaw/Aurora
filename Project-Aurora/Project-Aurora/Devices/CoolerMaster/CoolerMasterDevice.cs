using Aurora.Settings;
using Aurora.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DK = Aurora.Devices.DeviceKeys;

namespace Aurora.Devices.CoolerMaster
{
    public class CoolerMasterDevice : IDevice
    {
        private VariableRegistry variableRegistry = null;
        private Stopwatch watch = new Stopwatch();
        private long lastUpdateTime = 0;

        private bool loggedLayout;
        private readonly List<(SDK.Native.DEVICE_INDEX Device, SDK.Native.COLOR_MATRIX Matrix)> SDKDevices = new List<(SDK.Native.DEVICE_INDEX, SDK.Native.COLOR_MATRIX)>();
        private readonly List<HidLibrary.HidDeviceBase> HidLibraryDevices = new List<HidLibrary.HidDeviceBase>();

        public string DeviceName => "CoolerMaster";

        protected string DeviceInfo => string.Join(", ", SDKDevices.Select(sd => Enum.GetName(typeof(SDK.Native.DEVICE_INDEX), sd.Device)).Concat(HidLibraryDevices.Select(hd => hd.GetType().Name)));

        public string DeviceDetails => IsInitialized
            ? $"Initialized{(string.IsNullOrWhiteSpace(DeviceInfo) ? "" : ": " + DeviceInfo)}"
            : "Not Initialized";

        public string DeviceUpdatePerformance => IsInitialized
            ? lastUpdateTime + " ms"
            : "";

        public bool IsInitialized => SDKDevices.Count != 0 || HidLibraryDevices.Count != 0;

        public VariableRegistry RegisteredVariables
        {
            get
            {
                if (variableRegistry == null)
                {
                    variableRegistry = new VariableRegistry();
                    variableRegistry.Register($"{DeviceName}_enable_sdk", true, "Enable CoolerMaster SDK");
                    variableRegistry.Register($"{DeviceName}_enable_hidlibrary", false, "Enable Hid library");
                    variableRegistry.Register($"{DeviceName}_enable_shutdown_color", false, "Enable shutdown color");
                    variableRegistry.Register($"{DeviceName}_shutdown_color", new RealColor(Color.FromArgb(255, 255, 255, 255)), "Shutdown color");
                }

                return variableRegistry;
            }
        }

        public bool Initialize()
        {
            if (!IsInitialized)
            {
                if (Global.Configuration.VarRegistry.GetVariable<bool>($"{DeviceName}_enable_sdk"))
                {
                    Global.logger.Info($"Trying to initialize CoolerMaster SDK version {SDK.Native.GetCM_SDK_DllVer()}");

                    foreach (var device in SDK.Native.Devices.Where(d => d != SDK.Native.DEVICE_INDEX.DEFAULT))
                    {
                        if (SDK.Native.IsDevicePlug(device) && SDK.Native.EnableLedControl(true, device))
                        {
                            SDKDevices.Add((device, SDK.Native.COLOR_MATRIX.Create()));
                        }
                    }
                }

                if (Global.Configuration.VarRegistry.GetVariable<bool>($"{DeviceName}_enable_hidlibrary"))
                {
                    Global.logger.Info($"Trying to initialize devices using Hid library");

                    var devices = from type in Assembly.GetExecutingAssembly().GetTypes()
                                  where typeof(HidLibrary.HidDeviceBase).IsAssignableFrom(type) && !type.IsAbstract
                                  let inst = (HidLibrary.HidDeviceBase)Activator.CreateInstance(type)
                                  select inst;

                    foreach (var device in devices)
                    {
                        if (device.Connect())
                        {
                            HidLibraryDevices.Add(device);
                        }
                    }
                }
            }

            return IsInitialized;
        }

        public void Reset()
        {
            Shutdown();
            Initialize();
        }

        public void Shutdown()
        {
            if (!IsInitialized)
                return;

            if (Global.Configuration.VarRegistry.GetVariable<bool>($"{DeviceName}_enable_shutdown_color"))
            {
                Color color = Global.Configuration.VarRegistry.GetVariable<RealColor>($"{DeviceName}_shutdown_color").GetDrawingColor();

                // Need testing
                foreach (var (dev, colors) in SDKDevices)
                {
                    for (int column = 0; column < SDK.Native.MAX_LED_COLUMN; column++)
                    {
                        for (int row = 0; row < SDK.Native.MAX_LED_ROW; row++)
                        {
                            colors.KeyColor[column, row] = new SDK.Native.KEY_COLOR(color);
                        }
                    }

                    SDK.Native.SetAllLedColor(colors, dev);
                }

                foreach (var dev in HidLibraryDevices)
                {
                    // Apply shutdown color
                    dev.SetColor(color.R, color.G, color.B);
                }
            }

            foreach (var (dev, _) in SDKDevices)
                SDK.Native.EnableLedControl(false, dev);

            foreach (var dev in HidLibraryDevices)
                dev.Disconnect();

            SDKDevices.Clear();
            HidLibraryDevices.Clear();
        }

        public bool UpdateDevice(DeviceColorComposition colorComposition, DoWorkEventArgs e, bool forced = false)
        {
            watch.Restart();

            bool result = UpdateDevice(colorComposition.keyColors, e, forced);

            watch.Stop();
            lastUpdateTime = watch.ElapsedMilliseconds;

            return result;
        }

        public bool UpdateDevice(Dictionary<DK, Color> keyColors, DoWorkEventArgs e, bool forced = false)
        {
            foreach (var (dev, colors) in SDKDevices)
            {
                if (SDK.Native.Mice.Contains(dev) && Global.Configuration.DevicesDisableMouse)
                    continue;
                if (SDK.Native.Keyboards.Contains(dev) && Global.Configuration.DevicesDisableKeyboard)
                    continue;

                if (!SDK.KeyMaps.LayoutMapping.TryGetValue(dev, out var dict))
                {
                    dict = SDK.KeyMaps.GenericFullSize;
                    if (!loggedLayout)
                    {
                        Global.logger.Error($"Could not find layout for device {Enum.GetName(typeof(SDK.Native.DEVICE_INDEX), dev)}, using generic.");
                        loggedLayout = true;
                    }
                }

                foreach (var (dk, clr) in keyColors)
                {
                    DK key = dk;
                    //HACK: the layouts for some reason switch backslash and enter
                    //around between ANSI and ISO needlessly. We swap them around here
                    if (key == DK.ENTER && !Global.kbLayout.Loaded_Localization.IsANSI())
                        key = DK.BACKSLASH;

                    if (dict.TryGetValue(key, out var position))
                        colors.KeyColor[position.row, position.column] = new SDK.Native.KEY_COLOR(ColorUtils.CorrectWithAlpha(clr));
                }

                SDK.Native.SetAllLedColor(colors, dev);
            }

            var color = MergeKeyColors(keyColors);

            foreach (var dev in HidLibraryDevices)
            {
                if (color != dev.LastColor)
                {
                    dev.LastColor = color;
                    dev.SetColor(color.R, color.G, color.B);
                }
            }

            return true;
        }

        public Color MergeKeyColors(Dictionary<DK, Color> keyColors)
        {
            int keys = keyColors.Count;

            int r = 0;
            int g = 0;
            int b = 0;
            int a = 0;

            foreach (var item in keyColors)
            {
                r += item.Value.R;
                g += item.Value.G;
                b += item.Value.B;
                a += item.Value.A;
            }

           return Color.FromArgb(a / keys, r / keys, g / keys, b / keys);
        }
    }
}
