using System;

namespace Espera.Core
{
    /// <summary>
    /// The interface through which the user interface video player can interact.
    /// </summary>
    public interface IVideoPlayerCallback
    {
        Func<TimeSpan> GetTime { set; }

        Func<float> GetVolume { set; }

        Action LoadRequest { set; }

        Action PauseRequest { set; }

        Action PlayRequest { set; }

        Action<TimeSpan> SetTime { set; }

        Action<float> SetVolume { set; }

        Action StopRequest { set; }

        Uri VideoUrl { get; }

        void Finished();
    }
}