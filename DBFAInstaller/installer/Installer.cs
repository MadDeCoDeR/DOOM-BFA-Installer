using DBFAInstaller.enums;
using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using DBFAInstaller.utils;

namespace DBFAInstaller.installer
{
    class Installer
    {
        public static void install(GameType gameType, CPUArch cpuArch, string path)
        {
            bool removeSettings = Boolean.Parse(Properties.Resources.RemoveSettings);
            if (removeSettings)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Win32SettingsRemover.removeSettings();
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    UnixSettingsRemover.removeSettings();
                }
                else
                {
                    MessageBox.Show("Unsupported Operating System");
                }
            }

            switch (cpuArch) {
                case CPUArch.x64:
                    {
                        ZipManager.extractFiles(Properties.Resources.x64, path);
                        break;
                    }
                case CPUArch.x86:
                    {
                        ZipManager.extractFiles(Properties.Resources.x86, path);
                        break;
                    }
            }

            switch(gameType)
            {
                case GameType.BFG:
                    {
                        ZipManager.extractFiles(Properties.Resources._base, path + "/base");
                        ZipManager.extractFiles(Properties.Resources.base_BFG, path + "/base");
                        break;
                    }
                case GameType.NEW:
                    {
                        ZipManager.extractFiles(Properties.Resources._base, path + "/base");
                        break;
                    }
                case GameType.CLASSIC:
                    {
                        ZipManager.extractFiles(Properties.Resources.base_CLASSIC, path);
                        break;
                    }
            }
        }
    }
}
