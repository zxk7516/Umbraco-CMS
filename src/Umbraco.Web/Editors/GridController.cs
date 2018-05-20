using AutoMapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using Umbraco.Core.Configuration;
using Umbraco.Core.IO;
using Umbraco.Core.Models;
using Umbraco.Core.Services;
using Umbraco.Web.Models.ContentEditing;

namespace Umbraco.Web.Editors
{
    public class GridController : UmbracoAuthorizedJsonController
    {
        public IEnumerable<GridContentType> GetContentTypes()
        {
            //fixme - the content types returned will need to come from the grid data type config
            var contentTypes = Services.ContentTypeService.GetAll();

            return contentTypes.Select(x => new GridContentType
            {
                Alias = x.Alias,
                Icon = x.Icon,
                Id = x.Id,
                Key = x.Key,
                Name = x.Name,
                View = GetViewPath(x)
            });
        }

        public GridContentCell GetScaffold(Guid guid)
        {
            var contentType = Services.ContentTypeService.Get(guid);
            if (contentType == null) throw new HttpResponseException(System.Net.HttpStatusCode.NotFound);

            var emptyContent = Services.ContentService.Create("", -1, contentType.Alias, Security.GetUserId().ResultOr(0));
            var mapped = Mapper.Map<GridContentCell>(emptyContent);

            //remove this tab if it exists: umbContainerView
            var containerTab = mapped.Tabs.FirstOrDefault(x => x.Alias == Core.Constants.Conventions.PropertyGroups.ListViewGroupName);
            mapped.Tabs = mapped.Tabs.Except(new[] { containerTab });            

            return mapped;
        }

        private string GetViewPath(IContentType contentType)
        {
            var alias = contentType.Alias;
            var path = $"views/propertyeditors/grid2/inlineeditors/{alias}.html";
            var file = IOHelper.MapPath($"~{GlobalSettings.Path}/{path}");
            return System.IO.File.Exists(file) ? path : null; 
        }
    }
}
