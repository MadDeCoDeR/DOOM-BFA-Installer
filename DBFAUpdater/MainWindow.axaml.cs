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
using DBFAUpdater.Utils;
using Avalonia.Platform.Storage;
using System.Reflection;
using Avalonia.Platform;

namespace DBFAUpdater;

public class StateMachine
{
    public string? Previous { get; set; }
    public required string Current { get; set; }
    public string? Next { get; set; }

    public Func<FormModel, bool>? Condition { get; set; }

    public Func<FormModel, int, bool>? LoopingCondition { get; set; }

    public Action<MainWindow, FormModel, int>? LoopingAction { get; set; }

    public int LoopingIndex = 0;
}
public partial class MainWindow : Window
{
    private readonly List<StateMachine> states = [
        new StateMachine { Previous = null, Current = "Welcome", Next = "Version", Condition = null, LoopingCondition = null, LoopingAction = null },
        new StateMachine { Previous = "Welcome", Current = "Version", Next = "Profile", Condition = null, LoopingCondition = null },
        new StateMachine { Previous = "Version", Current = "Profile", Next = "Edition", Condition = (context) => context.Version == VersionEnum.Beta, LoopingCondition = null, LoopingAction = null },
        new StateMachine { Previous = "Profile", Current = "Edition", Next = "Addon", Condition = null, LoopingCondition = null, LoopingAction = null },
        new StateMachine { Previous = "Edition", Current = "Addon", Next = "License", Condition = (context) => CheckAvailableAddons(context), LoopingCondition = null, LoopingAction = null },
        new StateMachine { Previous = "Addon", Current = "License", Next = "InstPath", Condition = null, LoopingCondition = (context, index) => AllLicensesShawn(context, index), LoopingAction = (window, context, index) => ShowLicense(window, context, index) },
        new StateMachine { Previous = "License", Current = "InstPath", Next = "Progress", Condition = null, LoopingCondition = null, LoopingAction = null },
        new StateMachine { Previous = "InstPath", Current = "Progress", Next = "End", Condition = null, LoopingCondition = null, LoopingAction = null },
        new StateMachine { Previous = null, Current = "End", Next = null, Condition = null, LoopingCondition = null, LoopingAction = null },

    ];

    private static readonly List<string> LicensePaths = [
        "Assets/BFA/COPYING.txt",
        "Assets/launcher/COPYING.txt",
        "Assets/Open_Platform/COPYING.txt"
    ];

    private static readonly List<string> LicenseHeaders = [
        "DOOM BFA License",
        "DBFA Launcher License",
        "Open Platform License"
    ];

    private static readonly List<string> SHA256s = [
        "B683AC1B1D3F0CA6B92111DB85FC77ECE9D5C034CE5461EB8A7C4ADD8E239A22", //DOOM 3: BFG Edition
        "6DAECF3E621756C8A77B3C3064ED5FB488AFE357A80B7C14BEF35B6811B073CE", //DOOM 3 re-release (2019)
    ];

    private static readonly SHA256 sHA256 = SHA256.Create();

    private readonly HttpClient httpClient;
    private StateMachine currentState;
    public MainWindow()
    {
        InitializeComponent();
        currentState = states[0];
        HttpClientHandler httpClientHandler = new HttpClientHandler();
        httpClientHandler.AllowAutoRedirect = true;
        httpClient = new HttpClient(httpClientHandler);
        httpClient.DefaultRequestHeaders.Add("User-Agent", "DBFAInstaller/1.0.0.0"); //GK: Put it on releases
    }

