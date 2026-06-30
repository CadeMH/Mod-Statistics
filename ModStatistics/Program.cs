using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ModStatistics;
using ModStatistics.Platforms;

string gistId = Environment.GetEnvironmentVariable("GIST_ID") ?? "";
string githubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN") ?? "";
string steamApiKey = Environment.GetEnvironmentVariable("STEAM_API_KEY") ?? "";
string nexusApiKey = Environment.GetEnvironmentVariable("NEXUS_API_KEY") ?? "";

using HttpClient client = new HttpClient();
client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Compatible; ModStats/1.0)");

bool getThunderstore = true;
bool getSteam = false;
bool getNexus = false;

Console.WriteLine("/// --- /// MOD STATISTICS /// --- ///");

try
{
    var thunderstoreTeams = Thunderstore.GetThunderstoreMods();
    var steamMods = SteamWorkshop.GetSteamWorkshop();
    var nexusMods = NexusMods.GetNexusMods();

    var packageData = new Dictionary<string, object>();

    ulong totalDownloads = 0;
    ulong totalRatings = 0;
    ulong totalRatingsBad = 0;

    async IAsyncEnumerable<JsonElement> GetAllPages(string initialUrl)
    {
        string url = initialUrl;
        while (url != null)
        {
            var response = await client.GetStringAsync(url);
            using var doc = JsonDocument.Parse(response);

            foreach (var item in doc.RootElement.GetProperty("results").EnumerateArray())
                yield return item.Clone();

            url = doc.RootElement.GetProperty("next").GetString();
        }
    }

    if (getThunderstore)
    {
        foreach (var entry in thunderstoreTeams)
        {
            var baseURL = "https://thunderstore.io/api/cyberstorm";
            foreach (var community in entry.Value.communities)
            {
                var url = $"{baseURL}/listing/{community}/{entry.Key}";
                //var response = await client.GetStringAsync(url);
                //using var doc = JsonDocument.Parse(response);

                using var response = await client.GetStreamAsync($"{baseURL}/community/{community}");
                using var document = await JsonDocument.ParseAsync(response);

                string communityName = document.RootElement.GetProperty("name").GetString();


                await foreach(var item in GetAllPages(url))
                {
                    var versionRegex = new Regex(@"(?<=-)([\d\.]+)(?=\.png)");

                    var version = versionRegex.Match(item.GetProperty("icon_url").GetString() ?? "");
                    string extractedVersion = version.Success ? version.Value : "1.0.0";

                    var ratings = item.GetProperty("rating_count").GetUInt64();
                    var downloads = item.GetProperty("download_count").GetUInt64();

                    var identifier = $"{entry.Key}-{item.GetProperty("name").GetString()}";

                    var mod = new Mod
                    {
                        name = item.GetProperty("name").GetString().Replace("_", " ") ?? "null",
                        Downloads = downloads,
                        Ratings = ratings,
                        Version = extractedVersion,
                        community = community,
                        community_name = communityName,
                        link = $"https://thunderstore.io/c/{community}/p/{entry.Key}/{item.GetProperty("name").GetString() ?? "null"}",
                        platform = "Thunderstore",
                        popular = entry.Value.popular_identifiers.Contains(identifier) ? "True" : "False",
                        icon = item.GetProperty("icon_url").GetString() ?? "null"
                    };

                    totalDownloads += downloads;
                    totalRatings += ratings;

                    Console.WriteLine($"[Thunderstore]: Processed {identifier} || Downloads: {mod.Downloads} || Ratings: {mod.Ratings} || Popular: {mod.popular}");

                    packageData[item.GetProperty("name").GetString()] = mod;
                }
            }
        }
    }

    if (!string.IsNullOrEmpty(steamApiKey) && steamMods.Count > 0 && getSteam)
    {
        var steamIds = steamMods.Keys.ToList();
        var steamUrl = "https://api.steampowered.com/IPublishedFileService/GetDetails/v1/?key=" + steamApiKey + "&includevotes=true";

        for (int i = 0; i < steamIds.Count; i++) steamUrl += $"&publishedfileids[{i}]={steamIds[i]}";

        var response = await client.GetStringAsync(steamUrl);
        using var doc = JsonDocument.Parse(response);
        var items = doc.RootElement.GetProperty("response").GetProperty("publishedfiledetails");

        foreach (var item in items.EnumerateArray())
        {
            string id = item.GetProperty("publishedfileid").GetString()!;
            string title = item.GetProperty("title").GetString()!;
            var voteData = item.TryGetProperty("vote_data", out var v) ? v : default;

            if (steamMods.TryGetValue(id, out var mod))
            {
                mod.name = title;
                mod.Downloads = item.TryGetProperty("lifetime_subscriptions", out var d) ? d.GetUInt64() : 0;
                mod.PositiveRatings = voteData.ValueKind != JsonValueKind.Undefined ? voteData.GetProperty("votes_up").GetUInt64() : 0;
                mod.NegativeRatings = voteData.ValueKind != JsonValueKind.Undefined ? voteData.GetProperty("votes_down").GetUInt64() : 0;

                totalDownloads += mod.Downloads;
                totalRatings += mod.PositiveRatings;
                totalRatingsBad += mod.NegativeRatings;

                packageData[$"Steam - {title}"] = mod;
                Console.WriteLine($"[Steam] {title} || Subs: {mod.Downloads} || +{mod.PositiveRatings} / -{mod.NegativeRatings}");
            }
        }
    }

    if (getNexus && !string.IsNullOrEmpty(nexusApiKey))
    {
        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.Add("apikey", nexusApiKey);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Compatible; ModStats/1.0)");

        foreach (var entry in nexusMods)
        {
            Console.WriteLine(entry.Value.community, entry.Value.nexusModId);
            string url = $"https://api.nexusmods.com/v1/games/{entry.Value.community}/mods/{entry.Value.nexusModId}.json";
            var response = await client.GetStringAsync(url);
            using var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;

            entry.Value.Downloads = root.GetProperty("mod_downloads").GetUInt64();
            entry.Value.Ratings = root.GetProperty("endorsement_count").GetUInt64();
            entry.Value.Version = root.GetProperty("version").GetString() ?? "1.0.0";
            entry.Value.icon = root.GetProperty("picture_url").GetString() ?? "null";

            totalDownloads += entry.Value.Downloads;
            totalRatings += entry.Value.Ratings;

            packageData[$"Nexus - {entry.Key}"] = entry.Value;
            Console.WriteLine($"[Nexus] {entry.Key} || Downloads: {entry.Value.Downloads} || Endorsements: {entry.Value.Ratings}");
        }
    }

    var finalData = new Dictionary<string, object>
    {
        { "total_downloads", totalDownloads },
        { "total_ratings", totalRatings },
        { "total_ratings_bad", totalRatingsBad },
        { "last_checked", DateTimeOffset.UtcNow.ToUnixTimeSeconds() }
    };

    foreach (var pkg in packageData) finalData.Add(pkg.Key, pkg.Value);

    var _gistPayload = new { files = new { prev_json = new { content = JsonSerializer.Serialize(finalData, new JsonSerializerOptions { WriteIndented = true }) } } };
    Console.WriteLine(_gistPayload);

    if (!string.IsNullOrEmpty(githubToken))
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", githubToken);

        var gistFiles = new Dictionary<string, object>
        {
            { "mods.json", new { content = JsonSerializer.Serialize(finalData, new JsonSerializerOptions { WriteIndented = true }) } }
        };

        var gistPayload = new { files = gistFiles };
        var patchContent = new StringContent(JsonSerializer.Serialize(gistPayload), Encoding.UTF8, "application/json");

        var result = await client.PatchAsync($"https://api.github.com/gists/{gistId}", patchContent);
        Console.WriteLine(result.IsSuccessStatusCode ? "Success! Gist Updated" : $"Error: Gist Failed: {result.StatusCode}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    Environment.Exit(1);
}
