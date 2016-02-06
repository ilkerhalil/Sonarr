using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Extras.Files;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Extras
{
    public class ExtraService : IHandle<MediaCoversUpdatedEvent>,
                                IHandle<EpisodeImportedEvent>,
                                IHandle<EpisodeFolderCreatedEvent>,
                                IHandle<SeriesRenamedEvent>
    {
        private readonly IMediaFileService _mediaFileService;
        private readonly IEpisodeService _episodeService;
        private readonly IDiskProvider _diskProvider;
        private readonly IConfigService _configService;
        private readonly List<IManageExtraFiles> _extraFileManagers;
        private readonly Logger _logger;

        public ExtraService(IMediaFileService mediaFileService,
                            IEpisodeService episodeService,
                            IDiskProvider diskProvider,
                            IConfigService configService,
                            List<IManageExtraFiles> extraFileManagers,
                            Logger logger)
        {
            _mediaFileService = mediaFileService;
            _episodeService = episodeService;
            _diskProvider = diskProvider;
            _configService = configService;
            _extraFileManagers = extraFileManagers.OrderBy(e => e.Order).ToList();
            _logger = logger;
        }

        public void Handle(MediaCoversUpdatedEvent message)
        {
            var series = message.Series;
            var episodeFiles = GetEpisodeFiles(series.Id);

            foreach (var extraFileManager in _extraFileManagers)
            {
                extraFileManager.CreateAfterSeriesScan(series, episodeFiles);
            }
        }

        public void Handle(EpisodeImportedEvent message)
        {
            var series = message.EpisodeInfo.Series;
            var episodeFile = message.ImportedEpisode;

            foreach (var extraFileManager in _extraFileManagers)
            {
                extraFileManager.CreateAfterEpisodeImport(series, episodeFile);
            }

            if (message.NewDownload)
            {        
                var sourcePath = message.EpisodeInfo.Path;
                var sourceFolder = _diskProvider.GetParentFolder(sourcePath);
                var sourceFileName = Path.GetFileNameWithoutExtension(sourcePath);
                var files = _diskProvider.GetFiles(sourceFolder, SearchOption.TopDirectoryOnly);

                var wantedExtensions = _configService.ExtraFileExtensions.Split(new [] {','}, StringSplitOptions.RemoveEmptyEntries)
                                                                         .Select(e => e.Trim(' ', '.'))
                                                                         .ToList();

                var matchingFilenames = files.Where(f => Path.GetFileNameWithoutExtension(f).StartsWith(sourceFileName));

                foreach (var matchingFilename in matchingFilenames)
                {
                    var matchingExtension = wantedExtensions.FirstOrDefault(e => matchingFilename.EndsWith(e));

                    if (matchingExtension == null)
                    {
                        continue;
                    }

                    try
                    {
                        foreach (var extraFileManager in _extraFileManagers)
                        {
                            var extraFile = extraFileManager.Import(series, episodeFile, matchingFilename, matchingExtension, message.IsReadOnly);

                            if (extraFile != null)
                            {
                                break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.WarnException("Failed to import extra file: " + matchingFilename, ex);
                    }
                }
            }
        }

        public void Handle(EpisodeFolderCreatedEvent message)
        {
            var series = message.Series;

            foreach (var extraFileManager in _extraFileManagers)
            {
                extraFileManager.CreateAfterEpisodeImport(series, message.SeriesFolder, message.SeasonFolder);
            }
        }

        public void Handle(SeriesRenamedEvent message)
        {
            var series = message.Series;
            var episodeFiles = GetEpisodeFiles(series.Id);

            foreach (var extraFileManager in _extraFileManagers)
            {
                extraFileManager.MoveFilesAfterRename(series, episodeFiles);
            }
        }

        private List<EpisodeFile> GetEpisodeFiles(int seriesId)
        {
            var episodeFiles = _mediaFileService.GetFilesBySeries(seriesId);
            var episodes = _episodeService.GetEpisodeBySeries(seriesId);

            foreach (var episodeFile in episodeFiles)
            {
                var localEpisodeFile = episodeFile;
                episodeFile.Episodes = new LazyList<Episode>(episodes.Where(e => e.EpisodeFileId == localEpisodeFile.Id));
            }

            return episodeFiles;
        }
    }
}
