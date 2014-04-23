using System;

namespace Espera.Core
{
    /// <summary>
    /// The exception that is thrown if the video or audio download from YouTube fails.
    /// </summary>
    public class YoutubeDownloadException : Exception
    {
        public YoutubeDownloadException(string message, Exception innerException)
            : base(message, innerException)
        { }
    }
}