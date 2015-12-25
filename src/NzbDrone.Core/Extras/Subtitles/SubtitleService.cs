using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Extras.ExtraFiles;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Extras.Subtitles
{
    public interface ISubtitleService
    {
        ExtraFile GetExtraFile(string possibleSubtitleFile, string extension);
        List<ExtraFile> MoveFilesAfterRename(Series series, List<EpisodeFile> episodeFiles, List<ExtraFile> subtitleFiles);
    }

    public class SubtitleService : ISubtitleService
    {
        private readonly IDiskProvider _diskProvider;
        private readonly Logger _logger;

        public SubtitleService(IDiskProvider diskProvider,
                               Logger logger)
        {
            _diskProvider = diskProvider;
            _logger = logger;
        }

        public ExtraFile GetExtraFile(string possibleSubtitleFile, string extension)
        {
            if (SubtitleFileExtensions.Extensions.Contains(extension))
            {
                return new ExtraFile
                {
                    Type = ExtraType.Subtitle,
                    Language = LanguageParser.ParseSubtitleLanguage(possibleSubtitleFile)
                };
            }

            return null;
        }

        public List<ExtraFile> MoveFilesAfterRename(Series series, List<EpisodeFile> episodeFiles, List<ExtraFile> subtitleFiles)
        {
            var movedFiles = new List<ExtraFile>();

            foreach (var episodeFile in episodeFiles)
            {
                var extraFilesForEpisodeFile = subtitleFiles.Where(m => m.EpisodeFileId == episodeFile.Id).ToList();

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
                            _logger.WarnException("Unable to move subtitle file: " + existingFilename, ex);
                        }
                    }
                }
            }

            return movedFiles;
        }
    }
}
