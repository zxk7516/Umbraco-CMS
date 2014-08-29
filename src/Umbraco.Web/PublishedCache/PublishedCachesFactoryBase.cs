using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core.ObjectResolution;
using Umbraco.Core.Models.Membership;

namespace Umbraco.Web.PublishedCache
{
    abstract class PublishedCachesFactoryBase : IPublishedCachesFactory
    {
        private Func<IPublishedCaches> _getPublishedCachesFunc = () => UmbracoContext.Current.PublishedCaches;

        public Func<IPublishedCaches> GetPublishedCachesFunc
        {
            get { return _getPublishedCachesFunc; }
            set
            {
                using (Resolution.Configuration)
                {
                    _getPublishedCachesFunc = value;
                }
            }
        }

        public abstract IPublishedCaches CreatePublishedCaches(string previewToken);

        public IPublishedCaches GetPublishedCaches()
        {
            var caches = _getPublishedCachesFunc();
            if (caches == null)
                throw new Exception("Carrier's caches is null.");
            return caches;
        }


        public abstract string EnterPreview(IUser user, int contentId);
        public abstract void RefreshPreview(string previewToken, int contentId);
        public abstract void ExitPreview(string previewToken);
    }
}
