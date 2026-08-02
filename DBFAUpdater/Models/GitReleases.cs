
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace DBFAUpdater.Models;

public class GitRelease
{
    [JsonProperty("prerelease")]
    public bool? Prerelease {get; set;}

    [JsonProperty("published_at")]
    public DateTime? PublishedAt {get; set;}

    [JsonProperty("assets")]
    public List<GitReleaseAsset>? Assets {get; set;}

}


public class GitReleaseAsset
{
    [JsonProperty("name")]
    public string? Name {get; set;}

    [JsonProperty("browser_download_url")]
    public string? DownloadUrl {get; set;}
}