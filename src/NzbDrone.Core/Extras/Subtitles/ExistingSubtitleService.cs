using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Extras.ExtraFiles;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Extras.Subtitles
{
    public interface IExistingSubtitleService
    {
        List<ExtraFile> ProcessFiles(Series series, List<string> filesOnDisk);
    }

    public class ExistingSubtitleService : IExistingSubtitleService
    {
        private readonly IParsingService _parsingService;
        private readonly Logger _logger;

        public ExistingSubtitleService(IParsingService parsingService, Logger logger)
        {
            _parsingService = parsingService;
            _logger = logger;
        }

        public List<ExtraFile> ProcessFiles(Series series, List<string> filesOnDisk)
        {
            var subtitleFiles = new List<ExtraFile>();

            foreach (var possibleSubtitleFile in filesOnDisk)
            {
                var extension = Path.GetExtension(possibleSubtitleFile);

                if (SubtitleFileExtensions.Extensions.Contains(extension))
                {
                    var localEpisode = _parsingService.GetLocalEpisode(possibleSubtitleFile, series);

                    if (localEpisode == null)
                    {
                        _logger.Debug("Unable to parse subtitle file: {0}", possibleSubtitleFile);
                        continue;
                    }

                    if (localEpisode.Episodes.Empty())
                    {
                        _logger.Debug("Cannot find related episodes for: {0}", possibleSubtitleFile);
                        continue;
                    }

                    if (localEpisode.Episodes.DistinctBy(e => e.EpisodeFileId).Count() > 1)
                    {
                        _logger.Debug("Subtitle file: {0} does not match existing files.", possibleSubtitleFile);
                        continue;
                    }

                    var subtitleFile = new ExtraFile
                                       {
                                           Type = ExtraType.Subtitle,
                                           SeriesId = series.Id,
                                           SeasonNumber = localEpisode.SeasonNumber,
                                           EpisodeFileId = localEpisode.Episodes.First().EpisodeFileId,
                                           RelativePath = series.Path.GetRelativePath(possibleSubtitleFile),
                                           Language = LanguageParser.ParseSubtitleLanguage(possibleSubtitleFile)
                                       };

                    subtitleFiles.Add(subtitleFile);
                }
            }

            return subtitleFiles;
        }
    }
}
