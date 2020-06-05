using System.IO;

namespace DBFAInstaller.utils
{
    class FileRemover
    {
        public static void deleteFiles(string fileName, DirectoryInfo directory)
        {
            FileInfo[] Files = directory.GetFiles(fileName, SearchOption.AllDirectories);
            foreach (FileInfo file in Files)
            {
                file.Delete();
            }
        }
    }
}
