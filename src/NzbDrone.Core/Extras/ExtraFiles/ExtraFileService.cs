using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Common;
using NzbDrone.Common.Disk;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.Events;

namespace NzbDrone.Core.Extras.ExtraFiles
{
    public interface IExtraFileService
    {
        List<ExtraFile> GetFilesBySeries(int seriesId);
        List<ExtraFile> GetFilesByEpisodeFile(int episodeFileId);
        ExtraFile FindByPath(string path);
        List<string> FilterExistingFiles(List<string> files, Series series);
        void Upsert(List<ExtraFile> extraFiles);
        void Delete(int id);
    }

    public class ExtraFileService : IExtraFileService,
                                    IHandleAsync<SeriesDeletedEvent>,
                                    IHandleAsync<EpisodeFileDeletedEvent>
    {
        private readonly IExtraFileRepository _repository;
        private readonly ISeriesService _seriesService;
        private readonly IDiskProvider _diskProvider;
        private readonly Logger _logger;

        public ExtraFileService(IExtraFileRepository repository,
                                ISeriesService seriesService,
                                IDiskProvider diskProvider,
                                Logger logger)
        {
            _repository = repository;
            _seriesService = seriesService;
            _diskProvider = diskProvider;
            _logger = logger;
        }

        public List<ExtraFile> GetFilesBySeries(int seriesId)
        {
            return _repository.GetFilesBySeries(seriesId);
        }

        public List<ExtraFile> GetFilesByEpisodeFile(int episodeFileId)
        {
            return _repository.GetFilesByEpisodeFile(episodeFileId);
        }

        public ExtraFile FindByPath(string path)
        {
            return _repository.FindByPath(path);
        }

        public List<string> FilterExistingFiles(List<string> files, Series series)
        {
            var seriesFiles = GetFilesBySeries(series.Id).Select(f => Path.Combine(series.Path, f.RelativePath)).ToList();

            if (!seriesFiles.Any())
            {
                return files;
            }

            return files.Except(seriesFiles, PathEqualityComparer.Instance).ToList();
        }

        public void Upsert(List<ExtraFile> extraFiles)
        {
            extraFiles.ForEach(m =>
            {
                m.LastUpdated = DateTime.UtcNow;

                if (m.Id == 0)
                {
                    m.Added = m.LastUpdated;
                }
            });

            _repository.InsertMany(extraFiles.Where(m => m.Id == 0).ToList());
            _repository.UpdateMany(extraFiles.Where(m => m.Id > 0).ToList());
        }

        public void Delete(int id)
        {
            _repository.Delete(id);
        }

        public void HandleAsync(SeriesDeletedEvent message)
        {
            _logger.Debug("Deleting Extra from database for series: {0}", message.Series);
            _repository.DeleteForSeries(message.Series.Id);
        }

        public void HandleAsync(EpisodeFileDeletedEvent message)
        {
            var episodeFile = message.EpisodeFile;
            var series = _seriesService.GetSeries(message.EpisodeFile.SeriesId);

            foreach (var metadata in _repository.GetFilesByEpisodeFile(episodeFile.Id))
            {
                var path = Path.Combine(series.Path, metadata.RelativePath);

                if (_diskProvider.FileExists(path))
                {
                    _diskProvider.DeleteFile(path);
                }
            }

            _logger.Debug("Deleting Metadata from database for episode file: {0}", episodeFile);
            _repository.DeleteForEpisodeFile(episodeFile.Id);
        }
    }
}
