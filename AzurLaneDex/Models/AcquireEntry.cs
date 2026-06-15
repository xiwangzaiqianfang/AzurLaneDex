using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AzurLaneDex.Models
{
    public class AcquireEntry
    {
        [JsonPropertyName("tag")]
        public string Tag { get; set; } = "";

        [JsonPropertyName("params")]
        public List<string> Parameters { get; set; } = new();

        [JsonPropertyName("custom_text")]
        public LocalizedString CustomText { get; set; } = new();  // 改为 LocalizedString
    }
}