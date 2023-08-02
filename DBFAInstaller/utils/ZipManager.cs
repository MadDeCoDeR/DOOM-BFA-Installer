using Mono.Unix;
using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace DBFAInstaller.utils
{
    class ZipManager
    {
        public static void extractFiles(byte[] data, string path, ProgressBar progressBar, Label label)
        {
            label.Invoke(new Action(() =>
            {
                label.Text = "Extracting";
            }));
            Stream zip = new MemoryStream(data);
            using (ZipArchive zipFile = new ZipArchive(zip))
            {
                foreach (ZipArchiveEntry file in zipFile.Entries)
                {
                    string fullname = Path.Combine(path, file.FullName);
                    if (file.Name == "" || !File.Exists(fullname))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(fullname));
                        if (file.Name != "")
                        {
                            file.ExtractToFile(fullname, true);
                            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                            {
                                UnixFileInfo unixfileInfo = new UnixFileInfo(fullname);
                                unixfileInfo.FileAccessPermissions = FileAccessPermissions.AllPermissions;
                            }
                        }
                        continue;
                    }
                    file.ExtractToFile(fullname, true);
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        UnixFileInfo unixfileInfo = new UnixFileInfo(fullname);
                        unixfileInfo.FileAccessPermissions = FileAccessPermissions.AllPermissions;
                    }
                    installer.Installer.percentage++;
                    progressBar.Invoke(new Action(() =>
                    {
                        progressBar.Value = installer.Installer.percentage;
                        Thread.Sleep(2);
                    }));
                }
            }
        }

        public static int countFiles(byte[] data)
        {
            Stream zip = new MemoryStream(data);
            using (ZipArchive zipFile = new ZipArchive(zip))
            {
                return zipFile.Entries.Count;
            }
        }
    }
}
