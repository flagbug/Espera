using Espera.Network;
using Newtonsoft.Json;
using System;

namespace Espera.Core
{
    public class SoundCloudSong : Song
    {
        private User user;

        public SoundCloudSong()
            : base(String.Empty, TimeSpan.Zero)
        { }

        [JsonProperty("artwork_url")]
        public Uri ArtworkUrl { get; set; }

        public string Description { get; set; }

        [JsonProperty("duration")]
        public int DurationMilliseconds
        {
            get { return (int)this.Duration.TotalMilliseconds; }
            set { this.Duration = TimeSpan.FromMilliseconds(value); }
        }

        public int Id { get; set; }

        [JsonProperty("streamable")]
        public bool IsStreamable { get; set; }

        public override bool IsVideo
        {
            get { return false; }
        }

        public override NetworkSongSource NetworkSongSource
        {
            get { return NetworkSongSource.Youtube; }
        }

        [JsonProperty("permalink_url")]
        public Uri PermaLinkUrl
        {
            get { return new Uri(this.OriginalPath); }
            set { this.OriginalPath = value.ToString(); }
        }

        [JsonProperty("playback_count")]
        public int PlaybackCount { get; set; }

        [JsonProperty("stream_url")]
        public Uri StreamUrl
        {
            get { return new Uri(this.PlaybackPath); }
            set { this.PlaybackPath = value.ToString(); }
        }

        public User User
        {
            get { return this.user; }
            set
            {
                this.user = value;
                this.Artist = value.Username;
            }
        }
    }

    public class User
    {
        public string Username { get; set; }
    }
}