using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Challenge.src.Domain
{    class Solution(Options options, List<Action> actions)
    {
        [JsonPropertyName("options")]
        public Options Options { get; init; } = options;
        
        [JsonPropertyName("actions")]
        public List<Action> Actions { get; init; } = actions;
    }
}
