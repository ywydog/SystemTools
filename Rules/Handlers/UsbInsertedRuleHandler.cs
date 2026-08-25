using System;
using System.IO;

namespace SystemTools;

public partial class Plugin
{
    private static bool HandleUsbInsertedRule(object? settings)
    {
        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.DriveType == DriveType.Removable && drive.IsReady)
                {
                    return true;
                }
            }
        }
        catch
        {
            // 枚举驱动器失败时按“未插入”处理，保证规则链路稳定。
            return false;
        }

        return false;
    }
}