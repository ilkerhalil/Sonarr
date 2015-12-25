using System.Data;
using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(99)]
    public class extra_files : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            //TODO: Convert MetadataFiles to ExtraFiles

            Create.TableForModel("ExtraFiles")
                  .WithColumn("SeriesId").AsInt32().NotNullable()
                  .WithColumn("SeasonNumber").AsInt32().Nullable()
                  .WithColumn("EpisodeFileId").AsInt32().Nullable()
                  .WithColumn("Type").AsInt32().NotNullable()
                  .WithColumn("RelativePath").AsString().NotNullable()
                  .WithColumn("Hash").AsString().Nullable()
                  .WithColumn("Added").AsDateTime().NotNullable()
                  .WithColumn("LastUpdated").AsDateTime().NotNullable()

                  //Metadata
                  .WithColumn("MetadataConsumer").AsString().Nullable()
                  .WithColumn("MetadataType").AsInt32().Nullable()

                  //Subtitles
                  .WithColumn("Language").AsInt32().Nullable();

            Execute.WithConnection(ConvertMetadataFilesToExtraFiles);

            Delete.Table("MetadataFiles");
        }

        private void ConvertMetadataFilesToExtraFiles(IDbConnection conn, IDbTransaction tran)
        {
            using (IDbCommand cmd = conn.CreateCommand())
            {
                cmd.Transaction = tran;
                cmd.CommandText = @"INSERT INTO ExtraFiles (SeriesId, SeasonNumber, EpisodeFileId, Type, RelativePath, Hash, Added, LastUpdated, MetadataConsumer, MetadataType)
                                    SELECT SeriesId, SeasonNumber, EpisodeFileId, 1 as Type, RelativePath, Hash, datetime('2015-12-27 00:00:00') as Added, LastUpdated, Consumer as MetadataConsumer, Type as MetadataType
                                    FROM MetadataFiles";
                cmd.ExecuteNonQuery();
            }
        }
    }
}
