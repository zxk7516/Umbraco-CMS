using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Hosting;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Models;
using Umbraco.Core.Persistence.Repositories;
using Umbraco.Core.Services;
using Umbraco.Core.Sync;
using umbraco.presentation.umbraco.dialogs;
using Umbraco.Web.Cache;
using File = System.IO.File;

namespace Umbraco.Web.PublishedCache
{
// make sure it never goes to release
#if DEBUG
    public class EventsTracer : ApplicationEventHandler
    {
        // set const to true to enable
        // do NOT commit this class with Enabled set to true!
        private const bool Enabled = true;

        protected override void ApplicationStarted(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
        {
            if (Enabled == false) return;

            PageCacheRefresher.CacheUpdated += PageCacheUpdated;
            UnpublishedPageCacheRefresher.CacheUpdated += UnpublishedPageCacheUpdated;
            ContentTypeCacheRefresher.CacheUpdated += ContentTypeCacheUpdated; // same refresher for content, media & member

            ContentRepository.RemovedEntity += OnContentRemovedEntity;
            ContentRepository.RemovedVersion += OnContentRemovedVersion;
            ContentRepository.RefreshedEntity += OnContentRefreshedEntity;
            MediaRepository.RemovedEntity += OnMediaRemovedEntity;
            MediaRepository.RemovedVersion += OnMediaRemovedVersion;
            MediaRepository.RefreshedEntity += OnMediaRefreshedEntity;
            MemberRepository.RemovedEntity += OnMemberRemovedEntity;
            MemberRepository.RemovedVersion += OnMemberRemovedVersion;
            MemberRepository.RefreshedEntity += OnMemberRefreshedEntity;

            ContentRepository.EmptiedRecycleBin += OnEmptiedRecycleBin;
            MediaRepository.EmptiedRecycleBin += OnEmptiedRecycleBin;

            ContentService.ContentTypesChanged += OnContentTypesChanged;
            MediaService.ContentTypesChanged += OnMediaTypesChanged;
            MemberService.MemberTypesChanged += OnMemberTypesChanged;

            _filepath = HostingEnvironment.MapPath("~/App_Data/EventsLog.txt");
            Write("- START");
        }

        private string _filepath;

        private void Write(string msg)
        {
            File.AppendAllText(_filepath, msg + Environment.NewLine);
        }

        private void Write(string format, params object[] args)
        {
            Write(string.Format(format, args));
        }

        private void UnpublishedPageCacheUpdated(UnpublishedPageCacheRefresher sender, CacheRefresherEventArgs args)
        {
            switch (args.MessageType)
            {
                case MessageType.RefreshAll:
                    Write("UPUB REFRSH ALL");
                    break;
                case MessageType.RefreshById:
                    Write("UPUB REFRSH BYID {0}", (int)args.MessageObject);
                    break;
                case MessageType.RefreshByInstance:
                    Write("UPUB REFRSH INST {0}", ((IContent)args.MessageObject).Id);
                    break;
                case MessageType.RefreshByJson:
                    var json = (string)args.MessageObject;
                    Write("UPUB REFRSH JSON");
                    foreach (var c in UnpublishedPageCacheRefresher.DeserializeFromJsonPayload(json))
                        Write("  {0} {1}", c.Operation, c.Id);
                    break;
                case MessageType.RemoveById:
                    Write("UPUB REMOVE BYID {0}", (int)args.MessageObject);
                    break;
                case MessageType.RemoveByInstance:
                    Write("UPUB REMOVE INST {0}", ((IContent)args.MessageObject).Id);
                    break;
            }
        }

        private void PageCacheUpdated(PageCacheRefresher sender, CacheRefresherEventArgs args)
        {
            switch (args.MessageType)
            {
                case MessageType.RefreshAll:
                    Write("PUBL REFRSH ALL");
                    break;
                case MessageType.RefreshById:
                    Write("PUBL REFRSH BYID {0}", (int)args.MessageObject);
                    break;
                case MessageType.RefreshByInstance:
                    Write("PUBL REFRSH INST {0}", ((IContent)args.MessageObject).Id);
                    break;
                case MessageType.RefreshByJson:
                    Write("PUBL REFRSH JSON WTF?");
                    break;
                case MessageType.RemoveById:
                    Write("PUBL REMOVE BYID {0}", (int)args.MessageObject);
                    break;
                case MessageType.RemoveByInstance:
                    Write("PUBL REMOVE INST {0}", ((IContent)args.MessageObject).Id);
                    break;
            }
        }

        private void ContentTypeCacheUpdated(ContentTypeCacheRefresher sender, CacheRefresherEventArgs args)
        {
            switch (args.MessageType)
            {
                case MessageType.RefreshAll:
                    Write("CTYP REFRSH ALL");
                    break;
                case MessageType.RefreshById:
                    Write("CTYP REFRSH BYID {0}", (int)args.MessageObject);
                    break;
                case MessageType.RefreshByInstance:
                    Write("CTYP REFRSH INST {0}", ((IContent)args.MessageObject).Id);
                    break;
                case MessageType.RefreshByJson:
                    var json = (string)args.MessageObject;
                    Write("CTYP REFRSH JSON");
                    foreach (var c in ContentTypeCacheRefresher.DeserializeFromJsonPayload(json))
                    {
                        var action = c.WasDeleted ? "REMOVE" : "REFRSH";
                        Write("  {0} {1} {2}{3}", action, c.Type, c.Id, c.DescendantPayloads.Any() ? " +DESC" : "");
                        WriteContentTypePayloadDescendants(c.DescendantPayloads, "  ");
                    }
                    break;
                case MessageType.RemoveById:
                    Write("CTYP REMOVE BYID {0}", (int)args.MessageObject);
                    break;

                case MessageType.RemoveByInstance:
                    Write("CTYP REMOVE INST {0}", ((IContent)args.MessageObject).Id);
                    break;
            }
        }

        private void WriteContentTypePayloadDescendants(IEnumerable<ContentTypeCacheRefresher.JsonPayload> payloads, string tabs)
        {
            foreach (var payload in payloads)
            {
                var action = payload.WasDeleted ? "REMOVE" : "REFRSH";
                Write("  {0}{1} {2} {3}{4}", tabs, action, payload.Type, payload.Id, payload.DescendantPayloads.Any() ? " +DESC" : "");
                WriteContentTypePayloadDescendants(payload.DescendantPayloads, tabs + "  ");
            }
        }

        private void OnContentRemovedEntity(object sender, ContentRepository.EntityChangeEventArgs args)
        {
            Write("REPO REMOVE CNT");
            foreach (var e in args.Entities)
                Write("  " + e.Id);
        }

        private void OnMediaRemovedEntity(object sender, MediaRepository.EntityChangeEventArgs args)
        {
            Write("REPO REMOVE MED");
            foreach (var e in args.Entities)
                Write("  " + e.Id);
        }

        private void OnMemberRemovedEntity(object sender, MemberRepository.EntityChangeEventArgs args)
        {
            Write("REPO REMOVE MBR");
            foreach (var e in args.Entities)
                Write("  " + e.Id);
        }

        private void OnContentRemovedVersion(object sender, ContentRepository.VersionChangeEventArgs args)
        {
            Write("REPO REMOVE CNTVER");
            foreach (var v in args.Versions)
                Write("  " + v.Item1 + ":" + v.Item2);
        }

        private void OnMediaRemovedVersion(object sender, MediaRepository.VersionChangeEventArgs args)
        {
            Write("REPO REMOVE MEDVER");
            foreach (var v in args.Versions)
                Write("  " + v.Item1 + ":" + v.Item2);
        }

        private void OnMemberRemovedVersion(object sender, MemberRepository.VersionChangeEventArgs args)
        {
            Write("REPO REMOVE MBRVER");
            foreach (var v in args.Versions)
                Write("  " + v.Item1 + ":" + v.Item2);
        }

        private void OnContentRefreshedEntity(object sender, ContentRepository.EntityChangeEventArgs args)
        {
            Write("REPO REFRSH CNT");
            foreach (var e in args.Entities)
            {
                var wasPublished = (e.IsPropertyDirty("Published") && ((Core.Models.Content)e).PublishedState == PublishedState.Unpublished);
                Write("  " + e.Id + " " + (e.Published ? "PUBL" : "UPUB") + (wasPublished ? " (UNPUBLISHED)" : ""));
            }
        }

        private void OnMediaRefreshedEntity(object sender, MediaRepository.EntityChangeEventArgs args)
        {
            Write("REPO REFRSH MED");
            foreach (var e in args.Entities)
                Write("  " + e.Id);
        }

        private void OnMemberRefreshedEntity(object sender, MemberRepository.EntityChangeEventArgs args)
        {
            Write("REPO REFRSH MBR");
            foreach (var e in args.Entities)
                Write("  " + e.Id);
        }

        private void OnEmptiedRecycleBin(object sender, ContentRepository.RecycleBinEventArgs args)
        {
            Write("REPO EMPTYTRASH CNT");
        }

        private void OnEmptiedRecycleBin(object sender, MediaRepository.RecycleBinEventArgs args)
        {
            Write("REPO EMPTYTRASH MED");
        }

        private void OnContentTypesChanged(ContentService sender, ContentService.ContentTypeChangedEventArgs args)
        {
            Write("SVCE CNT CHG");
            foreach (var id in args.ContentTypeIds)
                Write("  " + id);
        }

        private void OnMediaTypesChanged(MediaService sender, MediaService.ContentTypeChangedEventArgs args)
        {
            Write("SVCE MED CHG");
            foreach (var id in args.ContentTypeIds)
                Write("  " + id);
        }

        private void OnMemberTypesChanged(MemberService sender, MemberService.MemberTypeChangedEventArgs args)
        {
            Write("SVCE MBR CHG");
            foreach (var id in args.MemberTypeIds)
                Write("  " + id);
        }
    }
#endif
}
