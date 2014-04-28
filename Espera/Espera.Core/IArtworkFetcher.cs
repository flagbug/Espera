using System;
using System.Threading.Tasks;

namespace Espera.Core
{
    internal interface IArtworkFetcher
    {
        Task<Uri> RetrieveAsync(string artist, string album);
    }
}