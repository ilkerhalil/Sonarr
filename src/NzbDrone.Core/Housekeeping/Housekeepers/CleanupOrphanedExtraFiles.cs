using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Housekeeping.Housekeepers
{
    public class CleanupOrphanedExtraFiles : IHousekeepingTask
    {
        private readonly IMainDatabase _database;

        public CleanupOrphanedExtraFiles(IMainDatabase database)
        {
            _database = database;
        }

        public void Clean()
        {
            DeleteOrphanedBySeries();
            DeleteOrphanedByEpisodeFile();
            DeleteWhereEpisodeFileIsZero();
        }

        private void DeleteOrphanedBySeries()
        {
            var mapper = _database.GetDataMapper();

            mapper.ExecuteNonQuery(@"DELETE FROM ExtraFiles
                                     WHERE Id IN (
                                     SELECT ExtraFiles.Id FROM ExtraFiles
                                     LEFT OUTER JOIN Series
                                     ON ExtraFiles.SeriesId = Series.Id
                                     WHERE Series.Id IS NULL)");
        }

        private void DeleteOrphanedByEpisodeFile()
        {
            var mapper = _database.GetDataMapper();

            mapper.ExecuteNonQuery(@"DELETE FROM ExtraFiles
                                     WHERE Id IN (
                                     SELECT ExtraFiles.Id FROM ExtraFiles
                                     LEFT OUTER JOIN EpisodeFiles
                                     ON ExtraFiles.EpisodeFileId = EpisodeFiles.Id
                                     WHERE ExtraFiles.EpisodeFileId > 0
                                     AND EpisodeFiles.Id IS NULL)");
        }

        private void DeleteWhereEpisodeFileIsZero()
        {
            var mapper = _database.GetDataMapper();

            mapper.ExecuteNonQuery(@"DELETE FROM ExtraFiles
                                     WHERE Id IN (
                                     SELECT Id FROM ExtraFiles
                                     WHERE MetadataType IN (2, 5)
                                     AND EpisodeFileId = 0)");
        }
    }
}
