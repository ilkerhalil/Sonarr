using Nancy.Bootstrapper;

namespace Sonarr.Http.Extensions.Pipelines
{
    public interface IRegisterNancyPipeline
    {
        void Register(IPipelines pipelines);
    }
}