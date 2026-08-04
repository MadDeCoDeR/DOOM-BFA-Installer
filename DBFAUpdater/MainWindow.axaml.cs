using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Avalonia.Controls;
using DBFAUpdater.Models;
using MsBox.Avalonia;
using MsBox.Avalonia.Base;
using MsBox.Avalonia.Enums;
using Newtonsoft.Json;

namespace DBFAUpdater;

public class StateMachine
{
    public string? Previous { get; set; }
    public required string Current { get; set; }
    public string? Next { get; set; }

    public Func<FormModel, bool>? Condition { get; set; }
}
public partial class MainWindow : Window
{
    private readonly List<StateMachine> states = new List<StateMachine>
    {
        new StateMachine { Previous = null, Current = "Welcome", Next = "Version", Condition = null },
        new StateMachine { Previous = "Welcome", Current = "Version", Next = "Profile", Condition = null },
        new StateMachine { Previous = "Version", Current = "Profile", Next = "Edition", Condition = (context) => context.Version == VersionEnum.Beta },
        new StateMachine { Previous = "Profile", Current = "Edition", Next = "Addon", Condition = null },
        new StateMachine { Previous = "Edition", Current = "Addon", Next = "InstPath", Condition = null },
        new StateMachine { Previous = "Addon", Current = "InstPath", Next = "Progress", Condition = null },
        new StateMachine { Previous = "InstPath", Current = "Progress", Next = "End", Condition = null },
        new StateMachine { Previous = null, Current = "End", Next = null, Condition = null },

    };

    private readonly HttpClient httpClient;
    private StateMachine currentState;
    public MainWindow()
    {
        InitializeComponent();
        currentState = states[0];
        httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer "); //GK: Put it on releases

        
    }

