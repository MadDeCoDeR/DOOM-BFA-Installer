using DBFAInstaller.utils;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace DBFAInstaller.installer
{
    class Win32SettingsRemover
    {

        [DllImport("Shell32.dll")]
        private static extern int SHGetKnownFolderPath(
        [MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint dwFlags, IntPtr hToken,
        out IntPtr ppszPath);

        public static void removeSettings()
        {
            int res = SHGetKnownFolderPath(new Guid("4C5C32FF-BB9D-43b0-B5B4-2D72E54EAAA4"), 0x8000 | 0x0800, new IntPtr(0), out IntPtr outPath);
            if (res >= 0)
            {
                string path = Marshal.PtrToStringUni(outPath);
                DirectoryInfo directory = new DirectoryInfo(path + "/id Software/DOOM BFA");
                if (directory.Exists)
                {
                    FileRemover.deleteFiles("profile.bin", directory);
                    FileRemover.deleteFiles("DBFAConfig.cfg", directory);
                }
            }
        }
    }
}
