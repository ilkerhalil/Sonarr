using Sonarr.Http.REST;

namespace NzbDrone.Api.RemotePathMappings
{
    public class RemotePathMappingResource : RestResource
    {
        public string Host { get; set; }
        public string RemotePath { get; set; }
        public string LocalPath { get; set; }
    }
}
