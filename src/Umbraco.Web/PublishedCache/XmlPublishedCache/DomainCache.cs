using System.Collections.Generic;
using System.Linq;
using Umbraco.Core.Services;
using Umbraco.Web.Routing;

namespace Umbraco.Web.PublishedCache.XmlPublishedCache
{
    class DomainCache : IDomainCache
    {
        private readonly IDomainService _domainService;

        public DomainCache(IDomainService domainService)
        {
            _domainService = domainService;
        }

        public IEnumerable<Domain> GetAll(bool includeWildcards)
        {
            return _domainService.GetAll(includeWildcards)
                .Select(x => new Domain(x.Id, x.DomainName, x.RootContent.Id, x.Language.CultureInfo, x.IsWildcard));
        }

        public IEnumerable<Domain> GetAssigned(int contentId, bool includeWildcards)
        {
            return _domainService.GetAssignedDomains(contentId, includeWildcards)
                .Select(x => new Domain(x.Id, x.DomainName, x.RootContent.Id, x.Language.CultureInfo, x.IsWildcard));
        }
    }
}
