using System;
using Espera.Core;

namespace Espera.View.ViewModels
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
            })
        { }
    }
}