namespace UptimeDaddy.API.DTOs
{
    public class MqttPingPreviewResponseDto
    {
        public string RequestId { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public int StatusCode { get; set; }
        public long DnsLookupMs { get; set; }
        public long ConnectMs { get; set; }
        public long TlsHandshakeMs { get; set; }
        public long TimeToFirstByteMs { get; set; }
        public long TotalTimeMs { get; set; }
    }
}
