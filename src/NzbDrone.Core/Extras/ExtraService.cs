using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Extras.ExtraFiles;
using NzbDrone.Core.Extras.Metadata;
using NzbDrone.Core.Extras.Subtitles;
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
        private readonly IMetadataService _metadataService;
        private readonly ISubtitleService _subtitleService;
        private readonly IExtraFileService _extraFileService;
        private readonly IMediaFileService _mediaFileService;
        private readonly IEpisodeService _episodeService;
        private readonly IDiskProvider _diskProvider;
        private readonly IDiskTransferService _diskTransferService;
        private readonly IConfigService _configService;
        private readonly Logger _logger;

        public ExtraService(IMetadataService metadataService,
                            ISubtitleService subtitleService,
                            IExtraFileService extraFileService,
                            IMediaFileService mediaFileService,
                            IEpisodeService episodeService,
                            IDiskProvider diskProvider,
                            IDiskTransferService diskTransferService,
                            IConfigService configService,
                            Logger logger)
        {
            _metadataService = metadataService;
            _subtitleService = subtitleService;
            _extraFileService = extraFileService;
            _mediaFileService = mediaFileService;
            _episodeService = episodeService;
            _diskProvider = diskProvider;
            _diskTransferService = diskTransferService;
            _configService = configService;
            _logger = logger;
        }

        public void Handle(MediaCoversUpdatedEvent message)
        {
            var series = message.Series;
            var episodeFiles = GetEpisodeFiles(series.Id);
            var extraFiles = _extraFileService.GetFilesBySeries(series.Id);

            _extraFileService.Upsert(_metadataService.CreateSeasonAndSeriesMetadata(series, episodeFiles, ExtraFilesOfType(extraFiles, ExtraType.Metadata)));
        }

        public void Handle(EpisodeImportedEvent message)
        {
            var series = message.EpisodeInfo.Series;
            var episodeFile = message.ImportedEpisode;

            var extraFiles = new List<ExtraFile>();
            var metadataFiles = _metadataService.CreateEpisodeMetadata(series, episodeFile);

            if (message.NewDownload)
            {
                var trimCharacters = new[] { ' ', '.' };                
                var sourcePath = message.EpisodeInfo.Path;
                var sourceFolder = _diskProvider.GetParentFolder(sourcePath);
                var sourceFileName = Path.GetFileNameWithoutExtension(sourcePath);
                var files = _diskProvider.GetFiles(sourceFolder, SearchOption.TopDirectoryOnly);

                var wantedExtensions = _configService.ExtraFileExtensions.Split(new [] {','}, StringSplitOptions.RemoveEmptyEntries)
                                                                         .Select(e => e.Trim(trimCharacters))
                                                                         .ToList();

                var wantedExtraFiles = files.Where(f => Path.GetFileNameWithoutExtension(f).StartsWith(sourceFileName) &&
                                                        wantedExtensions.Contains(Path.GetExtension(f.Trim(trimCharacters))))
                                            .ToList();

                foreach (var file in wantedExtraFiles)
                {
                    try
                    {
                        var extension = Path.GetExtension(file);
                        var extraFile = _subtitleService.GetExtraFile(file, extension) ?? new ExtraFile();

                        extension = extension == ".nfo" ? ".nfo-orig" : extension;

                        var newFileName = Path.Combine(series.Path, Path.ChangeExtension(episodeFile.RelativePath, extension));

                        extraFile.SeriesId = series.Id;
                        extraFile.SeasonNumber = episodeFile.SeasonNumber;
                        extraFile.EpisodeFileId = episodeFile.SeasonNumber;
                        extraFile.RelativePath = series.Path.GetRelativePath(newFileName);

                        var transferMode = TransferMode.Move;

                        if (message.IsReadOnly)
                        {
                            transferMode = _configService.CopyUsingHardlinks ? TransferMode.HardLinkOrCopy : TransferMode.Copy;
                        }

                        _diskTransferService.TransferFile(file, newFileName, transferMode, true, false);

                        extraFiles.Add(extraFile);
                    }
                    catch (Exception ex)
                    {
                        _logger.WarnException("Failed to import extra file: " + file, ex);
                    }
                }
            }

            extraFiles.AddRange(metadataFiles);
            _extraFileService.Upsert(extraFiles);
        }

        public void Handle(EpisodeFolderCreatedEvent message)
        {
            var series = message.Series;
            var extraFiles = _extraFileService.GetFilesBySeries(series.Id);

            _extraFileService.Upsert(_metadataService.CreateSeasonAndSeriesMetadataAfterEpisodeImport(series, message.SeriesFolder, message.SeasonFolder, ExtraFilesOfType(extraFiles, ExtraType.Metadata)));
        }

        public void Handle(SeriesRenamedEvent message)
        {
            var series = message.Series;
            var episodeFiles = GetEpisodeFiles(series.Id);
            var extraFiles = _extraFileService.GetFilesBySeries(series.Id);

            var movedFiles = new List<ExtraFile>();
            var movedMetadataFiles = _metadataService.MoveFilesAfterRename(series, episodeFiles, ExtraFilesOfType(extraFiles, ExtraType.Metadata));
            var movedSubtitleFiles = _subtitleService.MoveFilesAfterRename(series, episodeFiles, ExtraFilesOfType(extraFiles, ExtraType.Subtitle));
            var movedExtraFiles = MoveFilesAfterRename(series, episodeFiles, ExtraFilesOfType(extraFiles, ExtraType.Other));

            movedFiles.AddRange(movedMetadataFiles);
            movedFiles.AddRange(movedSubtitleFiles);
            movedFiles.AddRange(movedExtraFiles);

            _extraFileService.Upsert(movedFiles);
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

        private List<ExtraFile> ExtraFilesOfType(List<ExtraFile> extraFiles, ExtraType type)
        {
            return extraFiles.Where(e => e.Type == type).ToList();
        }

        private List<ExtraFile> MoveFilesAfterRename(Series series, List<EpisodeFile> episodeFiles, List<ExtraFile> extraFiles)
        {
            var movedFiles = new List<ExtraFile>();

            foreach (var episodeFile in episodeFiles)
            {
                var extraFilesForEpisodeFile = extraFiles.Where(m => m.EpisodeFileId == episodeFile.Id).ToList();

                foreach (var extraFile in extraFilesForEpisodeFile)
                {
                    var existingFilename = Path.Combine(series.Path, extraFile.RelativePath);
                    var extension = Path.GetExtension(existingFilename).TrimStart('.');
                    var newFileName = Path.ChangeExtension(Path.Combine(series.Path, episodeFile.RelativePath), extension);

                    if (!newFileName.PathEquals(existingFilename))
                    {
                        try
                        {
                            _diskProvider.MoveFile(existingFilename, newFileName);
                            extraFile.RelativePath = series.Path.GetRelativePath(newFileName);
                            movedFiles.Add(extraFile);
                        }
                        catch (Exception ex)
                        {
                            _logger.WarnException("Unable to move extra file: " + existingFilename, ex);
                        }
                    }
                }
            }

            return movedFiles;
        }
    }
}
