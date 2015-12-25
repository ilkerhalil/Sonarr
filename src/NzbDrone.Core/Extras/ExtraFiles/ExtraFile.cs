using System;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Extras.Metadata;
using NzbDrone.Core.Parser;

namespace NzbDrone.Core.Extras.ExtraFiles
{
    public class ExtraFile : ModelBase
    {
        public int SeriesId { get; set; }
        public int? EpisodeFileId { get; set; }
        public int? SeasonNumber { get; set; }
        public ExtraType Type { get; set; }
        public string RelativePath { get; set; }
        public string Hash { get; set; }
        public DateTime Added { get; set; }
        public DateTime LastUpdated { get; set; }

        //Metadata
        public string MetadataConsumer { get; set; }
        public MetadataType MetadataType { get; set; }

        //Subtitles
        public Language Language { get; set; }
    }
}
