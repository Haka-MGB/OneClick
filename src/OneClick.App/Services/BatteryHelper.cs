using System.Runtime.InteropServices;

namespace OneClick.App.Services;

/// <summary>Detects whether the device has a battery (FR-BAT-01).</summary>
public static class BatteryHelper
{
    [DllImport("kernel32.dll")]
    private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS status);

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_POWER_STATUS
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public uint BatteryLifeTime;
        public uint BatteryFullLifeTime;
    }

    public static bool DeviceHasBattery()
    {
        try
        {
            return GetSystemPowerStatus(out var s) && s.BatteryFlag != 128; // 128 = no battery
        }
        catch
        {
            return false;
        }
    }
}
