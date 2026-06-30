using System.Text.Json.Serialization;

namespace ModStatistics
{
    public class Mod
    {
        public string name { get; set; } = "";
        [JsonPropertyName("downloads")]
        public ulong Downloads { get; set; }
        [JsonPropertyName("ratings")]
        public ulong Ratings { get; set; }
        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.0.0";
        public string community { get; set; } = "null";
        public string community_name { get; set; } = "null";
        public string link { get; set; } = "null";
        public string platform { get; set; } = "null";
        public string popular { get; set; } = "False";
        public string icon { get; set; } = "null";

        [JsonPropertyName("positive ratings")]
        public ulong PositiveRatings { get; set; }
        [JsonPropertyName("negative ratings")]
        public ulong NegativeRatings { get; set; }

        public string nexusModId { get; set; }
    }

    public class ThunderstoreJSONContent
    {
        public List<string> communities { get; set; } = new();
        public List<string> popular_identifiers { get; set; } = new();
    }

    public class ThunderstoreJSON
    {
        public Dictionary<string, ThunderstoreJSONContent> Content { get; set; } = new();
    }

    public class GistData
    {
        public ulong total_downloads { get; set; }
        public ulong total_ratings { get; set; }
        public ulong total_ratings_bad { get; set; }
        public double last_checked { get; set; }
        public Dictionary<string, object> Mods { get; set; } = new();
    }
}