    private void Window_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext != null) {
#pragma warning disable CS8622 // Nullability of reference types in type of parameter doesn't match the target delegate (possibly because of nullability attributes).
            ((FormModel)DataContext).PropertyChanged += OnEditionChanged;
            ((FormModel)DataContext).PropertyChanged += OnProfileChanged;
            ((FormModel)DataContext).PropertyChanged += OnAddonSelected;
            ((FormModel)DataContext).PropertyChanged += OnVersionSelected;
#pragma warning restore CS8622 // Nullability of reference types in type of parameter doesn't match the target delegate (possibly because of nullability attributes).
        }
    }

    private async void Next_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        HandleState();
    }

    private async void Back_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        HandleState(false);
    }

    private async void FolderSelector_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        IReadOnlyList<IStorageFolder>? folder = await this.StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
        {
            AllowMultiple = false
        });
        string? mountPoint = ((Button?)sender)?.Name?.Replace("Button", "");
        if (!string.IsNullOrEmpty(mountPoint) && folder != null && folder.Count > 0)
        {

            FormModel? formModel = (FormModel?)DataContext;
            PropertyInfo? mountProperty = formModel?.GetType().GetProperties().Where(prop => prop.Name.Contains(mountPoint)).FirstOrDefault();
            if (mountProperty != null)
            {
                mountProperty.SetValue(formModel, folder[0].Path.LocalPath);
            }
        }
    }

    private async void HandleState(bool direction = true)
    {
        if (this.DataContext != null) {
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
            if (currentFrame == null || upcomingFrame == null)
            {
                return;
            }
            if (currentState.LoopingCondition != null)
            {
                currentState.LoopingIndex += direction ? 1 : -1;
            }

            if (currentState.LoopingCondition == null || !currentState.LoopingCondition(dataForm, currentState.LoopingIndex) || currentState.LoopingIndex == -1) {
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


                Back.IsVisible = upcomingState.Previous != null && (upcomingState.Current != "Progress");
                Back.IsEnabled = upcomingState.Previous != null && (upcomingState.Current != "Progress");
                Next.IsVisible = upcomingState.Current != "Progress";
                Next.IsEnabled = upcomingState.Previous != "Progress";
                Next.Content = upcomingState.Current == "End" ? "Close" : "Next";

                if (currentState.Current == "Addon" && !direction)
                {
                    ResetAddons();
                }
                currentState.LoopingIndex = !direction ? 0 : currentState.LoopingIndex - 1;
                currentState = upcomingState;

                if (currentState.Current == "Progress")
                {
                    await InstallMod();
                }

            }

            if (currentState.LoopingCondition != null )
            {
                if(currentState.LoopingCondition(dataForm, currentState.LoopingIndex) && currentState.LoopingAction != null)
                {
                    currentState.LoopingAction(this, dataForm, currentState.LoopingIndex);
                    
                }
                
            }
        }

    }

    private void OnEditionChanged(object sender, PropertyChangedEventArgs e)
    {
        if (this.DataContext != null) {
            FormModel dataModel = (FormModel)this.DataContext;
            if (e.PropertyName == "Edition")
            {
                Addon2.IsEnabled = dataModel.Edition == EditionEnum.Classic;
                Addon2.IsVisible = dataModel.Edition == EditionEnum.Classic;
                Addon3.IsVisible = dataModel.Edition == EditionEnum.Standard;
                Addon3.IsEnabled = dataModel.Edition == EditionEnum.Standard;
                Addon4.IsVisible = dataModel.Edition == EditionEnum.Standard;
                Addon4.IsEnabled = dataModel.Edition == EditionEnum.Standard;
                Addon5.IsVisible = dataModel.Edition == EditionEnum.Standard;
                Addon5.IsEnabled = dataModel.Edition == EditionEnum.Standard;
                Addon6.IsVisible = dataModel.Edition == EditionEnum.Standard;
                Addon6.IsEnabled = dataModel.Edition == EditionEnum.Standard;
                ClassicPathTitle.IsEnabled = dataModel.Edition == EditionEnum.Classic;
                ClassicPathTitle.IsVisible = dataModel.Edition == EditionEnum.Classic;
                ClassicPathTitle.Header = dataModel.Edition == EditionEnum.Classic ? "Select DOOM 1 + 2 installation Path" : ClassicPathTitle.Header;
                ClassicPath.IsEnabled = dataModel.Edition == EditionEnum.Classic;
                ClassicPath.IsVisible = dataModel.Edition == EditionEnum.Classic;
            }

        }
    }

    private void OnProfileChanged(object sender, PropertyChangedEventArgs e)
    {
        if (this.DataContext != null) {
            FormModel dataModel = ((FormModel)this.DataContext);
            if (e.PropertyName == "Profile")
            {
                Addon1.IsEnabled = dataModel.Profile == ProfileEnum.Retail || dataModel.Version == VersionEnum.Stable;
                Addon1.IsVisible = dataModel.Profile == ProfileEnum.Retail || dataModel.Version == VersionEnum.Stable;

            }
        }
    }

    private void OnAddonSelected(object sender, PropertyChangedEventArgs e)
    {
        if (this.DataContext != null) {
            FormModel dataModel = ((FormModel)this.DataContext);
            if (e.PropertyName == "Addon3" || e.PropertyName == "Addon4" || e.PropertyName == "Addon6")
            {
                bool isTheAddonSelected = dataModel.Addon3 || dataModel.Addon4 || dataModel.Addon6;
                ClassicPathTitle.IsEnabled = isTheAddonSelected;
                ClassicPathTitle.IsVisible = isTheAddonSelected;
                ClassicPathTitle.Header = isTheAddonSelected ? "Select Original DOOM 3 installation Path" : "Select DOOM 1 + 2 installation Path";
                ClassicPath.IsEnabled = isTheAddonSelected;
                ClassicPath.IsVisible = isTheAddonSelected;


            }
        }
    }

    private void OnVersionSelected(object sender, PropertyChangedEventArgs e)
    {
        if (this.DataContext != null) {
            FormModel dataModel = ((FormModel)this.DataContext);
            if (e.PropertyName == "Version")
            {
                Addon7.IsEnabled = dataModel.Version == VersionEnum.Beta;
                Addon7.IsVisible = dataModel.Version == VersionEnum.Beta;

            }
        }
    }

    private void ResetAddons()
    {
        if (this.DataContext != null) {
            FormModel dataModel = ((FormModel)this.DataContext);
            dataModel.Addon1 = false;
            dataModel.Addon2 = false;
            dataModel.Addon3 = false;
            dataModel.Addon4 = false;
            dataModel.Addon5 = false;
            dataModel.Addon6 = false;
            dataModel.Addon7 = false;
        }
    }

    private static bool CheckAvailableAddons(FormModel formModel)
    {
        bool isAddon1Active = formModel.Profile == ProfileEnum.Retail || formModel.Version == VersionEnum.Stable;
        bool isAddon2Active = formModel.Edition == EditionEnum.Classic;
        bool isAddons3456Active = formModel.Edition == EditionEnum.Standard;
        bool isAddon7Active = formModel.Version == VersionEnum.Beta;
        return isAddon1Active || isAddon2Active || isAddons3456Active || isAddon7Active;
    }

    private static bool AllLicensesShawn(FormModel formModel, int index)
    {
        int maxLicenses = 1;
        maxLicenses += formModel.Version == VersionEnum.Stable ? 1 : 0;
        maxLicenses += formModel.Version == VersionEnum.Beta && formModel.Addon7 ? 1 : 0;
        maxLicenses += formModel.Addon1 ? 1 : 0;

        return index < maxLicenses;
    }

    private static void ShowLicense(MainWindow mainWindow, FormModel formModel, int index)
    {
        int maxLicenses = 1;
        maxLicenses += formModel.Version == VersionEnum.Stable ? 1 : 0;
        maxLicenses += formModel.Version == VersionEnum.Beta && formModel.Addon7 ? 1 : 0;
        maxLicenses += formModel.Addon1 ? 1 : 0;
        if (index < maxLicenses)
        {
            string licensePath = LicensePaths[index];
            mainWindow.LicenseHeader.Text = LicenseHeaders[index];
            using (StreamReader licenseReader = new StreamReader(AssetLoader.Open(new Uri("avares://DBFAUpdater/" + licensePath)))) {
                mainWindow.LicenseText.Text = licenseReader.ReadToEnd();
            }
        }
    }

    private async Task InstallMod()
    {
        if (this.DataContext != null) {
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

            if (formModel.Addon3 || formModel.Addon4)
            {
                if (string.IsNullOrEmpty(formModel.ClassicPath)) {
                    await ShowErrorAndRollback("Please provide a path for the Original DOOM 3");
                    return;
                }

                if (!File.Exists(formModel.ClassicPath + "/d3xp/pak000.pk4"))
                {
                    await ShowErrorAndRollback("The Original DOOM 3 path doesn't have the ROE expansion installed");
                    return;
                }
            }

            if (formModel.Addon6)
            {
                if (string.IsNullOrEmpty(formModel.ClassicPath)) {
                    await ShowErrorAndRollback("Please provide a path for the Original DOOM 3");
                    return;
                }

                if (!File.Exists(formModel.ClassicPath + "/d3xp/pak001.pk4") || !File.Exists(formModel.ClassicPath + "/base/pak007.pk4"))
                {
                    await ShowErrorAndRollback("The Original DOOM 3 path doesn't have the ROE expansion with the latest updates installed");
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
                ICollection<GitRelease>? releases = JsonConvert.DeserializeObject<ICollection<GitRelease>>(await response.Content.ReadAsStringAsync());
                latestRelease = releases?.Where(rel => rel.Prerelease == (formModel.Version == VersionEnum.Beta)).OrderByDescending(rel => rel.PublishedAt).First();
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
                        asset = latestRelease?.Assets?.Where(ast => ast.Name != null && ast.Name.Contains(formModel.Edition.ToString())).FirstOrDefault();
                        break;
                    }
                case VersionEnum.Beta:
                    {
                        string debug = formModel.Profile.ToString().ToLower();
                        bool edition = formModel.Edition == EditionEnum.Classic;
                        asset = latestRelease?.Assets?.Where(ast => ast.Name != null &&  ast.Name.Contains(osString.ToLower()) && ast.Name.Contains(debug) && ast.Name.Contains("classic") == edition && !ast.Name.Contains("x86")).FirstOrDefault();
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
            await DownloadFile(asset.DownloadUrl ?? "", "./doom_bfa.zip");

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

            await Utilities.CopyDirectoryContents(source, Destination);
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
                await CopyWadFiles(formModel.ClassicPath ?? ".", formModel.MainPath);

            }

            if (formModel.Addon1 && (formModel.Profile == ProfileEnum.Retail || formModel.Version == VersionEnum.Stable))
            {
                await InstallOpenPlatform(formModel.MainPath);
            }

            if (formModel.Addon2 && formModel.Edition == EditionEnum.Classic)
            {
                await ExtractKEXGUS(formModel.ClassicPath ?? ".", formModel.MainPath);
            }

            if ((formModel.Addon3 || formModel.Addon4 || formModel.Addon5) && formModel.Edition == EditionEnum.Standard)
            {
                ProgressText.Header = "Downloading BFA extras";
                ProgressLoad.IsIndeterminate = false;
                ProgressLoad.ShowProgressText = true;
                await DownloadFile("https://api.github.com/repos/MadDeCoDeR/BFA-Assets/zipball/extras", "./bfa_extras.zip");
                if (Directory.Exists("./bfa_extras"))
                {
                    Directory.Delete("./bfa_extras", true);
                }
                using(FileStream file = File.OpenRead("./bfa_extras.zip"))
                {
                    await ZipFile.ExtractToDirectoryAsync(file, "./bfa_extras");
                }
                if (formModel.Addon3) {
                    await InstallErebus5Restored(formModel.ClassicPath ?? ".", formModel.MainPath);
                }
                if (formModel.Addon4)
                {
                    await InstallROEArcades(formModel.ClassicPath ?? ".", formModel.MainPath);
                }

                if (formModel.Addon5)
                {
                    await InstallLEArcade(formModel.MainPath);
                }

                if (Directory.Exists("./bfa_extras"))
                {
                    Directory.Delete("./bfa_extras", true);
                }

                if (Directory.Exists("./ogD3Assets"))
                {
                    Directory.Delete("./ogD3Assets", true);
                }

                if (File.Exists("./bfa_extras.zip"))
                {
                    File.Delete("./bfa_extras.zip");
                }

            }

            if (formModel.Addon6 && formModel.Edition == EditionEnum.Standard)
            {
                await InstallEFXFiles(formModel.ClassicPath ?? ".", formModel.MainPath);
                if (Directory.Exists("./ogD3Assets"))
                {
                    Directory.Delete("./ogD3Assets", true);
                }
            }

            if (formModel.Addon7 && formModel.Version == VersionEnum.Beta)
            {
                await InstallLauncher(formModel.MainPath);
            }

            this.HandleState();
        }
    }

/**
Addons logic
*/
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
        ICollection<GitRelease>? releases = JsonConvert.DeserializeObject<ICollection<GitRelease>>(await response.Content.ReadAsStringAsync());
        latestRelease = releases?.OrderByDescending(rel => rel.PublishedAt).First();
        i++;
        if (latestRelease == null)
        {
            await ShowError("Failed to Find the latest release");
            return;
        }
        GitReleaseAsset? asset = latestRelease?.Assets?.Where(ast => ast.Name != null && ast.Name.Contains("Steam")).FirstOrDefault();;

        if (asset == null)
        {
            await ShowError("Failed to find the right asset");
            return;
        }

        ProgressText.Header = "Downloading Open Platform";
        ProgressLoad.IsIndeterminate = false;
        ProgressLoad.ShowProgressText = true;
        await DownloadFile(asset.DownloadUrl ?? "", "./open_platform.zip");

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

        string opOSstring = osString.ToLower() != "linux" ? "Win" : "linux";
        string source = "./open_platform" + "/" + opOSstring + "64";

        foreach (var file in Directory.GetFiles(source))
        {
            if (Path.GetFileName(file).Contains("OpenPlatform"))
            {
                Utilities.SafeCopy(file, destination + "/base");
            } else
            {
                Utilities.SafeCopy(file, destination);
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

        await Utilities.SelectiveExtraction(source + "/Common.kpf", "./kex_common", "GUS");
        await Utilities.CopyDirectoryContents(destination + "/base/classicmusic", destination + "/base/dgguspat");
        Directory.Delete(destination + "/base/classicmusic", true);
        await Utilities.CopyDirectoryContents("./kex_common/GUS", destination + "/base/classicmusic");
        Directory.Delete("./kex_common", true);
    }
    
    private async Task InstallErebus5Restored(string source, string destination)
    {
        ProgressText.Header = "Restoring Erebus5";
        ProgressLoad.IsIndeterminate = true;
        ProgressLoad.ShowProgressText = false;

        await Utilities.SelectiveExtraction(source + "/d3xp/pak000.pk4", "./ogD3Assets", "def/monster_zombie_hazmat");
        if (!Directory.Exists(destination + "/base/def"))
        {
            Directory.CreateDirectory(destination + "/base/def");
        }
        Utilities.SafeCopy("./ogD3Assets/def/monster_zombie_hazmat.def", destination + "/base/def");
        string expectedFile = Utilities.FindFileInFolder("./bfa_extras", "zExtra_erebus5");
        Utilities.SafeCopy(expectedFile, destination + "/base/maps");
    }

    private async Task InstallROEArcades(string source, string destination)
    {
        ProgressText.Header = "Restoring ROE Arcades";
        ProgressLoad.IsIndeterminate = true;
        ProgressLoad.ShowProgressText = false;

        await Utilities.SelectiveExtraction(source + "/d3xp/pak000.pk4", "./ogD3Assets", "guis/assets/arcade");
        await Utilities.SelectiveExtraction(source + "/d3xp/pak000.pk4", "./ogD3Assets", "guis/assets/bearshoot");
        await Utilities.SelectiveExtraction(source + "/d3xp/pak000.pk4", "./ogD3Assets", "guis/assets/bustout");
        await Utilities.SelectiveExtraction(source + "/d3xp/pak000.pk4", "./ogD3Assets", "guis/GameBustOut");
        await Utilities.SelectiveExtraction(source + "/d3xp/pak000.pk4", "./ogD3Assets", "guis/GameSSD");
        await Utilities.SelectiveExtraction(source + "/d3xp/pak000.pk4", "./ogD3Assets", "guis/GameBearShoot");
        await Utilities.SelectiveExtraction(source + "/d3xp/pak000.pk4", "./ogD3Assets", "materials/GameBearShoot");
        await Utilities.SelectiveExtraction(source + "/d3xp/pak000.pk4", "./ogD3Assets", "materials/GameSSD");
        await Utilities.SelectiveExtraction(source + "/d3xp/pak000.pk4", "./ogD3Assets", "models/mapobjects/arcade_machine");
        await Utilities.SelectiveExtraction(source + "/d3xp/pak000.pk4", "./ogD3Assets", "newpdas/arcade");
        await Utilities.SelectiveExtraction(source + "/d3xp/pak000.pk4", "./ogD3Assets", "sound/arcade_machines");
        await Utilities.SelectiveExtraction(source + "/d3xp/pak000.pk4", "./ogD3Assets", "sound/arcade");
        await Utilities.SelectiveExtraction(source + "/d3xp/pak000.pk4", "./ogD3Assets", "textures/particles/fball2_strip");
        await Utilities.SelectiveExtraction(source + "/d3xp/pak000.pk4", "./ogD3Assets", "textures/particles/flame2_strip");
        await Utilities.SelectiveExtraction(source + "/d3xp/pak000.pk4", "./ogD3Assets", "ui/assets/crosshair");
        await Utilities.CopyDirectoryContents("./ogD3Assets", destination + "/base");
        string expectedFile = Utilities.FindFileInFolder("./bfa_extras", "zExtra_ROE_arcades");
        Utilities.SafeCopy(expectedFile, destination + "/base/maps");
    }

    private async Task InstallLEArcade(string destination)
    {
        ProgressText.Header = "Installing ROE Arcade";
        ProgressLoad.IsIndeterminate = true;
        ProgressLoad.ShowProgressText = false;
        string expectedFile = Utilities.FindFileInFolder("./bfa_extras", "zExtra_le_arcade");
        Utilities.SafeCopy(expectedFile, destination + "/base_BFG/maps");
    }

    private async Task InstallEFXFiles(string source, string destination)
    {
        ProgressText.Header = "Copying EFX files";
        ProgressLoad.IsIndeterminate = true;
        ProgressLoad.ShowProgressText = false;

        await Utilities.SelectiveExtraction(source + "/base/pak007.pk4", "./ogD3Assets", "efxs");
        await Utilities.SelectiveExtraction(source + "/d3xp/pak001.pk4", "./ogD3Assets", "efxs");
        await Utilities.CopyDirectoryContents("./ogD3Assets", destination + "/base");
    }

    private async Task InstallLauncher(string destination)
    {
        OperatingSystem os = Environment.OSVersion;
        string osString = os.Platform == PlatformID.Win32NT ? "Windows" : "Linux";
        //First get available releases of Open Platform
        GitRelease? latestRelease = null;
        int i = 1;
        ProgressText.Header = "Checking Latest Release";
        ProgressLoad.IsIndeterminate = true;
        ProgressLoad.ShowProgressText = false;
        HttpResponseMessage response = await httpClient.GetAsync("https://api.github.com/repos/MadDeCoDeR/CRBDL/releases?page=" + i);
        ICollection<GitRelease>? releases = JsonConvert.DeserializeObject<ICollection<GitRelease>>(await response.Content.ReadAsStringAsync());
        latestRelease = releases?.OrderByDescending(rel => rel.PublishedAt).First();
        i++;
        if (latestRelease == null)
        {
            await ShowError("Failed to Find the latest release");
            return;
        }
        GitReleaseAsset? asset = latestRelease?.Assets?.Where(ast => osString == "Linux" ? ast.Name != null && ast.Name.Contains(".AppImage") : true).FirstOrDefault();

        if (asset == null)
        {
            await ShowError("Failed to find the right asset");
            return;
        }

        ProgressText.Header = "Downloading Launcher";
        ProgressLoad.IsIndeterminate = false;
        ProgressLoad.ShowProgressText = true;
        await DownloadFile(asset.DownloadUrl ?? "", osString == "Linux" ? "DBFAL-x86_64.AppImage": "./launcher.zip");

        if (osString == "Windows") {
            ProgressText.Header = "Extracting Launcher";
            ProgressLoad.IsIndeterminate = true;
            ProgressLoad.ShowProgressText = false;

            if (Directory.Exists("./launcher"))
            {
                Directory.Delete("./launcher", true);
            }
            using(FileStream file = File.OpenRead("./launcher.zip"))
            {
                await ZipFile.ExtractToDirectoryAsync(file, "./launcher");
            }

        }

        string source = osString == "Linux" ? "./DBFAL-x86_64.AppImage" : "./launcher/DBFAL-windows-x64";

        if (!source.EndsWith(".AppImage")) {
            await Utilities.CopyDirectoryContents(source, destination);
            //cleanup
            if (Directory.Exists("./launcher"))
            {
                Directory.Delete("./launcher", true);
            }
            if (File.Exists("./launcher.zip"))
            {
                File.Delete("./launcher.zip");
            }
        } else
        {
            File.Move(source, destination + "/DBFAL-x86_64.AppImage", true);
        }

    }
/**
Bound to UI utilities
*/
    private async Task ShowError(string Message)
    {
        IMsBox<ButtonResult> messageBox = MessageBoxManager.GetMessageBoxStandard("Error", Message, ButtonEnum.Ok);
        ButtonResult result = await messageBox.ShowAsync();
        if (result == ButtonResult.Ok)
        {
            this.Close();
        }
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

        if (Url == "")
        {
            await ShowError("The download Url is empty");
            return;
        }
        using (FileStream file = File.OpenWrite(Path)) {
            using (HttpResponseMessage downloadResponse = await httpClient.GetAsync(Url, HttpCompletionOption.ResponseHeadersRead)) {

                long? contentLength = downloadResponse.Content.Headers.ContentLength;
                ProgressLoad.IsIndeterminate = contentLength == null;
                ProgressLoad.ShowProgressText = contentLength != null;
                using (Stream downloadStream = await downloadResponse.Content.ReadAsStreamAsync())
                {
                    int offset = 0;
                    while (true) {
                        byte[] buffer = new byte[ 8 * (1024 ^ 2)];
                        int tmpOffset = await downloadStream.ReadAsync(buffer, 0, 8 * (1024 ^ 2));
                        if (tmpOffset > 0) {
                            await file.WriteAsync(buffer, 0, tmpOffset);
                            offset += tmpOffset;
                            if (contentLength != null) {
                                ProgressLoad.Value = ((double)((offset * 1.0) / contentLength) * 100);
                            }
                            
                        } else
                        {
                            break;
                        }
                    }
                }
            }
        }
    }

    private async Task CopyWadFiles(string source, string destination)
    {
        string masterSource = "";
        if (Directory.Exists(Directory.GetParent(source)?.ToString() + "/base/master/wads"))
        {
            masterSource = Directory.GetParent(source)?.ToString() + "/base/master/wads";
        } else if (Directory.Exists(source + "/dosdoom/base/master/wads"))
        {
            masterSource = source + "/dosdoom/base/master/wads";
        }
        long totalFileSize = Utilities.CalculateFileSizes([
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

    
}