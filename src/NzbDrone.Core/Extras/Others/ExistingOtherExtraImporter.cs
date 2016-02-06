using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Extras.Files;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Extras.Others
{
    public class ExistingOtherExtraImporter : IImportExistingExtraFiles
    {
        private readonly IOtherExtraFileService _otherExtraFileService;
        private readonly IParsingService _parsingService;
        private readonly Logger _logger;

        public ExistingOtherExtraImporter(IOtherExtraFileService otherExtraFileService,
                                          IParsingService parsingService,
                                          Logger logger)
        {
            _otherExtraFileService = otherExtraFileService;
            _parsingService = parsingService;
            _logger = logger;
        }

        public int Order
        {
            get
            {
                return 2;
            }
        }

        public IEnumerable<ExtraFile> ProcessFiles(Series series, List<string> filesOnDisk)
        {
            var extraFiles = new List<OtherExtraFile>();

            foreach (var possibleExtraFile in filesOnDisk)
            {
                var localEpisode = _parsingService.GetLocalEpisode(possibleExtraFile, series);

                if (localEpisode == null)
                {
                    _logger.Debug("Unable to parse extra file: {0}", possibleExtraFile);
                    continue;
                }

                if (localEpisode.Episodes.Empty())
                {
                    _logger.Debug("Cannot find related episodes for: {0}", possibleExtraFile);
                    continue;
                }

                if (localEpisode.Episodes.DistinctBy(e => e.EpisodeFileId).Count() > 1)
                {
                    _logger.Debug("Extra file: {0} does not match existing files.", possibleExtraFile);
                    continue;
                }

                var extraFile = new OtherExtraFile
                {
                    SeriesId = series.Id,
                    SeasonNumber = localEpisode.SeasonNumber,
                    EpisodeFileId = localEpisode.Episodes.First().EpisodeFileId,
                    RelativePath = series.Path.GetRelativePath(possibleExtraFile)
                };

                extraFiles.Add(extraFile);
            }

            _logger.Info("Found {0} existing other extra files", extraFiles.Count);
            _otherExtraFileService.Upsert(extraFiles);

            return extraFiles;
        }
    }
}
