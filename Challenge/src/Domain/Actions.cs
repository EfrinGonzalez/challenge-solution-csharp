using System.Text.Json.Serialization;

namespace Challenge.src.Domain;

/// <summary>
/// Action is a json-friendly representation of an action.
/// </summary>
/// <param name="timestamp">action timestamp</param>
/// <param name="id">order id</param>
/// <param name="action">place, move, pickup or discard</param>
/// <param name="target">heater, cooler or shelf. Target is the destination for move</param>
public class Actions(DateTime timestamp, string id, string action, string target) {
 

    //Actions
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; init; } = (long)timestamp.Subtract(DateTime.UnixEpoch).TotalMicroseconds;
    
    [JsonPropertyName("id")]
    public string Id { get; init; } = id;
   
    [JsonPropertyName("action")]
    public string Action { get; init; } = action;
   
    [JsonPropertyName("target")]
    public string Target { get; init; } = target;
};
