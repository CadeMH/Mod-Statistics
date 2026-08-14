using System.Reflection;
using System.Text.Json;

namespace ModStatistics.Platforms
{
    public class Discord
    {
        public static Dictionary<string, DiscordServer> GetDiscordServers()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(r => r.EndsWith("discord.json"));

            if (string.IsNullOrEmpty(resourceName))
            {
                throw new FileNotFoundException("Could not find embedded discord.json");
            }

            using (Stream? stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null) return new Dictionary<string, DiscordServer>();

                using (StreamReader reader = new StreamReader(stream))
                {
                    string jsonContent = reader.ReadToEnd();
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                    var rawData = JsonSerializer.Deserialize<Dictionary<string, DiscordServer>>(jsonContent, options)
                               ?? new Dictionary<string, DiscordServer>();

                    foreach (var entry in rawData)
                    {
                        entry.Value.Name = entry.Key;
                        entry.Value.Platform = "Discord";
                        entry.Value.Link = $"https://discord.gg/{entry.Value.InviteLink}";
                    }

                    return rawData;
                }
            }
        }
    }
}
