using Rareform.Validation;
using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace Espera.Core
{
    public interface ISongFinder<out T> where T : Song
    {
        IObservable<T> GetSongs();
    }
}