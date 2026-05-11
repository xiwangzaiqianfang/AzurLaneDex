using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AzurLaneDex.Models;

public class ShipState
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("owned")]
    public bool Owned { get; set; }

    [JsonPropertyName("breakthrough")]
    public int Breakthrough { get; set; }

    [JsonPropertyName("remodeled")]
    public bool Remodeled { get; set; }

    [JsonPropertyName("oath")]
    public bool Oath { get; set; }

    [JsonPropertyName("level_120")]
    public bool Level120 { get; set; }

    [JsonPropertyName("special_gear_obtained")]
    public bool SpecialGearObtained { get; set; }
}

public class StateList
{
    [JsonPropertyName("states")]
    public List<ShipState> States { get; set; } = new();
}