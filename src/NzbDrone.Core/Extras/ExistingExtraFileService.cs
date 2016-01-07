using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Extras.ExtraFiles;
using NzbDrone.Core.Extras.Metadata;
using NzbDrone.Core.Extras.Subtitles;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Extras
{
    public class ExistingExtraFileService : IHandle<SeriesScannedEvent>
    {
        private readonly IExtraFileService _extraFileService;
        private readonly IDiskProvider _diskProvider;
        private readonly IExistingMetadataService _existingMetadataService;
        private readonly IExistingSubtitleService _existingSubtitleService;
        private readonly IParsingService _parsingService;
        private readonly Logger _logger;

        public ExistingExtraFileService(IExtraFileService extraFileService,
                                        IDiskProvider diskProvider,
                                        IExistingMetadataService existingMetadataService,
                                        IExistingSubtitleService existingSubtitleService,
                                        IParsingService parsingService,
                                        Logger logger)
        {
            _extraFileService = extraFileService;
            _diskProvider = diskProvider;
            _existingMetadataService = existingMetadataService;
            _existingSubtitleService = existingSubtitleService;
            _parsingService = parsingService;
            _logger = logger;
        }

        private List<ExtraFile> ProcessFiles(Series series, List<string> filesOnDisk)
        {
            var extraFiles = new List<ExtraFile>();

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

                var extraFile = new ExtraFile
                {
                    Type = ExtraType.Other,
                    SeriesId = series.Id,
                    SeasonNumber = localEpisode.SeasonNumber,
                    EpisodeFileId = localEpisode.Episodes.First().EpisodeFileId,
                    RelativePath = series.Path.GetRelativePath(possibleExtraFile)
                };

                extraFiles.Add(extraFile);
            }

            return extraFiles;
        }

        private List<string> FilterMatchedFiles(List<string> excludedFiles, List<ExtraFile> matchedFiles, Series series)
        {
            return excludedFiles.Except(matchedFiles.Select(m => Path.Combine(series.Path, m.RelativePath))).ToList();
        }

        public void Handle(SeriesScannedEvent message)
        {
            var series = message.Series;
            var extraFiles = new List<ExtraFile>();

            if (!_diskProvider.FolderExists(series.Path))
            {
                return;
            }

            _logger.Debug("Looking for existing extra files in {0}", series.Path);

            var filesOnDisk = _diskProvider.GetFiles(series.Path, SearchOption.AllDirectories);
            var possibleExtraFiles = filesOnDisk.Where(c => !MediaFileExtensions.Extensions.Contains(Path.GetExtension(c).ToLower()) && !c.StartsWith(Path.Combine(series.Path, "EXTRAS"))).ToList();
            var filteredFiles = _extraFileService.FilterExistingFiles(possibleExtraFiles, series);

            // Process then exclude metadata files
            var metadataFiles = _existingMetadataService.ProcessFiles(series, filteredFiles);
            filteredFiles = FilterMatchedFiles(filteredFiles, metadataFiles, series);

            // Process then exclude subtitle files
            var subtitleFiles = _existingSubtitleService.ProcessFiles(series, filteredFiles);
            filteredFiles = FilterMatchedFiles(filteredFiles, subtitleFiles, series);

            // Process remaining extra files
            var newExtraFiles = ProcessFiles(series, filteredFiles);

            extraFiles.AddRange(metadataFiles);
            extraFiles.AddRange(subtitleFiles);
            extraFiles.AddRange(newExtraFiles);

            if (extraFiles.Any())
            {
                _logger.Info("Found {0} extra Files, {1} metadata files, {2} subtitles and {3} other files", extraFiles, metadataFiles, subtitleFiles, newExtraFiles);
            }

            _extraFileService.Upsert(extraFiles);
        }

        
    }
}
