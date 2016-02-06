using System.Collections.Generic;
using System.IO;
using System.Linq;
using NzbDrone.Common;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Extras.Files
{
    public interface IManageExtraFiles
    {
        int Order { get; }
        IEnumerable<ExtraFile> CreateAfterSeriesScan(Series series, List<EpisodeFile> episodeFiles);
        IEnumerable<ExtraFile> CreateAfterEpisodeImport(Series series, EpisodeFile episodeFile);
        IEnumerable<ExtraFile> CreateAfterEpisodeImport(Series series, string seriesFolder, string seasonFolder);
        IEnumerable<ExtraFile> MoveFilesAfterRename(Series series, List<EpisodeFile> episodeFiles);
        ExtraFile Import(Series series, EpisodeFile episodeFile, string path, string extension, bool readOnly);
        List<string> FilterExistingFiles(Series series, List<string> files);
    }

    public abstract class ExtraFileManager<TExtraFile> : IManageExtraFiles
        where TExtraFile : ExtraFile, new()

    {
        private readonly IConfigService _configService;
        private readonly IDiskTransferService _diskTransferService;
        private readonly IExtraFileService<TExtraFile> _extraFileService;

        public ExtraFileManager(IConfigService configService,
                                IDiskTransferService diskTransferService,
                                IExtraFileService<TExtraFile> extraFileService)
        {
            _configService = configService;
            _diskTransferService = diskTransferService;
            _extraFileService = extraFileService;
        }

        public abstract int Order { get; }
        public abstract IEnumerable<ExtraFile> CreateAfterSeriesScan(Series series, List<EpisodeFile> episodeFiles);
        public abstract IEnumerable<ExtraFile> CreateAfterEpisodeImport(Series series, EpisodeFile episodeFile);
        public abstract IEnumerable<ExtraFile> CreateAfterEpisodeImport(Series series, string seriesFolder, string seasonFolder);
        public abstract IEnumerable<ExtraFile> MoveFilesAfterRename(Series series, List<EpisodeFile> episodeFiles);
        public abstract ExtraFile Import(Series series, EpisodeFile episodeFile, string path, string extension, bool readOnly);

        public List<string> FilterExistingFiles(Series series, List<string> files)
        {
            var seriesFiles = _extraFileService.GetFilesBySeries(series.Id).Select(f => Path.Combine(series.Path, f.RelativePath)).ToList();

            if (!seriesFiles.Any())
            {
                return files;
            }

            return files.Except(seriesFiles, PathEqualityComparer.Instance).ToList();
        }

        protected TExtraFile ImportFile(Series series, EpisodeFile episodeFile, string path, string extension, bool readOnly)
        {
            var newFileName = Path.Combine(series.Path, Path.ChangeExtension(episodeFile.RelativePath, extension));

            var transferMode = TransferMode.Move;

            if (readOnly)
            {
                transferMode = _configService.CopyUsingHardlinks ? TransferMode.HardLinkOrCopy : TransferMode.Copy;
            }

            _diskTransferService.TransferFile(path, newFileName, transferMode, true, false);

            return new TExtraFile
            {
                SeriesId = series.Id,
                SeasonNumber = episodeFile.SeasonNumber,
                EpisodeFileId = episodeFile.Id,
                RelativePath = series.Path.GetRelativePath(newFileName)
            };
        }
    }
}
