using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.XPath;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Models;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Core.Security;
using Umbraco.Core.Services;
using Umbraco.Core.Xml.XPath;
using Umbraco.Web.PublishedCache.NuCache.Navigable;
using Umbraco.Web.Security;

namespace Umbraco.Web.PublishedCache.NuCache
{
    // fixme - this is NOT very efficient...
    // fixme - NOT managing PREVIEW here?!
    // caching in the FacadeCache is OK but then...
    // we should INDEX them not create several entries for several members?

    class MemberCache : IPublishedMemberCache, INavigableData
    {
        private readonly IMemberService _memberService;
        private readonly IDataTypeService _dataTypeService;
        private readonly PublishedContentTypeCache _contentTypeCache;
        private readonly bool _previewDefault;

        public MemberCache(bool previewDefault, IMemberService memberService, IDataTypeService dataTypeService, PublishedContentTypeCache contentTypeCache)
        {
            _memberService = memberService;
            _dataTypeService = dataTypeService;
            _contentTypeCache = contentTypeCache;
            _previewDefault = previewDefault;
        }

        private static T GetCacheItem<T>(string cacheKey, Func<T> getCacheItem)
        {
            var facade = Facade.Current;
            var cache = facade == null ? null : facade.FacadeCache;
            return cache == null
                ? getCacheItem()
                : cache.GetCacheItem(cacheKey, getCacheItem);
        }

        public IPublishedContent GetByProviderKey(object key)
        {
            return GetCacheItem(
                GetCacheKey("GetByProviderKey", key), () =>
                {
                    var provider = Core.Security.MembershipProviderExtensions.GetMembersMembershipProvider();
                    if (provider.IsUmbracoMembershipProvider() == false)
                    {
                        throw new NotSupportedException("Cannot access this method unless the Umbraco membership provider is active");
                    }

                    var member = _memberService.GetByProviderKey(key);
                    return member == null ? null : PublishedMember.Create(member, _contentTypeCache.Get(PublishedItemType.Member, member.ContentTypeId));
                });
        }

        public IPublishedContent GetById(bool preview, int memberId)
        {
            return GetById(memberId);
        }

        public IPublishedContent GetById(int memberId)
        {
            return GetCacheItem(
                GetCacheKey("GetById", memberId), () =>
                {
                    var provider = Core.Security.MembershipProviderExtensions.GetMembersMembershipProvider();
                    if (provider.IsUmbracoMembershipProvider() == false)
                    {
                        throw new NotSupportedException("Cannot access this method unless the Umbraco membership provider is active");
                    }

                    var member = _memberService.GetById(memberId);
                    return member == null ? null : PublishedMember.Create(member, _contentTypeCache.Get(PublishedItemType.Member, member.ContentTypeId));
                });
        }

        public IPublishedContent GetByUsername(string username)
        {
            return GetCacheItem(
                GetCacheKey("GetByUsername", username), () =>
                {
                    var provider = Core.Security.MembershipProviderExtensions.GetMembersMembershipProvider();
                    if (provider.IsUmbracoMembershipProvider() == false)
                    {
                        throw new NotSupportedException("Cannot access this method unless the Umbraco membership provider is active");
                    }

                    var member = _memberService.GetByUsername(username);
                    return member == null ? null : PublishedMember.Create(member, _contentTypeCache.Get(PublishedItemType.Member, member.ContentTypeId));
                });
        }

        public IPublishedContent GetByEmail(string email)
        {
            return GetCacheItem(
                GetCacheKey("GetByEmail", email), () =>
                {
                    var provider = Core.Security.MembershipProviderExtensions.GetMembersMembershipProvider();
                    if (provider.IsUmbracoMembershipProvider() == false)
                    {
                        throw new NotSupportedException("Cannot access this method unless the Umbraco membership provider is active");
                    }

                    var member = _memberService.GetByEmail(email);
                    return member == null ? null : PublishedMember.Create(member, _contentTypeCache.Get(PublishedItemType.Member, member.ContentTypeId));
                });
        }

        public IPublishedContent GetByMember(IMember member)
        {
            return PublishedMember.Create(member, _contentTypeCache.Get(PublishedItemType.Member, member.ContentTypeId));
        }

        public IEnumerable<IPublishedContent> GetAtRoot(bool preview)
        {
            // because members are flat (not a tree) everything is at root
            // because we're loading everything... let's just not cache?
            var members = _memberService.GetAllMembers();
            return members.Select(m => PublishedMember.Create(m, _contentTypeCache.Get(PublishedItemType.Member, m.ContentTypeId)));
        }

        public XPathNavigator CreateNavigator()
        {
            var source = new Source(this, false);
            var navigator = new NavigableNavigator(source);
            return navigator;
        }

        public XPathNavigator CreateNavigator(bool preview)
        {
            return CreateNavigator();
        }

        public XPathNavigator CreateNodeNavigator(int id, bool preview)
        {
            var provider = Core.Security.MembershipProviderExtensions.GetMembersMembershipProvider();
            if (provider.IsUmbracoMembershipProvider() == false)
            {
                throw new NotSupportedException("Cannot access this method unless the Umbraco membership provider is active");
            }

            var result = _memberService.GetById(id);
            if (result == null) return null;

            var exs = new EntityXmlSerializer();
            var s = exs.Serialize(_dataTypeService, result);
            var n = s.GetXmlNode();
            return n.CreateNavigator();
        }

        private static string GetCacheKey(string key, params object[] additional)
        {
            var sb = new StringBuilder(string.Format("{0}-{1}", typeof(MembershipHelper).Name, key));
            foreach (var s in additional)
            {
                sb.Append("-");
                sb.Append(s);
            }
            return sb.ToString();
        }

        #region Content types

        public PublishedContentType GetContentType(int id)
        {
            return _contentTypeCache.Get(PublishedItemType.Member, id);
        }

        public PublishedContentType GetContentType(string alias)
        {
            return _contentTypeCache.Get(PublishedItemType.Member, alias);
        }

        #endregion
    }
}
