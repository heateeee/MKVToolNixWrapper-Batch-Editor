using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MKVToolNixWrapper
{
    public class cProperties
    {
        [JsonPropertyName("uid")]
        public double Uid { get; set; }
    }

    public class cAttachment
    {
        [JsonPropertyName("content_type")]
        public string? Content_type { get; set; }
        [JsonPropertyName("description")]
        public string? Description { get; set; }
        [JsonPropertyName("file_name")]
        public string? File_Name { get; set; }
        [JsonPropertyName("id")]
        public double Id { get; set; }
        [JsonPropertyName("properties")]
        public cProperties? Properties { get; set; }
        [JsonPropertyName("size")]
        public double Size { get; set; }
    }

    public class cChapter
    {
        [JsonPropertyName("num_entries")]
        public double Num_entries { get; set; }
    }

    public class cContainerProperties
    {
        [JsonPropertyName("container_type")]
        public double Container_type { get; set; }
        [JsonPropertyName("data_local")]
        public string? Date_local { get; set; }
        [JsonPropertyName("date_utc")]
        public string? Date_utc { get; set; }
        [JsonPropertyName("duration")]
        public long Duration { get; set; }
        [JsonPropertyName("is_providing_timestamps")]
        public bool Is_providing_timestamps { get; set; }
        [JsonPropertyName("muxing_application")]
        public string? Muxing_application { get; set; }
        [JsonPropertyName("segment_uid")]
        public string? Segment_uid { get; set; }
        [JsonPropertyName("title")]
        public string? Title { get; set; }
        [JsonPropertyName("writing_application")]
        public string? Writing_application { get; set; }
    }

    public class cContainer
    {
        [JsonPropertyName("properties")]
        public cContainerProperties? Properties { get; set; }
        [JsonPropertyName("recognized")]
        public bool Recognized { get; set; }
        [JsonPropertyName("supported")]
        public bool Supported { get; set; }
        [JsonPropertyName("type")]
        public string? Type { get; set; }
    }

    public class cProperties2
    {
        [JsonPropertyName("codec_id")]
        public string? Codec_id { get; set; }
        [JsonPropertyName("codec_private_data")]
        public string? Codec_private_data { get; set; }
        [JsonPropertyName("codec_private_length")]
        public double? Codec_private_length { get; set; }
        [JsonPropertyName("default_location")]
        public double? Default_duration { get; set; }
        [JsonPropertyName("default_track")]
        public bool? Default_Track { get; set; }
        [JsonPropertyName("enabled_track")]
        public bool? Enabled_track { get; set; }
        [JsonPropertyName("forced_track")]
        public bool? Forced_Track { get; set; }
        [JsonPropertyName("language")]
        public string? Language { get; set; }
        [JsonPropertyName("minimum_timestamp")]
        public double Minimum_timestamp { get; set; }
        [JsonPropertyName("number")]
        public double Number { get; set; }
        [JsonPropertyName("packetizer")]
        public string? Packetizer { get; set; }
        [JsonPropertyName("pixel_dimensions")]
        public string? Pixel_dimensions { get; set; }
        [JsonPropertyName("uid")]
        public double Uid { get; set; }
        [JsonPropertyName("track_name")]
        public string? Track_Name { get; set; }
    }

    public class cTrack
    {
        [JsonPropertyName("codec")]
        public string? Codec { get; set; }
        [JsonPropertyName("id")]
        public double Id { get; set; }
        [JsonPropertyName("properties")]
        public cProperties2? Properties { get; set; }
        [JsonPropertyName("type")]
        public string? Type { get; set; }
    }

    public class cRootObject
    {
        [JsonPropertyName("attachments")]
        public List<cAttachment> Attachments { get; set; } = [];
        [JsonPropertyName("chapters")]
        public List<cChapter> Chapters { get; set; } = [];
        [JsonPropertyName("container")]
        public cContainer? Container { get; set; }
        [JsonPropertyName("errors")]
        public List<object> Errors { get; set; } = [];
        [JsonPropertyName("file_name")]
        public string? File_name { get; set; }
        [JsonPropertyName("global_tags")]
        public List<object> Global_tags { get; set; } = [];
        [JsonPropertyName("identification_format_version")]
        public double Identification_format_version { get; set; }
        [JsonPropertyName("track_tags")]
        public List<object> Track_tags { get; set; } = [];
        [JsonPropertyName("tracks")]
        public List<cTrack> Tracks { get; set; } = [];
    }
}