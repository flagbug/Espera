using System;
using System.Threading.Tasks;

namespace Espera.Core
{
    public interface IArtworkFetcher
    {
        Task<Uri> RetrieveAsync(string artist, string album);
    }
}