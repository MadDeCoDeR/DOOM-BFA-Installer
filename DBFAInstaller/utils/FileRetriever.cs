using DBFAInstaller.enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Net.WebRequestMethods;

namespace DBFAInstaller.utils
{
    class FileRetriever
    {
        private HttpClient client;

        public FileRetriever()
        {
            this.client = new HttpClient();
        }

        public async Task<bool> GetRemoteBinaries(CPUArch arch, string path, ProgressBar progressBar, Label label)
        {
            string url = "https://github.com/MadDeCoDeR/Classic-RBDOOM-3-BFG/releases/download/" + Properties.Resources.Version;
            switch (arch)
            {
                case CPUArch.x64:
                    url += "/windows-RC-retail.zip";
                    break;
                case CPUArch.x86:
                    url += "/windows-32-bit-RC-retail.zip";
                    break;
            }
            HttpResponseMessage response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            Thread.Sleep(2000);
            if (response.IsSuccessStatusCode)
            {
                long? contentLength = response.Content.Headers.ContentLength;
                if (contentLength != null)
                {
                    label.Invoke(new Action(() =>
                    {
                        label.Text = "Downloading";
                    }));
                    progressBar.Invoke(new Action(() => {
                        progressBar.Maximum = (int)contentLength;
                    }));
                    byte[] data = new byte[(int)contentLength];
                    Stream stream = await response.Content.ReadAsStreamAsync();
                    int offset = 0;
                    while(true)
                    {
                        byte[] buffer = new byte[1024];
                        int readData = stream.Read(buffer, 0, 1024);
                        if (readData == 0) { break; }
                        Array.Copy(buffer, 0, data, offset, readData);
                        offset += readData;

                        progressBar.Invoke(new Action(() => {
                            progressBar.Value += readData;
                        }));
                    }
                    stream.Dispose();
                    progressBar.Invoke(new Action(() => { 
                        progressBar.Maximum += ZipManager.countFiles(data);
                    }));
                    ZipManager.extractFiles(data, path, progressBar, label);
                    return true;
                }
            }
            return false;
        }
    }
}
