using System;
using System.Collections.Generic;
using System.Linq;
using Espera.Core;
using FlagLib.Patterns.MVVM;

namespace Espera.View
{
    internal class MainViewModel : ViewModelBase<MainViewModel>
    {
        private readonly Library library;

        public IEnumerable<string> Artists
        {
            get
            {
                return this.library.Songs
                    .GroupBy(song => song.Artist)
                    .Select(group => group.Key)
                    .OrderBy(artist => artist);
            }
        }

        public TimeSpan TotalTime
        {
            get { return this.library.TotalTime; }
        }

        public TimeSpan CurrentTime
        {
            get { return this.library.CurrentTime; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MainViewModel"/> class.
        /// </summary>
        public MainViewModel()
        {
            this.library = new Library();
        }
    }
}