using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Espera.Core.Audio;

namespace Espera.Core.Management
{
    public class LibraryReader
    {
        public static IEnumerable<Song> ReadSongs(Stream stream)
        {
            var document = XDocument.Load(stream);

            return document
                .Descendants("Root")
                .Descendants("Songs")
                .Elements("Song")
                .Select
                (
                    element =>
                        new LocalSong
                        (
                            element.Attribute("Path").Value,
                            (AudioType)Enum.Parse(typeof(AudioType), element.Attribute("AudioType").Value),
                            TimeSpan.FromTicks(Int64.Parse(element.Attribute("Duration").Value))
                        )
                        {
                            Album = element.Attribute("Album").Value,
                            Artist = element.Attribute("Artist").Value,
                            Genre = element.Attribute("Genre").Value,
                            Title = element.Attribute("Title").Value,
                            TrackNumber = Int32.Parse(element.Attribute("TrackNumber").Value)
                        }
                );
        }
    }
}