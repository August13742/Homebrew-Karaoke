using System.Collections.Generic;
using System.Text.Json.Serialization;

public class LyricData
{
    [JsonPropertyName("version")]
    public string Version { get; set; }

    [JsonPropertyName("metadata")]
    public LyricMetadata Metadata { get; set; }

    [JsonPropertyName("lines")]
    public List<LyricLine> Lines { get; set; }

    [JsonPropertyName("words")]
    public List<LyricWord> Words { get; set; }
}

public class LyricWord
{
    [JsonPropertyName("text")]
    public string Text { get; set; }

    [JsonPropertyName("start")]
    public double Start { get; set; }

    [JsonPropertyName("end")]
    public double End { get; set; }

    [JsonPropertyName("line_id")]
    public int LineId { get; set; }
}

public class LyricMetadata
{
    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("duration_seconds")]
    public double DurationSeconds { get; set; }
}

public class LyricLine
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; }

    [JsonPropertyName("start")]
    public double Start { get; set; }

    [JsonPropertyName("end")]
    public double End { get; set; }
}
