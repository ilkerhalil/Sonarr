using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Housekeeping.Housekeepers
{
    public class CleanupDuplicateExtraFiles : IHousekeepingTask
    {
        private readonly IMainDatabase _database;

        public CleanupDuplicateExtraFiles(IMainDatabase database)
        {
            _database = database;
        }

        public void Clean()
        {
            DeleteDuplicateSeriesMetadata();
            DeleteDuplicateEpisodeMetadata();
            DeleteDuplicateEpisodeImages();
        }

        private void DeleteDuplicateSeriesMetadata()
        {
            var mapper = _database.GetDataMapper();

            mapper.ExecuteNonQuery(@"DELETE FROM ExtraFiles
                                     WHERE Id IN (
                                         SELECT Id FROM ExtraFiles
                                         WHERE MetadataType = 1
                                         GROUP BY SeriesId, MetadataConsumer
                                         HAVING COUNT(SeriesId) > 1
                                     )");
        }

        private void DeleteDuplicateEpisodeMetadata()
        {
            var mapper = _database.GetDataMapper();

            mapper.ExecuteNonQuery(@"DELETE FROM ExtraFiles
                                     WHERE Id IN (
                                         SELECT Id FROM ExtraFiles
                                         WHERE MetadataType = 2
                                         GROUP BY EpisodeFileId, MetadataConsumer
                                         HAVING COUNT(EpisodeFileId) > 1
                                     )");
        }

        private void DeleteDuplicateEpisodeImages()
        {
            var mapper = _database.GetDataMapper();

            mapper.ExecuteNonQuery(@"DELETE FROM ExtraFiles
                                     WHERE Id IN (
                                         SELECT Id FROM ExtraFiles
                                         WHERE MetadataType = 5
                                         GROUP BY EpisodeFileId, MetadataConsumer
                                         HAVING COUNT(EpisodeFileId) > 1
                                     )");
        }
    }
}
