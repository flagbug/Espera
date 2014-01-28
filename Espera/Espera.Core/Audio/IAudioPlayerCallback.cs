using System;
using System.Threading.Tasks;

namespace Espera.Core.Audio
{
    public interface IAudioPlayerCallback
    {
        Func<TimeSpan> GetTime { set; }

        Func<float> GetVolume { set; }

        Func<Task> LoadRequest { set; }

        Uri Path { get; }

        Func<Task> PauseRequest { set; }

        Func<Task> PlayRequest { set; }

        Action<TimeSpan> SetTime { set; }

        Action<float> SetVolume { set; }

        Func<Task> StopRequest { set; }

        Task Finished();
    }
}