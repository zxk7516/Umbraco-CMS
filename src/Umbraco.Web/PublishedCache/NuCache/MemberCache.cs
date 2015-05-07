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
    class MemberCache : IPublishedMemberCache, INavigableData
    {
        private readonly IMemberService _memberService;
        private readonly IDataTypeService _dataTypeService;
        private readonly PublishedContentTypeCache _contentTypeCache;

        public MemberCache(IMemberService memberService, IDataTypeService dataTypeService, PublishedContentTypeCache contentTypeCache)
        {
            _memberService = memberService;
            _dataTypeService = dataTypeService;
            _contentTypeCache = contentTypeCache;
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

                    var result = _memberService.GetByProviderKey(key);
                    if (result == null) return null;
                    var type = _contentTypeCache.Get(PublishedItemType.Member, result.ContentTypeId); 
                    return new PublishedMember(result, type).CreateModel();
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

                    var result = _memberService.GetById(memberId);
                    if (result == null) return null;
                    var type = _contentTypeCache.Get(PublishedItemType.Member, result.ContentTypeId);
                    return new PublishedMember(result, type).CreateModel();
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

                    var result = _memberService.GetByUsername(username);
                    if (result == null) return null;
                    var type = _contentTypeCache.Get(PublishedItemType.Member, result.ContentTypeId);
                    return new PublishedMember(result, type).CreateModel();
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

                    var result = _memberService.GetByEmail(email);
                    if (result == null) return null;
                    var type = _contentTypeCache.Get(PublishedItemType.Member, result.ContentTypeId);
                    return new PublishedMember(result, type).CreateModel();
                });
        }

        public IPublishedContent GetByMember(IMember member)
        {
            var type = _contentTypeCache.Get(PublishedItemType.Member, member.ContentTypeId);
            return new PublishedMember(member, type).CreateModel();
        }

        public IEnumerable<IPublishedContent> GetAtRoot(bool preview)
        {
            // because members are flat (not a tree) everything is at root
            // because we're loading everything... let's just not cache?
            var members = _memberService.GetAllMembers();
            return members.Select(m =>
            {
                var type = _contentTypeCache.Get(PublishedItemType.Member, m.ContentTypeId);
                return (new PublishedMember(m, type)).CreateModel();
            });
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
