using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.Serialization;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using AutoMapper;
using Newtonsoft.Json;
using umbraco.cms.businesslogic.web;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Core.Models.Mapping;
using Umbraco.Core.PropertyEditors;
using Umbraco.Core.Services;
using Umbraco.Web.Models.ContentEditing;
using Umbraco.Web.Models.Segments;
using Umbraco.Web.Routing.Segments;
using Umbraco.Web.Trees;
using umbraco;
using Umbraco.Web.Routing;
using umbraco.BusinessLogic.Actions;

namespace Umbraco.Web.Models.Mapping
{
    /// <summary>
    /// Declares how model mappings for content
    /// </summary>
    internal class ContentModelMapper : MapperConfiguration
    {
        public override void ConfigureMappings(IConfiguration config, ApplicationContext applicationContext)
        {

            //FROM IContent TO ContentItemDisplay
            config.CreateMap<IContent, ContentItemDisplay>()
                  .ForMember(
                      dto => dto.Owner,
                      expression => expression.ResolveUsing<OwnerResolver<IContent>>())
                  .ForMember(
                      dto => dto.Updator,
                      expression => expression.ResolveUsing<CreatorResolver>())
                  .ForMember(
                      dto => dto.Icon,
                      expression => expression.MapFrom(content => content.ContentType.Icon))
                  .ForMember(
                      dto => dto.ContentTypeAlias,
                      expression => expression.MapFrom(content => content.ContentType.Alias))
                  .ForMember(
                      dto => dto.ContentTypeName,
                      expression => expression.MapFrom(content => content.ContentType.Name))
                  .ForMember(
                      dto => dto.IsChildOfListView,
                      expression => expression.MapFrom(content => content.Parent().ContentType.IsContainer))
                  .ForMember(
                      dto => dto.PublishDate,
                      expression => expression.MapFrom(content => GetPublishedDate(content, applicationContext.Services.ContentService)))
                  .ForMember(
                      dto => dto.TemplateAlias, expression => expression.MapFrom(content => content.Template.Alias))
                  .ForMember(
                      dto => dto.Urls,
                      expression => expression.MapFrom(content =>
                                                       UmbracoContext.Current == null
                                                           ? new[] {"Cannot generate urls without a current Umbraco Context"}
                                                           : content.GetContentUrls()))
                  .ForMember(display => display.Properties, expression => expression.Ignore())
                  .ForMember(display => display.TreeNodeUrl, expression => expression.Ignore())
                  .ForMember(display => display.Notifications, expression => expression.Ignore())
                  .ForMember(display => display.Errors, expression => expression.Ignore())
                  .ForMember(display => display.Alias, expression => expression.Ignore())
                  .ForMember(display => display.Tabs, expression => expression.ResolveUsing<TabsAndPropertiesResolver>())
                  .ForMember(display => display.AllowedActions, expression => expression.ResolveUsing(
                      new ActionButtonsResolver(new Lazy<IUserService>(() => applicationContext.Services.UserService))))
                  .AfterMap((content, display) => AfterMap(content, display, applicationContext.Services.ContentService));

            //FROM IContent TO ContentItemBasic<ContentPropertyBasic, IContent>
            config.CreateMap<IContent, ContentItemBasic<ContentPropertyBasic, IContent>>()
                .ForMember(
                    dto => dto.Owner,
                    expression => expression.ResolveUsing<OwnerResolver<IContent>>())
                .ForMember(
                    dto => dto.Updator,
                    expression => expression.ResolveUsing<CreatorResolver>())
                .ForMember(
                    dto => dto.Icon,
                    expression => expression.MapFrom(content => content.ContentType.Icon))
                .ForMember(
                    dto => dto.ContentTypeAlias,
                    expression => expression.MapFrom(content => content.ContentType.Alias))
                .ForMember(display => display.Alias, expression => expression.Ignore());

            //FROM IContent TO ContentItemDto<IContent>
            config.CreateMap<IContent, ContentItemDto<IContent>>()
                .ForMember(
                    dto => dto.Owner,
                    expression => expression.ResolveUsing<OwnerResolver<IContent>>())
                .ForMember(display => display.Updator, expression => expression.Ignore())
                .ForMember(display => display.Icon, expression => expression.Ignore())
                .ForMember(display => display.Alias, expression => expression.Ignore());


        }


