using System;
using System.Threading.Tasks;
using Espera.Core;
using Espera.View.ViewModels;

namespace Espera.View.DesignTime
{
    public class DesignTimeTagEditorViewModel : TagEditorViewModel
    {
        public DesignTimeTagEditorViewModel()
            : base(new[]
            {
                new LocalSong("C://song.mp3", TimeSpan.FromMinutes(3))
                {
                    Artist = "The Artist",
                    Album = "The Album",
                    Genre = "The Genre",
                    Title = "The Title",
                    TrackNumber = 1
                }
            }, () => Task.FromResult(false))
        { }
    }
}