    private void Window_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ((FormModel)DataContext).PropertyChanged += OnEditionChanged;
    }

    private async void Next_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        HandleState();
    }

    private async void Back_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        HandleState(false);
    }

    private async void HandleState(bool direction = true)
    {
        FormModel dataForm = (FormModel)this.DataContext;
        Control? currentFrame = null;
        Control? upcomingFrame = null;
        StateMachine? upcomingState = direction ? states.GetNextState(currentState) : states.GetPreviousState(currentState);
        if (upcomingState == null)
        {
            this.Close();
            return;
        }

        int found = 0;
        foreach (var child in Main.Children)
        {
            if (child.Name == currentState.Current)
            {
                currentFrame = child;
                found++;
            }

            if (child.Name == upcomingState.Current)
            {
                upcomingFrame = child;
                found++;
            }
            if (found == 2)
            {
                break;
            }
        }

        currentFrame.IsVisible = false;
        currentFrame.IsEnabled = false;
        if (upcomingState.Condition != null && !upcomingState.Condition(dataForm))
        {
            currentState = upcomingState;
            HandleState(direction);
            return;
        }
        upcomingFrame.IsVisible = true;
        upcomingFrame.IsEnabled = true;


        Back.IsVisible = upcomingState.Previous != null || (upcomingState.Current == "Progress");
        Back.IsEnabled = upcomingState.Previous != null || (upcomingState.Current == "Progress");
        Next.IsVisible = upcomingState.Current != "Progress";
        Next.IsEnabled = upcomingState.Previous != "Progress";
        Next.Content = upcomingState.Current == "End" ? "Close" : "Next";

        currentState = upcomingState;

        if (currentState.Current == "Progress")
        {
            await InstallMod();
        }

    }

    private void OnEditionChanged(object sender, PropertyChangedEventArgs e)
    {
        FormModel dataModel = ((FormModel)this.DataContext);
        if (e.PropertyName == "Edition")
        {
            Addon2.IsEnabled = dataModel.Edition == EditionEnum.Classic;
            Addon2.IsVisible = dataModel.Edition == EditionEnum.Classic;
            ClassicPathTitle.IsEnabled = dataModel.Edition == EditionEnum.Classic;
            ClassicPathTitle.IsVisible = dataModel.Edition == EditionEnum.Classic;
            ClassicPath.IsEnabled = dataModel.Edition == EditionEnum.Classic;
            ClassicPath.IsVisible = dataModel.Edition == EditionEnum.Classic;

        }
    }

    private static readonly List<string> SHA256s = new List<string> {
            "B683AC1B1D3F0CA6B92111DB85FC77ECE9D5C034CE5461EB8A7C4ADD8E239A22", //DOOM 3: BFG Edition
            "6DAECF3E621756C8A77B3C3064ED5FB488AFE357A80B7C14BEF35B6811B073CE", //DOOM 3 re-release (2019)
        };

    private static readonly SHA256 sHA256 = SHA256.Create();

    private async Task InstallMod()
    {
        FormModel formModel = ((FormModel)DataContext);
        if (string.IsNullOrEmpty(formModel.MainPath))
        {
            await ShowErrorAndRollback("Please provide an installation Path");
            return;
        }
        if (formModel.Edition == EditionEnum.Classic && !File.Exists(formModel.ClassicPath + "/doom.wad"))
        {
            await ShowErrorAndRollback("Please provide a proper installation path of DOOM 1 + 2");
            return;
        }

        if (formModel.Edition == EditionEnum.Classic && File.Exists(formModel.MainPath + "/base/_common.resources"))
        {
            string filePath = formModel.MainPath + "/base/_common.resources";
            byte[] data = File.ReadAllBytes(filePath);
            string fileSha256 = BitConverter.ToString(sHA256.ComputeHash(data)).ToUpper().Replace("-", "");
            if (fileSha256 == SHA256s[0] || fileSha256 == SHA256s[1])
            {
                await ShowErrorAndRollback("You can't install Classic Edition on DOOM 3 BFG Edition\nPlease Select another path");
                return;
            }
        }
        OperatingSystem os = Environment.OSVersion;
        string osString = os.Platform == PlatformID.Win32NT ? "Windows" : "Linux";
        //First get available releases of DOOM BFA
        GitRelease? latestRelease = null;
        int i = 1;
        ProgressText.Header = "Checking Latest Release";
        ProgressLoad.IsIndeterminate = true;
        while(latestRelease == null) {
            HttpResponseMessage response = await httpClient.GetAsync("https://api.github.com/repos/MadDeCoDeR/Classic-RBDOOM-3-BFG/releases?page=" + i);
            ICollection<GitRelease> releases = JsonConvert.DeserializeObject<ICollection<GitRelease>>(await response.Content.ReadAsStringAsync());
            latestRelease = releases.Where(rel => rel.Prerelease == (formModel.Version == VersionEnum.Beta)).OrderByDescending(rel => rel.PublishedAt).First();
            i++;
        }
        if (latestRelease == null)
        {
            await ShowError("Failed to Find the latest release");
            return;
        }
        GitReleaseAsset? asset = null;
        switch(formModel.Version)
        {
            case VersionEnum.Stable:
                {
                    asset = latestRelease?.Assets?.Where(ast => ast.Name.Contains(formModel.Edition.ToString())).FirstOrDefault();
                    break;
                }
            case VersionEnum.Beta:
                {
                    string debug = formModel.Profile.ToString().ToLower();
                    bool edition = formModel.Edition == EditionEnum.Classic;
                    asset = latestRelease?.Assets?.Where(ast => ast.Name.Contains(osString.ToLower()) && ast.Name.Contains(debug) && ast.Name.Contains("classic") == edition && !ast.Name.Contains("x86")).FirstOrDefault();
                    break;
                }
        }

        if (asset == null)
        {
            await ShowError("Failed to find the right asset");
            return;
        }

        ProgressText.Header = "Downloading DOOM BFA";
        ProgressLoad.IsIndeterminate = false;
        ProgressLoad.ShowProgressText = true;
        await DownloadFile(asset.DownloadUrl, "./doom_bfa.zip");

        ProgressText.Header = "Extracting DOOM BFA";
        ProgressLoad.IsIndeterminate = true;
        ProgressLoad.ShowProgressText = false;

        if (Directory.Exists("./doom_bfa"))
        {
            Directory.Delete("./doom_bfa", true);
        }
        using(FileStream file = File.OpenRead("./doom_bfa.zip"))
        {
            await ZipFile.ExtractToDirectoryAsync(file, "./doom_bfa");
        }
        
        string Destination = formModel.MainPath;

        string source = "./doom_bfa";
        if (formModel.Version == VersionEnum.Stable)
        {
            source += "/" + osString + "-x64";
        }

        await CopyDirectoryContents(source, Destination);
        if (formModel.Version == VersionEnum.Beta && formModel.Profile == ProfileEnum.Retail && !File.Exists(Destination + "/steam_appid.txt"))
        {
            using (StreamWriter outputFile = new StreamWriter(Destination + "/steam_appid.txt"))
            {
                if (formModel.Edition == EditionEnum.Standard)
                {
                    await outputFile.WriteLineAsync("208200");
                } else
                {
                    await outputFile.WriteLineAsync("2280");
                }
            }
        }
        //cleanup
        if (Directory.Exists("./doom_bfa"))
        {
            Directory.Delete("./doom_bfa", true);
        }
        if (File.Exists("./doom_bfa.zip"))
        {
            File.Delete("./doom_bfa.zip");
        }

        if (formModel.Edition == EditionEnum.Classic) {
            ProgressText.Header = "Copying wad files";
            ProgressLoad.IsIndeterminate = false;
            ProgressLoad.ShowProgressText = true;
            await CopyWadFiles(formModel.ClassicPath, formModel.MainPath);

        }

        if (formModel.Addon1)
        {
            await InstallOpenPlatform(formModel.MainPath);
        }

        if (formModel.Addon2 && formModel.Edition == EditionEnum.Classic)
        {
            await ExtractKEXGUS(formModel.ClassicPath, formModel.MainPath);
        }

        this.HandleState();
    }

    private async Task ShowError(string Message)
    {
        IMsBox<ButtonResult> messageBox = MessageBoxManager.GetMessageBoxStandard("Error", Message, ButtonEnum.Ok);
        ButtonResult result = await messageBox.ShowAsync();
        if (result == ButtonResult.Ok)
        {
            this.Close();
        }
    }

    private async Task ShowErrorAndRollback(string Message)
    {
        IMsBox<ButtonResult> messageBox = MessageBoxManager.GetMessageBoxStandard("Error", Message, ButtonEnum.Ok);
        ButtonResult result = await messageBox.ShowAsync();
        if (result == ButtonResult.Ok)
        {
            this.HandleState(false);
        }
    }

    private async Task DownloadFile(string Url, string Path)
    {
        if (File.Exists(Path))
        {
            File.Delete(Path);
        }
        using (FileStream file = File.OpenWrite(Path)) {
            using (HttpResponseMessage downloadResponse = await httpClient.GetAsync(Url, HttpCompletionOption.ResponseHeadersRead)) {

                long? contentLength = downloadResponse.Content.Headers.ContentLength;
                using (Stream downloadStream = await downloadResponse.Content.ReadAsStreamAsync())
                {
                    int offset = 0;
                    while (true) {
                        byte[] buffer = new byte[ 8 * (1024 ^ 2)];
                        int tmpOffset = await downloadStream.ReadAsync(buffer, 0, 8 * (1024 ^ 2));
                        if (tmpOffset > 0) {
                            await file.WriteAsync(buffer, 0, tmpOffset);
                            offset += tmpOffset;
                            ProgressLoad.Value = ((double)((offset * 1.0) / contentLength) * 100);
                            
                        } else
                        {
                            break;
                        }
                    }
                }
            }
        }
    }

    private async Task CopyDirectoryContents(string source, string destination)
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

    private async Task CopyWadFiles(string source, string destination)
    {
        string masterSource = "";
        if (Directory.Exists(Directory.GetParent(source).ToString() + "/base/master/wads"))
        {
            masterSource = Directory.GetParent(source).ToString() + "/base/master/wads";
        } else if (Directory.Exists(source + "/dosdoom/base/master/wads"))
        {
            masterSource = source + "/dosdoom/base/master/wads";
        }
        long totalFileSize = CalculateFileSizes([
            source + "/doom.wad",
            source + "/doom2.wad",
            source + "/extras.wad",
            source + "/sigil.wad",
            source + "/sigil2.wad",
            source + "/nerve.wad",
            source + "/tnt.wad",
            source + "/plutonia.wad",
            source + "/id1.wad",
            ..Directory.GetFiles(masterSource)
        ]);
        long WrittenFileSize = 0;
        WrittenFileSize += await SafeCopyWithProgress(source + "/doom.wad", destination + "/base/wads", WrittenFileSize, totalFileSize);
        WrittenFileSize += await SafeCopyWithProgress(source + "/doom2.wad", destination + "/base/wads", WrittenFileSize, totalFileSize);
        WrittenFileSize += await SafeCopyWithProgress(source + "/extras.wad", destination + "/base/wads", WrittenFileSize, totalFileSize);
        WrittenFileSize += await SafeCopyWithProgress(source + "/sigil.wad", destination + "/base/wads", WrittenFileSize, totalFileSize);
        WrittenFileSize += await SafeCopyWithProgress(source + "/sigil2.wad", destination + "/base/wads", WrittenFileSize, totalFileSize);
        WrittenFileSize += await SafeCopyWithProgress(source + "/nerve.wad", destination + "/base/wads", WrittenFileSize, totalFileSize);
        WrittenFileSize += await SafeCopyWithProgress(source + "/tnt.wad", destination + "/base/wads", WrittenFileSize, totalFileSize);
        WrittenFileSize += await SafeCopyWithProgress(source + "/plutonia.wad", destination + "/base/wads", WrittenFileSize, totalFileSize);
        WrittenFileSize += await SafeCopyWithProgress(source + "/id1.wad", destination + "/base/wads", WrittenFileSize, totalFileSize);
        
        

        //Search for Master Levels
        

        if (!string.IsNullOrEmpty(masterSource)) {
            Directory.CreateDirectory(destination + "/base/wads/master");
            foreach(var file in Directory.GetFiles(masterSource))
            {
                WrittenFileSize += await SafeCopyWithProgress(file, destination + "/base/wads/master", WrittenFileSize, totalFileSize);
            }
        }
    }

    private async Task InstallOpenPlatform(string destination)
    {
        OperatingSystem os = Environment.OSVersion;
        string osString = os.Platform == PlatformID.Win32NT ? "Windows" : "Linux";
        //First get available releases of Open Platform
        GitRelease? latestRelease = null;
        int i = 1;
        ProgressText.Header = "Checking Latest Release";
        ProgressLoad.IsIndeterminate = true;
        ProgressLoad.ShowProgressText = false;
        HttpResponseMessage response = await httpClient.GetAsync("https://api.github.com/repos/MadDeCoDeR/Open_Platform/releases?page=" + i);
        ICollection<GitRelease> releases = JsonConvert.DeserializeObject<ICollection<GitRelease>>(await response.Content.ReadAsStringAsync());
        latestRelease = releases.OrderByDescending(rel => rel.PublishedAt).First();
        i++;
        if (latestRelease == null)
        {
            await ShowError("Failed to Find the latest release");
            return;
        }
        GitReleaseAsset? asset = latestRelease?.Assets?.Where(ast => ast.Name.Contains("Steam")).FirstOrDefault();;

        if (asset == null)
        {
            await ShowError("Failed to find the right asset");
            return;
        }

        ProgressText.Header = "Downloading Open Platform";
        ProgressLoad.IsIndeterminate = false;
        ProgressLoad.ShowProgressText = true;
        await DownloadFile(asset.DownloadUrl, "./open_platform.zip");

        ProgressText.Header = "Extracting Open Platform";
        ProgressLoad.IsIndeterminate = true;
        ProgressLoad.ShowProgressText = false;

        if (Directory.Exists("./open_platform"))
        {
            Directory.Delete("./open_platform", true);
        }
        using(FileStream file = File.OpenRead("./open_platform.zip"))
        {
            await ZipFile.ExtractToDirectoryAsync(file, "./open_platform");
        }

        string source = "./open_platform" + "/" + osString.ToLower() + "64";

        foreach (var file in Directory.GetFiles(source))
        {
            if (Path.GetFileName(file).Contains("OpenPlatform"))
            {
                SafeCopy(file, destination + "/base");
            } else
            {
                SafeCopy(file, destination);
            }
        }
        //cleanup
        if (Directory.Exists("./open_platform"))
        {
            Directory.Delete("./open_platform", true);
        }
        if (File.Exists("./open_platform.zip"))
        {
            File.Delete("./open_platform.zip");
        }

    }

    private async Task ExtractKEXGUS(string source, string destination)
    {
        ProgressText.Header = "Extracting KEX's GUS";
        ProgressLoad.IsIndeterminate = true;
        ProgressLoad.ShowProgressText = false;

        using(FileStream file = File.OpenRead(source + "/Common.kpf"))
        {
            await ZipFile.ExtractToDirectoryAsync(file, "./kex_common");
        }
        await CopyDirectoryContents(destination + "/base/classicmusic", destination + "/base/dgguspats");
        Directory.Delete(destination + "/base/classicmusic", true);
        await CopyDirectoryContents("./kex_common/GUS", destination + "/base/classicmusic");
        Directory.Delete("./kex_common", true);
    }
    private void SafeCopy(string source, string destination)
    {
        if (File.Exists(source))
        {
            string fileName = Path.GetFileName(source);
            File.Copy(source, destination + "/" + fileName, true);
        }
    }

    private long CalculateFileSizes(string[] files)
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

    private async Task<int> SafeCopyWithProgress(string source, string destination, long writtenSize, long FileSize)
    {
        int finalBufferSize = 0;
        if (File.Exists(source))
        {
            string fileName = Path.GetFileName(source);
            using(FileStream destFile = File.OpenWrite(destination + "/" + fileName))
            {
                using (FileStream sourceFile = File.OpenRead(source))
                {
                    while (true) {
                        byte[] buffer = new byte[8 * 1024];
                        int bufferSize = await sourceFile.ReadAsync(buffer, 0, 8 * 1024);
                        if (bufferSize > 0) {
                            await destFile.WriteAsync(buffer, 0, bufferSize);
                            writtenSize += bufferSize;
                            finalBufferSize += bufferSize;
                            ProgressLoad.Value = ((writtenSize * 1.0) / FileSize) * 100;
                        } else
                        {
                            break;
                        }
                    }
                }
            }
        }
        return finalBufferSize;
    }
}