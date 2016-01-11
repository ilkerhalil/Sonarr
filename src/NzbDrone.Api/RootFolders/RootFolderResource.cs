using System.Collections.Generic;
using Sonarr.Http.REST;
using NzbDrone.Core.RootFolders;

namespace NzbDrone.Api.RootFolders
{
    public class RootFolderResource : RestResource
    {
        public string Path { get; set; }
        public long? FreeSpace { get; set; }

        public List<UnmappedFolder> UnmappedFolders { get; set; }
    }
}