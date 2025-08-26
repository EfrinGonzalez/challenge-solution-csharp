using System.Text.Json.Serialization;


namespace Challenge.src.Domain
{
    class Options(TimeSpan rate, TimeSpan min, TimeSpan max)
    {
        [JsonPropertyName("rate")]
        public long Rate { get; init; } = (long)rate.TotalMicroseconds;
        [JsonPropertyName("min")]
        public long Min { get; init; } = (long)min.TotalMicroseconds;
        [JsonPropertyName("max")]
        public long Max { get; init; } = (long)max.TotalMicroseconds;
    };
}
