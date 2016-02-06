using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(99)]
    public class extra_and_subtitle_files : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Alter.Table("MetadataFiles").AddColumn("Added").AsDateTime().Nullable();

            Create.TableForModel("ExtraFiles")
                  .WithColumn("SeriesId").AsInt32().NotNullable()
                  .WithColumn("SeasonNumber").AsInt32().NotNullable()
                  .WithColumn("EpisodeFileId").AsInt32().NotNullable()
                  .WithColumn("RelativePath").AsString().NotNullable()
                  .WithColumn("Added").AsDateTime().NotNullable()
                  .WithColumn("LastUpdated").AsDateTime().NotNullable();

            Create.TableForModel("SubtitleFiles")
                  .WithColumn("SeriesId").AsInt32().NotNullable()
                  .WithColumn("SeasonNumber").AsInt32().NotNullable()
                  .WithColumn("EpisodeFileId").AsInt32().NotNullable()
                  .WithColumn("RelativePath").AsString().NotNullable()
                  .WithColumn("Added").AsDateTime().NotNullable()
                  .WithColumn("LastUpdated").AsDateTime().NotNullable()
                  .WithColumn("Language").AsInt32().NotNullable();
        }
    }
}
