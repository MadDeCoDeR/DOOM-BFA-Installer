using DBFAInstaller.utils;
using System;
using System.IO;

namespace DBFAInstaller.installer
{
    class UnixSettingsRemover
    {
        public static void removeSettings()
        {
            DirectoryInfo directory = new DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "/.doombfa");
            if (directory.Exists)
            {
                FileRemover.deleteFiles("profile.bin", directory);
                FileRemover.deleteFiles("DBFAConfig.cfg", directory);
            }
        }
    }
}
