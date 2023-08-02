using DBFAInstaller.enums;
using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using DBFAInstaller.utils;
using System.Threading.Tasks;

namespace DBFAInstaller.installer
{
    class Installer
    {
        public static int percentage { get; set; }
        public static int total { get; set; }
        public static async Task<bool> install(GameType gameType, CPUArch cpuArch, string path, ProgressBar progressBar, Label label)
        {
            Task<bool> installTask = installTaskImpl(gameType, cpuArch, path, progressBar, label);
            await Task.WhenAny(installTask);

            return installTask.Result;
        }

        private static async Task<bool> installTaskImpl(GameType gameType, CPUArch cpuArch, string path, ProgressBar progressBar, Label l)
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

                /*countFiles(gameType, cpuArch);

                progressBar.Invoke(new Action(() =>
                {
                    progressBar.Maximum = total;
                }));*/

                FileRetriever fileRetriever = new FileRetriever();

            if (gameType != GameType.CLASSIC)
            {
                return await fileRetriever.GetRemoteBinaries(cpuArch, path, progressBar, l);
            }

                switch (gameType)
                {
                    /*case GameType.BFG:
                        {
                            ZipManager.extractFiles(Properties.Resources._base, path + "/base", progressBar);
                            ZipManager.extractFiles(Properties.Resources.base_BFG, path + "/base", progressBar);
                            break;
                        }
                    case GameType.NEW:
                        {
                            ZipManager.extractFiles(Properties.Resources._base, path + "/base", progressBar);
                            ZipManager.extractFiles(Properties.Resources.base_NEW, path + "/base", progressBar);
                            break;
                        }*/
                    case GameType.CLASSIC:
                        {
                            //ZipManager.extractFiles(Properties.Resources.base_CLASSIC, path, progressBar);
                            break;
                        }
                }
            return false;
        }

        /*private static void countFiles(GameType gameType, CPUArch cpuArch)
        {
            switch (cpuArch)
            {
                case CPUArch.x64:
                    {
                        total += ZipManager.countFiles(Properties.Resources.x64);
                        break;
                    }
                *//*case CPUArch.x86:
                    {
                        total += ZipManager.countFiles(Properties.Resources.x86);
                        break;
                    }*//*
            }

            switch (gameType)
            {
                case GameType.BFG:
                    {
                        total += ZipManager.countFiles(Properties.Resources._base);
                        total += ZipManager.countFiles(Properties.Resources.base_BFG);
                        break;
                    }
                case GameType.NEW:
                    {
                        total += ZipManager.countFiles(Properties.Resources._base);
                        total += ZipManager.countFiles(Properties.Resources.base_NEW);
                        break;
                    }
                case GameType.CLASSIC:
                    {
                        total += ZipManager.countFiles(Properties.Resources.base_CLASSIC);
                        break;
                    }
            }
        }*/
    }
}
