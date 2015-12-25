using NzbDrone.Core.Extras.ExtraFiles;

namespace NzbDrone.Core.Extras
{
    public interface IImportExtraFiles
    {
        ExtraType Type { get; }
        bool CanHandle(string path);
        ExtraFile GetExtraFile(string path, string extension);
        string GetNewExtension(string extension);
    }
}
