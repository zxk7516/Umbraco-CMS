using AutoMapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using Umbraco.Core;
using Umbraco.Core.IO;
using Umbraco.Core.Models;
using Umbraco.Core.PropertyEditors;
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
                Id = x.GetUdi(),
                Name = x.Name,
                Alias = x.Alias,
                Icon = x.Icon,
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

        public IDictionary<string, GridEditorPath> GetPaths(IContentType contentType)
        {
            if (contentType.PropertyGroups.Count == 0)
                throw new InvalidOperationException($"The content type {contentType.Alias} does not contain any tabs/properties");

            var props = contentType.PropertyGroups[0].PropertyTypes;

            var result = new Dictionary<string, GridEditorPath>();

            var editors = props.Select(x => _propertyEditors.FirstOrDefault(p => p.Alias == x.PropertyEditorAlias))
                .WhereNotNull()
                .ToDictionary(x => x.Alias, x => x);

            foreach (var x in props)
            {
                if (editors.TryGetValue(x.PropertyEditorAlias, out var editor))
                {
                    var valueEditor = editor.GetValueEditor();
                    if (valueEditor == null) continue;

                    if (valueEditor.View.IsNullOrWhiteSpace()) continue;

                    var inlineResult = GetPath(valueEditor, "inline");
                    if (inlineResult)
                        result[x.Alias] = new GridEditorPath(inlineResult.Result, false);
                    else
                    {
                        var previewResult = GetPath(valueEditor, "preview");
                        if (previewResult)
                            result[x.Alias] = new GridEditorPath(previewResult.Result, true);
                    }
                }
            }

            return result;
        }

        private Attempt<string> GetPath(IDataValueEditor valueEditor, string suffix)
        {
            var inlinePath = valueEditor.View.InvariantEndsWith(".html")
                        ? valueEditor.View.TrimEnd(".html") + $".{suffix}.html"
                        : valueEditor.View + $".{suffix}.html";

            var relativePath = !inlinePath.Contains("/") ? $"views/propertyeditors/{valueEditor.View}/{inlinePath}" : inlinePath;
            var fullPath = !inlinePath.Contains("/") ? $"~{GlobalSettings.Path}/{relativePath}" : relativePath;

            var file = IOHelper.MapPath(fullPath);

            if (!System.IO.File.Exists(file))
                return Attempt<string>.Fail();

            return Attempt.Succeed(relativePath);
        }
    }
}
