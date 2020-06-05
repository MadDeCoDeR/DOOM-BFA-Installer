using Mono.Unix;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace DBFAInstaller.utils
{
    class ZipManager
    {
        public static void extractFiles(byte[] data, string path)
        {
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
                }
            }
        }
    }
}
