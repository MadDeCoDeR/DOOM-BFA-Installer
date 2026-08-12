using CommunityToolkit.Mvvm.ComponentModel;

namespace DBFAUpdater;

public enum VersionEnum
{
    Stable,
    Beta
}

public enum ProfileEnum
{
    Retail,
    Debug
}

public enum EditionEnum
{
    Standard,
    Classic
}
public partial class FormModel : ObservableObject
{
    [ObservableProperty]
    public VersionEnum _version;

    [ObservableProperty]
    public ProfileEnum _profile;

    [ObservableProperty]
    public EditionEnum _edition;

    [ObservableProperty]
    public bool _addon1;

    [ObservableProperty]
    public bool _addon2;

    [ObservableProperty]
    public bool _addon3;

    [ObservableProperty]
    public bool _addon4;

    [ObservableProperty]
    public bool _addon5;

    [ObservableProperty]
    public bool _addon6;

    [ObservableProperty]
    public bool _addon7;

    [ObservableProperty]
    public string? _mainPath;

    [ObservableProperty]
    public string? _classicPath;

}