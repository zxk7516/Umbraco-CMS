using System;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Events;
using Umbraco.Core.Models;
using Umbraco.Core.Models.Membership;
using Umbraco.Core.Persistence.Repositories;
using Umbraco.Core.Services;
using umbraco.BusinessLogic;
using umbraco.cms.businesslogic;
using System.Linq;
using Umbraco.Core.Logging;
using Content = Umbraco.Core.Models.Content;
using ApplicationTree = Umbraco.Core.Models.ApplicationTree;
using DeleteEventArgs = umbraco.cms.businesslogic.DeleteEventArgs;

namespace Umbraco.Web.Cache
{
    /// <summary>
    /// Class which listens to events on business level objects in order to invalidate the cache amongst servers when data changes
    /// </summary>
    public class CacheRefresherEventHandler : ApplicationEventHandler
    {
        protected override void ApplicationStarted(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
        {
            LogHelper.Info<CacheRefresherEventHandler>("Initializing Umbraco internal event handlers for cache refreshing");

            // bind to application tree events
            ApplicationTreeService.Deleted += ApplicationTreeDeleted;
            ApplicationTreeService.Updated += ApplicationTreeUpdated;
            ApplicationTreeService.New += ApplicationTreeNew;

            // bind to application events
            SectionService.Deleted += ApplicationDeleted;
            SectionService.New += ApplicationNew;

            // bind to user / user type events
            UserService.SavedUserType += UserServiceSavedUserType;
            UserService.DeletedUserType += UserServiceDeletedUserType;
            UserService.SavedUser += UserServiceSavedUser;
            UserService.DeletedUser += UserServiceDeletedUser;

            // bind to dictionary events
            LocalizationService.DeletedDictionaryItem += LocalizationServiceDeletedDictionaryItem;
            LocalizationService.SavedDictionaryItem += LocalizationServiceSavedDictionaryItem;

            // bind to data type events
            // NOTE: we need to bind to legacy and new API events currently: http://issues.umbraco.org/issue/U4-1979
            DataTypeService.Deleted += DataTypeServiceDeleted;
            DataTypeService.Saved += DataTypeServiceSaved;

            // bind to stylesheet events
            FileService.SavedStylesheet += FileServiceSavedStylesheet;
            FileService.DeletedStylesheet += FileServiceDeletedStylesheet;

            // bind to domain events
            DomainService.Saved += DomainService_Saved;
            DomainService.Deleted += DomainService_Deleted;

            // bind to language events
            LocalizationService.SavedLanguage += LocalizationServiceSavedLanguage;
            LocalizationService.DeletedLanguage += LocalizationServiceDeletedLanguage;

            // bind to content type events
            ContentTypeService.Changed += ContentTypeServiceChanged;
            MediaTypeService.Changed += ContentTypeServiceChanged;
            MemberTypeService.Changed += ContentTypeServiceChanged;

            // bind to permission events
            // TODO: Wrap legacy permissions so we can get rid of this
            Permission.New += PermissionNew;
            Permission.Updated += PermissionUpdated;
            Permission.Deleted += PermissionDeleted;
            PermissionRepository<IContent>.AssignedPermissions += CacheRefresherEventHandler_AssignedPermissions;

            // bind to template events
            FileService.SavedTemplate += FileServiceSavedTemplate;
            FileService.DeletedTemplate += FileServiceDeletedTemplate;

            // bind to macro events
            MacroService.Saved += MacroServiceSaved;
            MacroService.Deleted += MacroServiceDeleted;

            // bind to member events
            MemberService.Saved += MemberServiceSaved;
            MemberService.Deleted += MemberServiceDeleted;
            MemberGroupService.Saved += MemberGroupService_Saved;
            MemberGroupService.Deleted += MemberGroupService_Deleted;

            // bind to media events
            MediaService.TreeChanged += MediaServiceChanged; // handles all media changes

            // bind to content events            
            ContentService.Saved += ContentServiceSaved; // used for permissions
            ContentService.Copied += ContentServiceCopied; // used for permissions
            ContentService.TreeChanged += ContentServiceChanged; // handles all content changes

            // ContentService.Changed will queue things in a ChangeSet, and when that
            // ChangeSet is committed we need to send all the events to the distributed
            // cache as one message (that will be processed sort-of atomically)
            ChangeSet.Flushed += ChangeSetFlushed;

            // public access events
            PublicAccessService.Saved += PublicAccessService_Saved;
        }

        // clear all events - for tests purposes
        // make sure that ALL events registered above are cleared
        internal void ClearEvents()
        {
            ApplicationTreeService.Deleted -= ApplicationTreeDeleted;
            ApplicationTreeService.Updated -= ApplicationTreeUpdated;
            ApplicationTreeService.New -= ApplicationTreeNew;

            SectionService.Deleted -= ApplicationDeleted;
            SectionService.New -= ApplicationNew;

            UserService.SavedUserType -= UserServiceSavedUserType;
            UserService.DeletedUserType -= UserServiceDeletedUserType;
            UserService.SavedUser -= UserServiceSavedUser;
            UserService.DeletedUser -= UserServiceDeletedUser;

            LocalizationService.DeletedDictionaryItem -= LocalizationServiceDeletedDictionaryItem;
            LocalizationService.SavedDictionaryItem -= LocalizationServiceSavedDictionaryItem;

            DataTypeService.Deleted -= DataTypeServiceDeleted;
            DataTypeService.Saved -= DataTypeServiceSaved;

            FileService.SavedStylesheet -= FileServiceSavedStylesheet;
            FileService.DeletedStylesheet -= FileServiceDeletedStylesheet;

            DomainService.Saved -= DomainService_Saved;
            DomainService.Deleted -= DomainService_Deleted;

            LocalizationService.SavedLanguage -= LocalizationServiceSavedLanguage;
            LocalizationService.DeletedLanguage -= LocalizationServiceDeletedLanguage;

            ContentTypeService.Changed -= ContentTypeServiceChanged;
            MediaTypeService.Changed -= ContentTypeServiceChanged;
            MemberTypeService.Changed -= ContentTypeServiceChanged;

            Permission.New -= PermissionNew;
            Permission.Updated -= PermissionUpdated;
            Permission.Deleted -= PermissionDeleted;
            PermissionRepository<IContent>.AssignedPermissions -= CacheRefresherEventHandler_AssignedPermissions;

            FileService.SavedTemplate -= FileServiceSavedTemplate;
            FileService.DeletedTemplate -= FileServiceDeletedTemplate;

            MacroService.Saved -= MacroServiceSaved;
            MacroService.Deleted -= MacroServiceDeleted;

            MemberService.Saved -= MemberServiceSaved;
            MemberService.Deleted -= MemberServiceDeleted;
            MemberGroupService.Saved -= MemberGroupService_Saved;
            MemberGroupService.Deleted -= MemberGroupService_Deleted;

            MediaService.TreeChanged -= MediaServiceChanged;

            ContentService.Saved -= ContentServiceSaved;
            ContentService.Copied -= ContentServiceCopied;
            ContentService.TreeChanged -= ContentServiceChanged;

            ChangeSet.Flushed -= ChangeSetFlushed;

            PublicAccessService.Saved -= PublicAccessService_Saved;
        }

        private static void ChangeSetFlushed(ChangeSet sender, EventArgs args)
        {
            DistributedCache.Instance.FlushChangeSet(sender);
        }

        #region Public access event handlers

        static void PublicAccessService_Saved(IPublicAccessService sender, SaveEventArgs<PublicAccessEntry> e)
        {
            DistributedCache.Instance.RefreshPublicAccess();
        }

        #endregion

        #region Content service and publishing strategy event handlers

        /// <summary>
        /// Handles cache refreshing for when content is copied
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <remarks>
        /// When an entity is copied new permissions may be assigned to it based on it's parent, if that is the 
        /// case then we need to clear all user permissions cache.
        /// </remarks>
        static void ContentServiceCopied(IContentService sender, CopyEventArgs<IContent> e)
        {
            //check if permissions have changed
            var permissionsChanged = ((Content)e.Copy).WasPropertyDirty("PermissionsChanged");
            if (permissionsChanged)
            {
                DistributedCache.Instance.RefreshAllUserPermissionsCache();
            }
        }

        /// <summary>
        /// Handles cache refreshing for when content is saved (not published)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <remarks>
        /// When an entity is saved we need to notify other servers about the change in order for the Examine indexes to 
        /// stay up-to-date for unpublished content.
        /// 
        /// When an entity is created new permissions may be assigned to it based on it's parent, if that is the 
        /// case then we need to clear all user permissions cache.
        /// </remarks>
        static void ContentServiceSaved(IContentService sender, SaveEventArgs<IContent> e)
        {
            var clearUserPermissions = false;
            e.SavedEntities.ForEach(x =>
            {
                //check if it is new
                if (x.IsNewEntity())
                {
                    //check if permissions have changed
                    var permissionsChanged = ((Content)x).WasPropertyDirty("PermissionsChanged");
                    if (permissionsChanged)
                    {
                        clearUserPermissions = true;                        
                    }    
                }
            });

            if (clearUserPermissions)
            {
                DistributedCache.Instance.RefreshAllUserPermissionsCache();
            }
        }

        private static void ContentServiceChanged(IContentService sender, TreeChange<IContent>.EventArgs args)
        {
            DistributedCache.Instance.RefreshContentCache(args.Changes.ToArray());
        }

        #endregion

        #region ApplicationTree event handlers
        static void ApplicationTreeNew(ApplicationTree sender, EventArgs e)
        {
            DistributedCache.Instance.RefreshAllApplicationTreeCache();
        }

        static void ApplicationTreeUpdated(ApplicationTree sender, EventArgs e)
        {
            DistributedCache.Instance.RefreshAllApplicationTreeCache();
        }

        static void ApplicationTreeDeleted(ApplicationTree sender, EventArgs e)
        {
            DistributedCache.Instance.RefreshAllApplicationTreeCache();
        } 
        #endregion

        #region Application event handlers
        static void ApplicationNew(Section sender, EventArgs e)
        {
            DistributedCache.Instance.RefreshAllApplicationCache();
        }

        static void ApplicationDeleted(Section sender, EventArgs e)
        {
            DistributedCache.Instance.RefreshAllApplicationCache();
        } 
        #endregion

        #region UserType event handlers
        static void UserServiceDeletedUserType(IUserService sender, DeleteEventArgs<IUserType> e)
        {
            e.DeletedEntities.ForEach(x => DistributedCache.Instance.RemoveUserTypeCache(x.Id));
        }

        static void UserServiceSavedUserType(IUserService sender, SaveEventArgs<IUserType> e)
        {
            e.SavedEntities.ForEach(x => DistributedCache.Instance.RefreshUserTypeCache(x.Id));
        }
        
        #endregion
        
        #region Dictionary event handlers

        static void LocalizationServiceSavedDictionaryItem(ILocalizationService sender, SaveEventArgs<IDictionaryItem> e)
        {
            e.SavedEntities.ForEach(x => DistributedCache.Instance.RefreshDictionaryCache(x.Id));
        }

        static void LocalizationServiceDeletedDictionaryItem(ILocalizationService sender, DeleteEventArgs<IDictionaryItem> e)
        {
            e.DeletedEntities.ForEach(x => DistributedCache.Instance.RemoveDictionaryCache(x.Id));
        }

        #endregion

        #region DataType event handlers

        static void DataTypeServiceSaved(IDataTypeService sender, SaveEventArgs<IDataTypeDefinition> e)
        {
            e.SavedEntities.ForEach(x => DistributedCache.Instance.RefreshDataTypeCache(x));
        }

        static void DataTypeServiceDeleted(IDataTypeService sender, DeleteEventArgs<IDataTypeDefinition> e)
        {
            e.DeletedEntities.ForEach(x => DistributedCache.Instance.RemoveDataTypeCache(x));
        }

   
        #endregion

        #region Stylesheet and stylesheet property event handlers
     
        static void FileServiceDeletedStylesheet(IFileService sender, DeleteEventArgs<Stylesheet> e)
        {
            e.DeletedEntities.ForEach(x => DistributedCache.Instance.RemoveStylesheetCache(x));
        }

        static void FileServiceSavedStylesheet(IFileService sender, SaveEventArgs<Stylesheet> e)
        {
            e.SavedEntities.ForEach(x => DistributedCache.Instance.RefreshStylesheetCache(x));
        }

        #endregion

        #region Domain event handlers

        static void DomainService_Saved(IDomainService sender, SaveEventArgs<IDomain> e)
        {
            e.SavedEntities.ForEach(x => DistributedCache.Instance.RefreshDomainCache(x));
        }

        static void DomainService_Deleted(IDomainService sender, DeleteEventArgs<IDomain> e)
        {
            e.DeletedEntities.ForEach(x => DistributedCache.Instance.RemoveDomainCache(x));
        }

        #endregion

        #region Language event handlers
        /// <summary>
        /// Fires when a langauge is deleted
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        static void LocalizationServiceDeletedLanguage(ILocalizationService sender, DeleteEventArgs<ILanguage> e)
        {
            e.DeletedEntities.ForEach(x => DistributedCache.Instance.RemoveLanguageCache(x));
        }

        /// <summary>
        /// Fires when a langauge is saved
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        static void LocalizationServiceSavedLanguage(ILocalizationService sender, SaveEventArgs<ILanguage> e)
        {
            e.SavedEntities.ForEach(x => DistributedCache.Instance.RefreshLanguageCache(x));
        }
      
        #endregion

        #region Content/media/member Type event handlers

        private void ContentTypeServiceChanged(ContentTypeServiceBase<IContentType> sender,
            ContentTypeServiceBase<IContentType>.Change.EventArgs args)
        {
            DistributedCache.Instance.RefreshContentTypeCache(args.Changes.ToArray());
        }

        private void ContentTypeServiceChanged(ContentTypeServiceBase<IMediaType> sender,
            ContentTypeServiceBase<IMediaType>.Change.EventArgs args)
        {
            DistributedCache.Instance.RefreshContentTypeCache(args.Changes.ToArray());
        }

        private void ContentTypeServiceChanged(ContentTypeServiceBase<IMemberType> sender,
            ContentTypeServiceBase<IMemberType>.Change.EventArgs args)
        {
            DistributedCache.Instance.RefreshContentTypeCache(args.Changes.ToArray());
        }
        
        #endregion
        
        #region User/permissions event handlers

        static void CacheRefresherEventHandler_AssignedPermissions(PermissionRepository<IContent> sender, SaveEventArgs<EntityPermission> e)
        {
            var userIds = e.SavedEntities.Select(x => x.UserId).Distinct();
            userIds.ForEach(x => DistributedCache.Instance.RefreshUserPermissionsCache(x));
        }

        static void PermissionDeleted(UserPermission sender, DeleteEventArgs e)
        {
            InvalidateCacheForPermissionsChange(sender);
        }

        static void PermissionUpdated(UserPermission sender, SaveEventArgs e)
        {
            InvalidateCacheForPermissionsChange(sender);
        }

        static void PermissionNew(UserPermission sender, NewEventArgs e)
        {
            InvalidateCacheForPermissionsChange(sender);
        }

        static void UserServiceSavedUser(IUserService sender, SaveEventArgs<IUser> e)
        {
            e.SavedEntities.ForEach(x => DistributedCache.Instance.RefreshUserCache(x.Id));
        }

        static void UserServiceDeletedUser(IUserService sender, DeleteEventArgs<IUser> e)
        {
            e.DeletedEntities.ForEach(x => DistributedCache.Instance.RemoveUserCache(x.Id));
        }
        
        private static void InvalidateCacheForPermissionsChange(UserPermission sender)
        {
            if (sender.User != null)
            {
                DistributedCache.Instance.RefreshUserPermissionsCache(sender.User.Id);
            }
            else if (sender.UserId > -1)
            {
                DistributedCache.Instance.RefreshUserPermissionsCache(sender.UserId);
            }
            else if (sender.NodeIds.Any())
            {
                DistributedCache.Instance.RefreshAllUserPermissionsCache();
            }
        }

        #endregion

        #region Template event handlers

        /// <summary>
        /// Removes cache for template
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        static void FileServiceDeletedTemplate(IFileService sender, DeleteEventArgs<ITemplate> e)
        {
            e.DeletedEntities.ForEach(x => DistributedCache.Instance.RemoveTemplateCache(x.Id));
        }

        /// <summary>
        /// Refresh cache for template
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        static void FileServiceSavedTemplate(IFileService sender, SaveEventArgs<ITemplate> e)
        {
            e.SavedEntities.ForEach(x => DistributedCache.Instance.RefreshTemplateCache(x.Id));
        }
      
        #endregion

        #region Macro event handlers

        void MacroServiceDeleted(IMacroService sender, DeleteEventArgs<IMacro> e)
        {
            foreach (var entity in e.DeletedEntities)
            {
                DistributedCache.Instance.RemoveMacroCache(entity);
            }
        }

        void MacroServiceSaved(IMacroService sender, SaveEventArgs<IMacro> e)
        {
            foreach (var entity in e.SavedEntities)
            {
                DistributedCache.Instance.RefreshMacroCache(entity);
            }
        }
  
        #endregion

        #region Media event handlers

        private static void MediaServiceChanged(IMediaService sender, TreeChange<IMedia>.EventArgs args)
        {
            DistributedCache.Instance.RefreshMediaCache(args.Changes.ToArray());
        }

        #endregion

        #region Member event handlers

        static void MemberServiceDeleted(IMemberService sender, DeleteEventArgs<IMember> e)
        {
            DistributedCache.Instance.RemoveMemberCache(e.DeletedEntities.ToArray());    
        }

        static void MemberServiceSaved(IMemberService sender, SaveEventArgs<IMember> e)
        {
            DistributedCache.Instance.RefreshMemberCache(e.SavedEntities.ToArray());
        }

        #endregion

        #region Member group event handlers

        static void MemberGroupService_Deleted(IMemberGroupService sender, DeleteEventArgs<IMemberGroup> e)
        {
            foreach (var m in e.DeletedEntities.ToArray())
            {
                DistributedCache.Instance.RemoveMemberGroupCache(m.Id);
            }
        }

        static void MemberGroupService_Saved(IMemberGroupService sender, SaveEventArgs<IMemberGroup> e)
        {
            foreach (var m in e.SavedEntities.ToArray())
            {
                DistributedCache.Instance.RemoveMemberGroupCache(m.Id);
            }
        } 
        #endregion
    }
}