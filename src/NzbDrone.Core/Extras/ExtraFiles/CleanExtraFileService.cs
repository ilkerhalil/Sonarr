using System.IO;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Extras.ExtraFiles
{
    public interface ICleanMetadataService
    {
        void Clean(Series series);
    }

    public class CleanExtraFileService : ICleanMetadataService
    {
        private readonly IExtraFileService _extraFileService;
        private readonly IDiskProvider _diskProvider;
        private readonly Logger _logger;

        public CleanExtraFileService(IExtraFileService extraFileService,
                                    IDiskProvider diskProvider,
                                    Logger logger)
        {
            _extraFileService = extraFileService;
            _diskProvider = diskProvider;
            _logger = logger;
        }

        public void Clean(Series series)
        {
            _logger.Debug("Cleaning missing metadata files for series: {0}", series.Title);

            var metadataFiles = _extraFileService.GetFilesBySeries(series.Id);

            foreach (var metadataFile in metadataFiles)
            {
                if (!_diskProvider.FileExists(Path.Combine(series.Path, metadataFile.RelativePath)))
                {
                    _logger.Debug("Deleting metadata file from database: {0}", metadataFile.RelativePath);
                    _extraFileService.Delete(metadataFile.Id);
                }
            }
        }
    }
}
