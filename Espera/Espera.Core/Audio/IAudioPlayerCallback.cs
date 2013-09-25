using System;

namespace Espera.Core.Audio
{
    public interface IAudioPlayerCallback
    {
        Func<TimeSpan> GetTime { set; }

        Func<float> GetVolume { set; }

        Action LoadRequest { set; }

        Uri Path { get; }

        Action PauseRequest { set; }

        Action PlayRequest { set; }

        Action<TimeSpan> SetTime { set; }

        Action<float> SetVolume { set; }

        Action StopRequest { set; }

        void Finished();
    }
}