        /// <summary>
        /// Maps the generic tab with custom properties for content
        /// </summary>
        /// <param name="content"></param>
        /// <param name="display"></param>
        /// <param name="contentService"></param>
        private static void AfterMap(IContent content, ContentItemDisplay display, IContentService contentService)
        {
            //map the tree node url
            if (HttpContext.Current != null)
            {
                var urlHelper = new UrlHelper(new RequestContext(new HttpContextWrapper(HttpContext.Current), new RouteData()));
                var url = urlHelper.GetUmbracoApiService<ContentTreeController>(controller => controller.GetTreeNode(display.Id.ToString(), null));
                display.TreeNodeUrl = url;    
            }

            //fill in the template config to be passed to the template drop down.
            var templateItemConfig = new Dictionary<string, string> { { "", "Choose..." } };
            foreach (var t in content.ContentType.AllowedTemplates)
            {
                templateItemConfig.Add(t.Alias, t.Name);
            }

            if (content.ContentType.IsContainer)
            {
                TabsAndPropertiesResolver.AddContainerView(display, "content");
            }

            TabsAndPropertiesResolver.MapGenericProperties(
                content, display,
                new ContentPropertyDisplay
                    {
                        Alias = string.Format("{0}releasedate", Constants.PropertyEditors.InternalGenericPropertiesPrefix),
                        Label = ui.Text("content", "releaseDate"),
                        Value = display.ReleaseDate.HasValue ? display.ReleaseDate.Value.ToIsoString() : null,
                        View = "datepicker" //TODO: Hard coding this because the templatepicker doesn't necessarily need to be a resolvable (real) property editor
                    },
                new ContentPropertyDisplay
                    {
                        Alias = string.Format("{0}expiredate", Constants.PropertyEditors.InternalGenericPropertiesPrefix),
                        Label = ui.Text("content", "unpublishDate"),
                        Value = display.ExpireDate.HasValue ? display.ExpireDate.Value.ToIsoString() : null,
                        View = "datepicker" //TODO: Hard coding this because the templatepicker doesn't necessarily need to be a resolvable (real) property editor
                    },
                new ContentPropertyDisplay
                    {
                        Alias = string.Format("{0}template", Constants.PropertyEditors.InternalGenericPropertiesPrefix),
                        Label = "Template", //TODO: localize this?
                        Value = display.TemplateAlias,
                        View = "dropdown", //TODO: Hard coding until we make a real dropdown property editor to lookup
                        Config = new Dictionary<string, object>
                            {
                                {"items", templateItemConfig}
                            }
                    },
                new ContentPropertyDisplay
                    {
                        Alias = string.Format("{0}urls", Constants.PropertyEditors.InternalGenericPropertiesPrefix),
                        Label = ui.Text("content", "urls"),
                        Value = string.Join(",", display.Urls),
                        View = "urllist" //TODO: Hard coding this because the templatepicker doesn't necessarily need to be a resolvable (real) property editor
                    });

            MapVariants(content, display, contentService);
        }

        /// <summary>
        /// Gets the published date value for the IContent object
        /// </summary>
        /// <param name="content"></param>
        /// <param name="contentService"></param>
        /// <returns></returns>
        private static DateTime? GetPublishedDate(IContent content, IContentService contentService)
        {
            if (content.Published)
            {
                return content.UpdateDate;
            }
            if (content.HasPublishedVersion())
            {
                var published = contentService.GetPublishedVersion(content.Id);
                return published.UpdateDate;
            }
            return null;
        }

        /// <summary>
        /// Creates the list of action buttons allowed for this user - Publish, Send to publish, save, unpublish returned as the button's 'letter'
        /// </summary>
        private class ActionButtonsResolver : ValueResolver<IContent, IEnumerable<char>>
        {
            private readonly Lazy<IUserService> _userService;

            public ActionButtonsResolver(Lazy<IUserService> userService)
            {
                _userService = userService;
            }

            protected override IEnumerable<char> ResolveCore(IContent source)
            {
                if (UmbracoContext.Current == null)
                {
                    //cannot check permissions without a context
                    return Enumerable.Empty<char>();
                }
                var svc = _userService.Value;

                var permissions = svc.GetPermissions(UmbracoContext.Current.Security.CurrentUser, source.Id)
                                              .FirstOrDefault();
                if (permissions == null)
                {
                    return Enumerable.Empty<char>();
                }

                var result = new List<char>();

                //can they publish ?
                if (permissions.AssignedPermissions.Contains(ActionPublish.Instance.Letter.ToString(CultureInfo.InvariantCulture)))
                {
                    result.Add(ActionPublish.Instance.Letter);
                }
                //can they send to publish ?
                if (permissions.AssignedPermissions.Contains(ActionToPublish.Instance.Letter.ToString(CultureInfo.InvariantCulture)))
                {
                    result.Add(ActionToPublish.Instance.Letter);
                }
                //can they save ?
                if (permissions.AssignedPermissions.Contains(ActionUpdate.Instance.Letter.ToString(CultureInfo.InvariantCulture)))
                {
                    result.Add(ActionUpdate.Instance.Letter);
                }
                //can they create ?
                if (permissions.AssignedPermissions.Contains(ActionNew.Instance.Letter.ToString(CultureInfo.InvariantCulture)))
                {
                    result.Add(ActionNew.Instance.Letter);
                }

                return result;
            }
        }

