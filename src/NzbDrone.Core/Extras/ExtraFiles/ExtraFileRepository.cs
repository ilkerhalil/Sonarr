using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Extras.ExtraFiles
{
    public interface IExtraFileRepository : IBasicRepository<ExtraFile>
    {
        void DeleteForSeries(int seriesId);
        void DeleteForSeason(int seriesId, int seasonNumber);
        void DeleteForEpisodeFile(int episodeFileId);
        List<ExtraFile> GetFilesBySeries(int seriesId);
        List<ExtraFile> GetFilesBySeason(int seriesId, int seasonNumber);
        List<ExtraFile> GetFilesByEpisodeFile(int episodeFileId);
        ExtraFile FindByPath(string path);
    }

    public class ExtraFileRepository : BasicRepository<ExtraFile>, IExtraFileRepository
    {
        public ExtraFileRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public void DeleteForSeries(int seriesId)
        {
            Delete(c => c.SeriesId == seriesId);
        }

        public void DeleteForSeason(int seriesId, int seasonNumber)
        {
            Delete(c => c.SeriesId == seriesId && c.SeasonNumber == seasonNumber);
        }

        public void DeleteForEpisodeFile(int episodeFileId)
        {
            Delete(c => c.EpisodeFileId == episodeFileId);
        }

        public List<ExtraFile> GetFilesBySeries(int seriesId)
        {
            return Query.Where(c => c.SeriesId == seriesId);
        }

        public List<ExtraFile> GetFilesBySeason(int seriesId, int seasonNumber)
        {
            return Query.Where(c => c.SeriesId == seriesId && c.SeasonNumber == seasonNumber);
        }

        public List<ExtraFile> GetFilesByEpisodeFile(int episodeFileId)
        {
            return Query.Where(c => c.EpisodeFileId == episodeFileId);
        }

        public ExtraFile FindByPath(string path)
        {
            return Query.Where(c => c.RelativePath == path).SingleOrDefault();
        }
    }
}
