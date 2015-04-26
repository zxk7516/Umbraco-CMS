using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.XPath;
using ClientDependency.Core;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Models;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Core.Security;
using Umbraco.Core.Services;
using Umbraco.Core.Xml.XPath;
using Umbraco.Web.PublishedCache.PublishedNoCache.Navigable;
using Umbraco.Web.Security;

namespace Umbraco.Web.PublishedCache.PublishedNoCache
{
    class PublishedMemberCache : IPublishedMemberCache, INavigableData
    {
        private readonly IDataTypeService _dataTypeService;
        private readonly IMemberService _memberService;
        private readonly IMemberTypeService _memberTypeService;
        private readonly IContentTypeService _contentTypeService;

        public PublishedMemberCache(IDataTypeService dataTypeService, IMemberService memberService, IContentTypeService contentTypeService)
        {
            _dataTypeService = dataTypeService;
            _memberService = memberService;
            _contentTypeService = contentTypeService;
        }

        public IPublishedContent GetByProviderKey(object key)
        {
            var provider = Core.Security.MembershipProviderExtensions.GetMembersMembershipProvider();
            if (provider.IsUmbracoMembershipProvider() == false)
            {
                throw new NotSupportedException("Cannot access this method unless the Umbraco membership provider is active");
            }

            var result = _memberService.GetByProviderKey(key);
            if (result == null) return null;
            var type = new PublishedContentType(_memberTypeService.Get(result.Id));
            return new PublishedMember(result, type).CreateModel();
        }

        public IPublishedContent GetById(bool preview, int memberId)
        {
            return GetById(memberId);
        }

        public IPublishedContent GetById(int memberId)
        {
            var provider = Core.Security.MembershipProviderExtensions.GetMembersMembershipProvider();
            if (provider.IsUmbracoMembershipProvider() == false)
            {
                throw new NotSupportedException("Cannot access this method unless the Umbraco membership provider is active");
            }

            var result = _memberService.GetById(memberId);
            if (result == null) return null;
            var type = new PublishedContentType(_memberTypeService.Get(result.Id));
            return new PublishedMember(result, type).CreateModel();
        }

        public IPublishedContent GetByUsername(string username)
        {
            var provider = Core.Security.MembershipProviderExtensions.GetMembersMembershipProvider();
            if (provider.IsUmbracoMembershipProvider() == false)
            {
                throw new NotSupportedException("Cannot access this method unless the Umbraco membership provider is active");
            }

            var result = _memberService.GetByUsername(username);
            if (result == null) return null;
            var type = new PublishedContentType(_memberTypeService.Get(result.Id));
            return new PublishedMember(result, type).CreateModel();
        }

        public IPublishedContent GetByEmail(string email)
        {
            var provider = Core.Security.MembershipProviderExtensions.GetMembersMembershipProvider();
            if (provider.IsUmbracoMembershipProvider() == false)
            {
                throw new NotSupportedException("Cannot access this method unless the Umbraco membership provider is active");
            }

            var result = _memberService.GetByEmail(email);
            if (result == null) return null;
            var type = new PublishedContentType(_memberTypeService.Get(result.Id));
            return new PublishedMember(result, type).CreateModel();
        }

        public IPublishedContent GetByMember(IMember member)
        {
            var type = new PublishedContentType(_memberTypeService.Get(member.ContentTypeId));
            return new PublishedMember(member, type).CreateModel();
        }

        public IEnumerable<IPublishedContent> GetAtRoot(bool preview)
        {
            // because members are flat (not a tree) everything is at root
            var members = _memberService.GetAllMembers().ToArray();
            var types = members
                .Select(x => x.ContentTypeId)
                .Distinct()
                .Select(x => new PublishedContentType(_memberTypeService.Get(x)))
                .ToDictionary(x => x.Id);
            return members.Select(m => (new PublishedMember(m, types[m.ContentTypeId])).CreateModel());
        }

        public XPathNavigator CreateNavigator()
        {
            var source = new Navigable.Source(this, false);
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

        #region Content types

        public PublishedContentType GetContentType(int id)
        {
            var contentType = _contentTypeService.Get(id);
            return contentType == null ? null : new PublishedContentType(contentType);
        }

        public PublishedContentType GetContentType(string alias)
        {
            var contentType = _contentTypeService.Get(alias);
            return contentType == null ? null : new PublishedContentType(contentType);
        }

        #endregion
    }
}
