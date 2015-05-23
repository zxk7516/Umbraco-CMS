using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core.Models;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Web.PublishedCache.NuCache.DataSource;

namespace Umbraco.Web.PublishedCache.NuCache
{
    // note
    // the whole PublishedMember thing should be refactored because as soon as a member
    // is wrapped on in a model, the inner IMember and all associated properties are lost

    class PublishedMember : PublishedContent //, IPublishedMember
    {
        private readonly IMember _member;

        private PublishedMember(IMember member, ContentNode contentNode, ContentData contentData)
            : base(contentNode, contentData)
        {
            _member = member;
        }

        public static PublishedMember Create(IMember member, PublishedContentType contentType, bool previewing)
        {
            var d = new ContentData
            {
                Name = member.Name,
                Published = previewing,
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
                member.CreateDate, member.CreatorId);
            return new PublishedMember(member, n, d);
        }

        private static Dictionary<string, object> GetPropertyValues(IContentBase content)
        {
            // see node in FacadeService
            // we do not (want to) support ConvertDbToXml/String

            //var propertyEditorResolver = PropertyEditorResolver.Current;

            return content
                .Properties
                //.Select(property =>
                //{
                //    var e = propertyEditorResolver.GetByAlias(property.PropertyType.PropertyEditorAlias);
                //    var v = e == null
                //        ? property.Value
                //        : e.ValueEditor.ConvertDbToString(property, property.PropertyType, ApplicationContext.Current.Services.DataTypeService);
                //    return new KeyValuePair<string, object>(property.Alias, v);
                //})
                //.ToDictionary(x => x.Key, x => x.Value);
                .ToDictionary(x => x.Alias, x => x.Value);
        }

        #region IPublishedMember

        public IMember Member
        {
            get { return _member; }
        }

        public string Email
        {
            get { return _member.Email; }
        }

        public string UserName
        {
            get { return _member.Username; }
        }

        public string PasswordQuestion
        {
            get { return _member.PasswordQuestion; }
        }

        public string Comments
        {
            get { return _member.Comments; }
        }

        public bool IsApproved
        {
            get { return _member.IsApproved; }
        }

        public bool IsLockedOut
        {
            get { return _member.IsLockedOut; }
        }

        public DateTime LastLockoutDate
        {
            get { return _member.LastLockoutDate; }
        }

        public DateTime CreationDate
        {
            get { return _member.CreateDate; }
        }

        public DateTime LastLoginDate
        {
            get { return _member.LastLoginDate; }
        }

        public DateTime LastActivityDate
        {
            get { return _member.LastLoginDate; }
        }

        public DateTime LastPasswordChangedDate
        {
            get { return _member.LastPasswordChangeDate; }
        }

        #endregion
    }
}
