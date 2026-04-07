using System.Text.Json.Serialization;

namespace UptimeDaddy.API.DTOs
{
    public class MqttFaviconUpdateDto
    {
        [JsonPropertyName("websiteId")]
        public long WebsiteId { get; set; }

        [JsonPropertyName("path")]
        public string Path { get; set; } = string.Empty;

        [JsonPropertyName("icon")]
        public string Icon { get; set; } = string.Empty;
    }
}