using System;

namespace Espera.Core
{
    /// <summary>
    /// The interface through which the user interface video player can interact.
    /// </summary>
    public interface IVideoPlayerCallback
    {
        Func<TimeSpan> GetTime { set; }

        Action LoadRequest { set; }

        Action PauseRequest { set; }

        Action PlayRequest { set; }

        Action<TimeSpan> SetTime { set; }

        Action StopRequest { set; }

        Uri VideoUrl { get; }

        Action<float> VolumeChangeRequest { set; }

        void Finished();
    }
}