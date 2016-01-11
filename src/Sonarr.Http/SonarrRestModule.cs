using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Datastore;
using Sonarr.Http.Mapping;
using Sonarr.Http.REST;
using Sonarr.Http.Validation;

namespace Sonarr.Http
{
    public abstract class SonarrRestModule<TResource> : RestModule<TResource> where TResource : RestResource, new()
    {
        protected string Resource { get; private set; }

        protected SonarrRestModule()
            : this(new TResource().ResourceName)
        {
        }

        protected SonarrRestModule(string resource)
            : base("/api/v3/" + resource.Trim('/'))
        {
            Resource = resource;
            PostValidator.RuleFor(r => r.Id).IsZero();
            PutValidator.RuleFor(r => r.Id).ValidId();
        }

        protected int GetNewId<TModel>(Func<TModel, TModel> function, TResource resource) where TModel : ModelBase, new()
        {
            var model = resource.InjectTo<TModel>();
            function(model);
            return model.Id;
        }

        protected List<TResource> ToListResource<TModel>(Func<IEnumerable<TModel>> function) where TModel : class
        {
            var modelList = function();
            return ToListResource(modelList);
        }

        protected virtual List<TResource> ToListResource<TModel>(IEnumerable<TModel> modelList) where TModel : class
        {
            return modelList.Select(ToResource).ToList();
        }

        protected virtual TResource ToResource<TModel>(TModel model) where TModel : class
        {
            return model.InjectTo<TResource>();
        }

        protected PagingResource<TResource> ApplyToPage<TModel>(Func<PagingSpec<TModel>, PagingSpec<TModel>> function, PagingSpec<TModel> pagingSpec) where TModel : ModelBase, new()
        {
            pagingSpec = function(pagingSpec);

            return new PagingResource<TResource>
                       {
                           Page = pagingSpec.Page,
                           PageSize = pagingSpec.PageSize,
                           SortDirection = pagingSpec.SortDirection,
                           SortKey = pagingSpec.SortKey,
                           TotalRecords = pagingSpec.TotalRecords,
                           Records = ToListResource(pagingSpec.Records)
                       };
        }
    }
}