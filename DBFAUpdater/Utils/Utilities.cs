using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace DBFAUpdater.Utils;

public static class Utilities
{
    public static async Task SelectiveExtraction(string SourceArchive, string DestinationFolder, string LookupString)
    {
        using(ZipArchive zipArchive = await ZipFile.OpenReadAsync(SourceArchive))
        {
            List<ZipArchiveEntry> FoundEntries = zipArchive.Entries.Where(entry => entry.FullName.Contains(LookupString)).ToList();
            Directory.CreateDirectory(DestinationFolder);
            foreach(ZipArchiveEntry entry in FoundEntries)
            {
                int lastSlashIndex = entry.FullName.LastIndexOf("/");
                string entryFolderName = lastSlashIndex > 0 ? entry.FullName.Substring(0, lastSlashIndex) : entry.FullName;
                if (!Directory.Exists(DestinationFolder + "/" + entryFolderName) && lastSlashIndex > 0)
                {
                    Directory.CreateDirectory(DestinationFolder + "/" + entryFolderName);
                }
                await entry.ExtractToFileAsync(DestinationFolder + "/" + entry.FullName, true);
            }
        }
    }

    public static async Task CopyDirectoryContents(string source, string destination)
    {
        foreach(var dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(dir.Replace(source, destination));
        }

        foreach(var file in Directory.GetFiles(source, "*.*", SearchOption.AllDirectories))
        {
            File.Copy(file, file.Replace(source, destination), true);
        }
    }

    public static void SafeCopy(string source, string destination)
    {
        if (File.Exists(source))
        {
            string fileName = Path.GetFileName(source);
            File.Copy(source, destination + "/" + fileName, true);
        }
    }

    public static long CalculateFileSizes(string[] files)
    {
        long totalSize = 0;
        foreach( string file in files)
        {
            using(FileStream fileStream = File.OpenRead(file))
            {
                totalSize += fileStream.Length;
            }
        }

        return totalSize;
    }
}