        //TODO: This uses other services which will need to be wired up in order for this mapper to execute, create overloaded ctor's so we ca
        // test or inject these services.

        private static void MapVariants(IContent content, ContentItemDisplay display, IContentService contentService)
        {
            if (content.HasIdentity == false) return;

            //if it's not a variant - go get it's variants
            if (content.VariantInfo.IsVariant == false)
            {
                var variantDef = contentService.GetByIds(content.VariantInfo.VariantIds);

                //if it's not a variant, then it's a master-doc so go lookup the possible variant types it can have
                var segmentProviderStatus = ContentSegmentProvidersStatus.GetProviderStatus();

                var assignableVariants = ContentSegmentProviderResolver.Current.GetAssignableVariants(segmentProviderStatus);

                //These are the assignable variants based on the installed providers (statically advertised variants)
                // that are enabled via the back office. If they are not enabled, they will not show up.
                var assignableSegments = assignableVariants
                    .Select(variantAttribute => new
                    {
                        variantAttribute,
                        assigned = variantDef.FirstOrDefault(k => k.VariantInfo.Key == variantAttribute.SegmentMatchKey)
                    })
                    .Select(x => x.assigned == null
                        ? new ContentVariableSegment(x.variantAttribute.VariantName, x.variantAttribute.SegmentMatchKey, false)
                        : new ContentVariableSegment(x.variantAttribute.VariantName, x.variantAttribute.SegmentMatchKey, false, x.assigned.Id, x.assigned.Trashed, x.assigned.UpdateDate));

                var assignedLanguages = GetAssignedLanguageVariants(display);

                var languageSegments = assignedLanguages
                    .Select(x => new { lang = x, assigned = variantDef.FirstOrDefault(k => k.VariantInfo.Key == x) })
                    .Select(x => x.assigned == null
                        ? new ContentVariableSegment(x.lang, true)
                        : new ContentVariableSegment(x.lang, true, x.assigned.Id, x.assigned.Trashed, x.assigned.UpdateDate));

                //assign the variants, NOTE: languages always take precedence if there is overlap
                display.ContentVariants = languageSegments.Union(assignableSegments);
            }
            else
            {
                display.MasterDocId = content.VariantInfo.MasterDocId;
                display.VariantKey = content.VariantInfo.Key;

                //We want to change the URL property because it shouldn't show urls if it's a variant, the URL will be specific
                // to the master doc - if it is NOT a language variant.
                //For language variants the URL should only reflect the one assigned by domain. 

                var genericTab = display.Tabs.Single(x => x.Id == 0);
                var urlProp = genericTab.Properties.Single(x => x.Alias == string.Format("{0}urls", Constants.PropertyEditors.InternalGenericPropertiesPrefix));

                var assignedLanguages = GetAssignedLanguageVariants(display);
                if (assignedLanguages.Contains(content.VariantInfo.Key) == false)
                {
                    //it's not a language, so remove the url

                    //TODO: show a message? or just remove the prop?
                    //var labelEditor = PropertyEditorResolver.Current.GetByAlias(Constants.PropertyEditors.NoEditAlias).ValueEditor.View;                    
                    genericTab.Properties = genericTab.Properties.Except(new[] {urlProp});
                    //urlProp.Value = "The URL is the same as the master doc, custom variants do not have different URLs";
                    //urlProp.Label = "";
                    //urlProp.View = labelEditor;
                }
                else
                {
                    //it is a language so only show that specific domain URL.
                    //TODO: How??? There doesn't seem to be an easy way to do this need to ask Stephen
                    //urlProp.Value = content.GetContentUrls();
                 
                }
            }
        }

        private static IEnumerable<string> GetAssignedLanguageVariants(ContentItemDisplay display)
        {
            //These are the lanuages assigned to this node (i.e. based on domains assigned to this node or ancestor nodes)
            var allDomains = DomainHelper.GetAllDomains(false);

            //now get the ones assigned within the path
            var splitPath = display.Path.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries).ToList();

            if (display.MasterDocId.HasValue)
            {
                //we need to add the master doc id to the path, since that is really where the languages are assigned
                splitPath.Insert(1, display.MasterDocId.Value.ToString(CultureInfo.InvariantCulture));
            }
            
            var assignedDomains = allDomains.Where(x => splitPath.Contains(x.RootNodeId.ToString(CultureInfo.InvariantCulture)));

            return assignedDomains.Select(x => x.Language.CultureAlias);
        } 

    }
}