using AutoMapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using Umbraco.Core;
using Umbraco.Core.Configuration;
using Umbraco.Core.IO;
using Umbraco.Core.Models;
using Umbraco.Core.PropertyEditors;
using Umbraco.Core.Services;
using Umbraco.Web.Models.ContentEditing;

namespace Umbraco.Web.Editors
{
    public class GridController : UmbracoAuthorizedJsonController
    {
        private readonly PropertyEditorCollection _propertyEditors;

        public GridController(PropertyEditorCollection propertyEditors)
        {
            _propertyEditors = propertyEditors;
        }

        public IEnumerable<GridContentType> GetContentTypes()
        {
            //fixme - the content types returned will need to come from the grid data type config
            var folder = Services.ContentTypeService.GetContainers("GridEditors", 1).ToList();
            if (folder.Count == 0) return Enumerable.Empty<GridContentType>();

            var contentTypes = Services.ContentTypeService.GetChildren(folder[0].Id);

            return contentTypes.Select(x => new GridContentType
            {
                Alias = x.Alias,
                Icon = x.Icon,
                Id = x.Id,
                Key = x.Key,
                Name = x.Name,
                Views = GetPaths(x)
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

        private string[] GetPaths(IContentType contentType)
        {
            if (contentType.PropertyGroups.Count == 0)
                throw new InvalidOperationException($"The content type {contentType.Alias} does not contain any tabs/properties");

            var props = contentType.PropertyGroups[0].PropertyTypes;

            var editors = props.Select(x => _propertyEditors.FirstOrDefault(p => p.Alias == x.PropertyEditorAlias)).WhereNotNull();

            var views = editors
                .Select(x =>
                {
                    var valueEditor = x.GetValueEditor();
                    if (valueEditor == null) return null;

                    if (valueEditor.View.IsNullOrWhiteSpace()) return null;

                    var path = valueEditor.View.InvariantEndsWith(".html")
                        ? valueEditor.View.TrimEnd(".html") + ".inline.html"
                        : valueEditor.View + ".inline.html";

                    var relativePath = !path.Contains("/") ? $"views/propertyeditors/{valueEditor.View}/{path}" : path;
                    var fullPath = !path.Contains("/") ? $"~{GlobalSettings.Path}/{relativePath}" : relativePath;

                    var file = IOHelper.MapPath(fullPath);
                    return System.IO.File.Exists(file) ? relativePath : null;
                })
                .WhereNotNull();

            return views.ToArray();

        }
    }
}
