using System;
using System.Text;
using System.Xml.XPath;
using Umbraco.Core.Cache;
using Umbraco.Core.Models;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Core.Security;
using Umbraco.Core.Services;
using Umbraco.Web.Security;

namespace Umbraco.Web.PublishedCache.XmlPublishedCache
{
    class PublishedMemberCache : IPublishedMemberCache
    {
        private readonly IMemberService _memberService;
        private readonly ICacheProvider _requestCache;
        private readonly XmlStore _xmlStore;

        public PublishedMemberCache(XmlStore xmlStore, ICacheProvider requestCacheProvider, IMemberService memberService)
        {
            _requestCache = requestCacheProvider;
            _memberService = memberService;
            _xmlStore = xmlStore;
        }

        public IPublishedContent GetByProviderKey(object key)
        {
            return _requestCache.GetCacheItem<IPublishedContent>(
                GetCacheKey("GetByProviderKey", key), () =>
                {
                    var provider = Core.Security.MembershipProviderExtensions.GetMembersMembershipProvider();
                    if (provider.IsUmbracoMembershipProvider() == false)
                    {
                        throw new NotSupportedException("Cannot access this method unless the Umbraco membership provider is active");
                    }

                    var result = _memberService.GetByProviderKey(key);
                    return result == null ? null : new PublishedMember(result).CreateModel();
                });
        }

        public IPublishedContent GetById(int memberId)
        {
            return _requestCache.GetCacheItem<IPublishedContent>(
                GetCacheKey("GetById", memberId), () =>
                {
                    var provider = Core.Security.MembershipProviderExtensions.GetMembersMembershipProvider();
                    if (provider.IsUmbracoMembershipProvider() == false)
                    {
                        throw new NotSupportedException("Cannot access this method unless the Umbraco membership provider is active");
                    }

                    var result = _memberService.GetById(memberId);
                    return result == null ? null : new PublishedMember(result).CreateModel();
                });
        }

        public IPublishedContent GetByUsername(string username)
        {
            return _requestCache.GetCacheItem<IPublishedContent>(
                GetCacheKey("GetByUsername", username), () =>
                {
                    var provider = Core.Security.MembershipProviderExtensions.GetMembersMembershipProvider();
                    if (provider.IsUmbracoMembershipProvider() == false)
                    {
                        throw new NotSupportedException("Cannot access this method unless the Umbraco membership provider is active");
                    }

                    var result = _memberService.GetByUsername(username);
                    return result == null ? null : new PublishedMember(result).CreateModel();
                });
        }

        public IPublishedContent GetByEmail(string email)
        {
            return _requestCache.GetCacheItem<IPublishedContent>(
                GetCacheKey("GetByEmail", email), () =>
                {
                    var provider = Core.Security.MembershipProviderExtensions.GetMembersMembershipProvider();
                    if (provider.IsUmbracoMembershipProvider() == false)
                    {
                        throw new NotSupportedException("Cannot access this method unless the Umbraco membership provider is active");
                    }

                    var result = _memberService.GetByEmail(email);
                    return result == null ? null : new PublishedMember(result).CreateModel();
                });
        }

        public IPublishedContent GetByMember(IMember member)
        {
            return new PublishedMember(member).CreateModel();
        }

        public XPathNavigator CreateNodeNavigator(int id, bool preview)
        {
            var n = _xmlStore.GetMemberXmlNode(id);
            return n == null ? null : n.CreateNavigator();
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
    }
}
