using System.Text.Json.Serialization;

namespace Challenge.src.Domain
{    class Solution(Options options, List<Actions> actions)
    {
        [JsonPropertyName("options")]
        public Options Options { get; init; } = options;
        
        [JsonPropertyName("actions")]
        public List<Actions> Actions { get; init; } = actions;
    }
}
