using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Core.Models.Membership;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Core.PropertyEditors;
using Umbraco.Web.PublishedCache.NuCache.DataSource;

namespace Umbraco.Web.PublishedCache.NuCache
{
    // fixme - the whole concept is borked
    // because as soon as we wrap... we're dead
    // what is the cache returning? PublishedContent or PublishedMember?!
    // plus soon as a model is created for a member... all it's native properties are lost?!!!

    class PublishedMember : PublishedContent
    {
        //private readonly IMember _member;
        private readonly IMembershipUser _membershipUser;

        public PublishedMember(IMember member, ContentNode contentNode, ContentData contentData)
            : base(contentNode, contentData)
        {
            //_member = member;
            _membershipUser = member;
        }

        public static PublishedMember Create(IMember member, PublishedContentType contentType)
        {
            var d = new ContentData
            {
                Name = member.Name,
                Published = true,
                TemplateId = -1,
                Version = member.Version,
                VersionDate = member.UpdateDate,
                WriterId = member.CreatorId, // what else?
                Properties = GetPropertyValues(member)
            };
            var n = new ContentNode(member.Id, member.Key,
                contentType,
                member.Level, member.Path, member.SortOrder,
                member.ParentId,
                member.CreateDate, member.CreatorId); // fixme - dirty
            return new PublishedMember(member, n, d);
        }

        // fixme temp
        private static Dictionary<string, object> GetPropertyValues(IContentBase content)
        {
            var propertyEditorResolver = PropertyEditorResolver.Current; // FIXME inject

            return content
                .Properties
                .Select(property =>
                {
                    var e = propertyEditorResolver.GetByAlias(property.PropertyType.PropertyEditorAlias);
                    var v = e == null
                        ? property.Value
                        : e.ValueEditor.ConvertDbToString(property, property.PropertyType, ApplicationContext.Current.Services.DataTypeService); // FIXME inject
                    return new KeyValuePair<string, object>(property.Alias, v);
                })
                .ToDictionary(x => x.Key, x => x.Value);
        }

        // fixme - 

        #region Membership provider member properties

        public string Email
        {
            get { return _membershipUser.Email; }
        }

        public string UserName
        {
            get { return _membershipUser.Username; }
        }

        public string PasswordQuestion
        {
            get { return _membershipUser.PasswordQuestion; }
        }

        public string Comments
        {
            get { return _membershipUser.Comments; }
        }

        public bool IsApproved
        {
            get { return _membershipUser.IsApproved; }
        }

        public bool IsLockedOut
        {
            get { return _membershipUser.IsLockedOut; }
        }

        public DateTime LastLockoutDate
        {
            get { return _membershipUser.LastLockoutDate; }
        }

        public DateTime CreationDate
        {
            get { return _membershipUser.CreateDate; }
        }

        public DateTime LastLoginDate
        {
            get { return _membershipUser.LastLoginDate; }
        }

        public DateTime LastActivityDate
        {
            get { return _membershipUser.LastLoginDate; }
        }

        public DateTime LastPasswordChangedDate
        {
            get { return _membershipUser.LastPasswordChangeDate; }
        }

        #endregion
    }
}
