using System;

namespace Espera.Core
{
    public class SoundCloudSong : Song
    {
        private readonly string playbackPath;
        private User user;

        public SoundCloudSong()
            : base(string.Empty, TimeSpan.Zero)
        {
        }

        /// <summary>
        ///     Constructor used in the library deserializer.
        /// </summary>
        public SoundCloudSong(string originalPath, string playbackPath)
            : base(originalPath, TimeSpan.Zero)
        {
            this.playbackPath = playbackPath;
        }

        [JsonProperty("artwork_url")] public Uri ArtworkUrl { get; set; }

        public string Description { get; set; }

        [JsonProperty("download_url")] public Uri DownloadUrl { get; set; }

        [JsonProperty("duration")]
        public int DurationMilliseconds
        {
            get => (int)Duration.TotalMilliseconds;
            set => Duration = TimeSpan.FromMilliseconds(value);
        }

        public int Id { get; set; }

        [JsonProperty("downloadable")] public bool IsDownloadable { get; set; }

        [JsonProperty("streamable")] public bool IsStreamable { get; set; }

        public override bool IsVideo => false;

        public override NetworkSongSource NetworkSongSource => NetworkSongSource.SoundCloud;

        [JsonProperty("permalink_url")]
        public Uri PermaLinkUrl
        {
            get => new Uri(OriginalPath);
            set => OriginalPath = value.ToString();
        }

        /// <remarks>For whatever reasons, the SoundCloud API can return null for this.</remarks>
        [JsonProperty("playback_count")]
        public int? PlaybackCount { get; set; }

        public override string PlaybackPath
        {
            get
            {
                // If we have assigned a playback path through library deserialization, return it.
                if (!string.IsNullOrEmpty(playbackPath)) return playbackPath;

                // Prioritize a downloadable version, it has probably a higher bitrate and therefore
                // better audio quality
                if (IsDownloadable) return DownloadUrl.ToString();

                if (IsStreamable) return StreamUrl.ToString();

                throw new InvalidOperationException("Somehow we couldn't obtain a SoundCloud streaming path");
            }
        }

        [JsonProperty("stream_url")] public Uri StreamUrl { get; set; }

        public User User
        {
            get => user;
            set
            {
                user = value;
                Artist = value == null ? string.Empty : value.Username;
            }
        }
    }

    public class User
    {
        public string Username { get; set; }
    }
}