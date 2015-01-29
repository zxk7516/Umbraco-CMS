using System;
using System.Linq;
using NUnit.Framework;
using Umbraco.Core.Models;
using Umbraco.Tests.TestHelpers;
using Umbraco.Tests.TestHelpers.Entities;

namespace Umbraco.Tests.Models
{
    [TestFixture]
    public class ContentExtensionsTests : BaseUmbracoConfigurationTest
    {
        #region RequireSaving

        // when Published...

        [Test] // debatable
        public void RequireSaving_When_PublishedAndNothingChanged_Should()
        {
            var contentType = MockedContentTypes.CreateTextpageContentType();
            var content = MockedContent.CreateTextpageContent(contentType, "Textpage", -1);

            content.ChangePublishedState(PublishedState.Published); // is published
            content.ResetDirtyProperties(false);

            Assert.IsTrue(content.RequiresSaving());
        }

        [Test]
        public void RequireSaving_When_PublishedAndSavingNothingChanged_ShouldNot()
        {
            var contentType = MockedContentTypes.CreateTextpageContentType();
            var content = MockedContent.CreateTextpageContent(contentType, "Textpage", -1);

            content.ChangePublishedState(PublishedState.Published); // is published
            content.ResetDirtyProperties(false);

            content.ChangePublishedState(PublishedState.Saved); // saving
            Assert.IsFalse(content.RequiresSaving());
        }

        [Test]
        public void RequireSaving_When_PublishedAndSavingUserPropertyChanged_Should()
        {
            var contentType = MockedContentTypes.CreateTextpageContentType();
            var content = MockedContent.CreateTextpageContent(contentType, "Textpage", -1);

            content.ChangePublishedState(PublishedState.Published); // is published
            content.ResetDirtyProperties(false);

            content.Properties.First().Value = "hello world"; // change data

            content.ChangePublishedState(PublishedState.Saved); // saving
            Assert.IsTrue(content.RequiresSaving());
        }

        [Test]
        public void RequireSaving_When_PublishedAndSavingContentPropertyChanged_Should()
        {
            var contentType = MockedContentTypes.CreateTextpageContentType();
            var content = MockedContent.CreateTextpageContent(contentType, "Textpage", -1);

            content.ChangePublishedState(PublishedState.Published); // is published
            content.ResetDirtyProperties(false);

            content.ReleaseDate = DateTime.Now; // change data

            content.ChangePublishedState(PublishedState.Saved); // saving
            Assert.IsTrue(content.RequiresSaving());
        }

        [Test]
        public void RequireSaving_When_PublishedAndUnpublishing_Should()
        {
            var contentType = MockedContentTypes.CreateTextpageContentType();
            var content = MockedContent.CreateTextpageContent(contentType, "Textpage", -1);

            content.ChangePublishedState(PublishedState.Published); // published
            content.ResetDirtyProperties(false);

            content.ChangePublishedState(PublishedState.Unpublished); // unpublishing
            Assert.IsTrue(content.RequiresSaving());
        }

        [Test]
        public void RequireSaving_When_PublishedAndPublishingNothingChanged_Should()
        {
            var contentType = MockedContentTypes.CreateTextpageContentType();
            var content = MockedContent.CreateTextpageContent(contentType, "Textpage", -1);

            content.ChangePublishedState(PublishedState.Published); // published
            content.ResetDirtyProperties(false);

            // just change to Unpublished will not be registered as a change,
            // have to change to something else then change again - that should
            // not really happen IRL - just documenting what would happen

            content.ChangePublishedState(PublishedState.Unpublished); // unpublishing
            content.ChangePublishedState(PublishedState.Published); // publishing
            Assert.IsTrue(content.RequiresSaving());
        }

        // when NotPublished...

        [Test]
        public void RequireSaving_When_UnpublishedAndNothingChanged_Should()
        {
            // that one is important: when unpublishing a content with changes,
            // the newest version is already Unpublished and nothing changes, BUT
            // we want it saved to register the date change etc.

            var contentType = MockedContentTypes.CreateTextpageContentType();
            var content = MockedContent.CreateTextpageContent(contentType, "Textpage", -1);

            content.ResetDirtyProperties(false);

            Assert.IsTrue(content.RequiresSaving());
        }

        [Test]
        public void RequireSaving_When_UnpublishedAndSavingNothingChanged_ShouldNot()
        {
            var contentType = MockedContentTypes.CreateTextpageContentType();
            var content = MockedContent.CreateTextpageContent(contentType, "Textpage", -1);

            content.ResetDirtyProperties(false);

            content.ChangePublishedState(PublishedState.Saved); // saving
            Assert.IsFalse(content.RequiresSaving());
        }

        [Test]
        public void RequireSaving_When_UnpublishedAndSavingUserPropertyChanged_Should()
        {
            var contentType = MockedContentTypes.CreateTextpageContentType();
            var content = MockedContent.CreateTextpageContent(contentType, "Textpage", -1);

            content.ResetDirtyProperties(false);

            content.Properties.First().Value = "hello world"; // change data

            content.ChangePublishedState(PublishedState.Saved); // saving
            Assert.IsTrue(content.RequiresSaving());
        }

        [Test]
        public void RequireSaving_When_UnpublishedAndSavingContentPropertyChanged_Should()
        {
            var contentType = MockedContentTypes.CreateTextpageContentType();
            var content = MockedContent.CreateTextpageContent(contentType, "Textpage", -1);

            content.ResetDirtyProperties(false);

            content.ReleaseDate = DateTime.Now; // change data

            content.ChangePublishedState(PublishedState.Saved); // saving
            Assert.IsTrue(content.RequiresSaving());
        }

        [Test]
        public void RequireSaving_When_UnpublishedAndPublishing_Should()
        {
            var contentType = MockedContentTypes.CreateTextpageContentType();
            var content = MockedContent.CreateTextpageContent(contentType, "Textpage", -1);

            content.ResetDirtyProperties(false);

            content.ChangePublishedState(PublishedState.Published); // publishing
            Assert.IsTrue(content.RequiresSaving());
        }

        [Test]
        public void RequireSaving_When_UnpublishedAndUnpublishingNothingChanged_Should()
        {
            var contentType = MockedContentTypes.CreateTextpageContentType();
            var content = MockedContent.CreateTextpageContent(contentType, "Textpage", -1);

            content.ResetDirtyProperties(false);

            // just change to Unpublished will not be registered as a change,
            // have to change to something else then change again - that should
            // not really happen IRL - just documenting what would happen

            content.ChangePublishedState(PublishedState.Published); // publishing
            content.ChangePublishedState(PublishedState.Unpublished); // unpublishing
            Assert.IsTrue(content.RequiresSaving());
        }

        #endregion

        #region RequireNewVersion

        // when language...

        [Test]
        public void RequireNewVersion_When_LanguageChanged_Should()
        {
            var contentType = MockedContentTypes.CreateTextpageContentType();
            var content = MockedContent.CreateTextpageContent(contentType, "Textpage", -1);

            content.ResetDirtyProperties(false);

            content.Language = "en-AU";
            Assert.IsTrue(content.RequiresNewVersion());
        }

        // when Published...

        [Test]
        public void RequireNewVersion_When_PublishedAndNothingChanged_ShouldNot()
        {
            var contentType = MockedContentTypes.CreateTextpageContentType();
            var content = MockedContent.CreateTextpageContent(contentType, "Textpage", -1);

            content.ChangePublishedState(PublishedState.Published); // is published
            content.ResetDirtyProperties(false);

            Assert.IsFalse(content.RequiresNewVersion());
        }

        [Test]
        public void RequireNewVersion_When_PublishedAndUserPropertyChanged_Should()
        {
            var contentType = MockedContentTypes.CreateTextpageContentType();
            var content = MockedContent.CreateTextpageContent(contentType, "Textpage", -1);

            content.ChangePublishedState(PublishedState.Published); // is published
            content.ResetDirtyProperties(false);

            content.Properties.First().Value = "hello world"; // change data

            Assert.IsTrue(content.RequiresNewVersion());
        }

        [Test]
        public void RequireNewVersion_When_PublishedAndContentPropertyChanged_Should()
        {
            var contentType = MockedContentTypes.CreateTextpageContentType();
            var content = MockedContent.CreateTextpageContent(contentType, "Textpage", -1);

            content.ChangePublishedState(PublishedState.Published); // is published
            content.ResetDirtyProperties(false);

            content.ReleaseDate = DateTime.Now; // change content property
            Assert.IsTrue(content.RequiresNewVersion());
        }

        [Test]
        public void RequireNewVersion_When_PublishedAndSavingNothingChanged_ShouldNot()
        {
            var contentType = MockedContentTypes.CreateTextpageContentType();
            var content = MockedContent.CreateTextpageContent(contentType, "Textpage", -1);

            content.ChangePublishedState(PublishedState.Published); // is published
            content.ResetDirtyProperties(false);

            content.ChangePublishedState(PublishedState.Saved); // saving
            Assert.IsFalse(content.RequiresNewVersion());
        }

        [Test]
        public void RequireNewVersion_When_PublishedAndSavingUserPropertyChanged_Should()
        {
            var contentType = MockedContentTypes.CreateTextpageContentType();
            var content = MockedContent.CreateTextpageContent(contentType, "Textpage", -1);

            content.ChangePublishedState(PublishedState.Published); // is published
            content.ResetDirtyProperties(false);

            content.Properties.First().Value = "hello world"; // change data

            content.ChangePublishedState(PublishedState.Saved); // saving
            Assert.IsTrue(content.RequiresNewVersion());
        }

        [Test]
        public void RequireNewVersion_When_PublishedAndSavingContentPropertyChanged_Should()
        {
            var contentType = MockedContentTypes.CreateTextpageContentType();
            var content = MockedContent.CreateTextpageContent(contentType, "Textpage", -1);

            content.ChangePublishedState(PublishedState.Published); // is published
            content.ResetDirtyProperties(false);

            content.ReleaseDate = DateTime.Now; // change content property
            content.ChangePublishedState(PublishedState.Saved); // saving
            Assert.IsTrue(content.RequiresNewVersion());
        }

        [Test]
        public void RequireNewVersion_When_PublishedAndUnpublishing_Should()
        {
            var contentType = MockedContentTypes.CreateTextpageContentType();
            var content = MockedContent.CreateTextpageContent(contentType, "Textpage", -1);

            content.ChangePublishedState(PublishedState.Published); // is published
            content.ResetDirtyProperties(false);

            content.ChangePublishedState(PublishedState.Unpublished); // unpublishing
            Assert.IsTrue(content.RequiresNewVersion());
        }

        [Test] // debatable
        public void RequireNewVersion_When_PublishedAndPublishingNothingChanged_ShouldNot()
        {
            var contentType = MockedContentTypes.CreateTextpageContentType();
            var content = MockedContent.CreateTextpageContent(contentType, "Textpage", -1);

            content.ChangePublishedState(PublishedState.Published); // is published
            content.ResetDirtyProperties(false);

            // just change to Unpublished will not be registered as a change,
            // have to change to something else then change again - that should
            // not really happen IRL - just documenting what would happen

            content.ChangePublishedState(PublishedState.Unpublished); // unpublishing
            content.ChangePublishedState(PublishedState.Published); // publishing
            Assert.IsFalse(content.RequiresNewVersion());
        }

        // when NotPublished...

        [Test]
        public void RequireNewVersion_When_NotPublishedAndNothingChanged_ShouldNot()
        {
            var contentType = MockedContentTypes.CreateTextpageContentType();
            var content = MockedContent.CreateTextpageContent(contentType, "Textpage", -1);

            content.ResetDirtyProperties(false);

            Assert.IsFalse(content.RequiresNewVersion());
        }

        [Test] // debatable
        public void RequireNewVersion_When_NotPublishedAndUserPropertyChanged_Should()
        {
            var contentType = MockedContentTypes.CreateTextpageContentType();
            var content = MockedContent.CreateTextpageContent(contentType, "Textpage", -1);

            content.ResetDirtyProperties(false);

            content.Properties.First().Value = "hello world"; // change user property
            Assert.IsTrue(content.RequiresNewVersion());
        }

        [Test]
        public void RequireNewVersion_When_NotPublishedAndContentPropertyChanged_Should()
        {
            var contentType = MockedContentTypes.CreateTextpageContentType();
            var content = MockedContent.CreateTextpageContent(contentType, "Textpage", -1);

            content.ResetDirtyProperties(false);

            content.ReleaseDate = DateTime.Now; // change content property
            Assert.IsTrue(content.RequiresNewVersion());
        }

        [Test]
        public void RequireNewVersion_When_NotPublishedAndSavingNothingChanged_ShouldNot()
        {
            var contentType = MockedContentTypes.CreateTextpageContentType();
            var content = MockedContent.CreateTextpageContent(contentType, "Textpage", -1);

            content.ResetDirtyProperties(false);

            content.ChangePublishedState(PublishedState.Saved); // saving
            Assert.IsFalse(content.RequiresNewVersion());
        }

        [Test]
        public void RequireNewVersion_When_NotPublishedAndSavingUserPropertyChanged_ShouldNot()
        {
            var contentType = MockedContentTypes.CreateTextpageContentType();
            var content = MockedContent.CreateTextpageContent(contentType, "Textpage", -1);

            content.ResetDirtyProperties(false);

            content.Properties.First().Value = "hello world"; // change user property
            content.ChangePublishedState(PublishedState.Saved); // saving
            Assert.IsFalse(content.RequiresNewVersion());
        }

        [Test]
        public void RequireNewVersion_When_NotPublishedAndSavingContentPropertyChanged_Should()
        {
            var contentType = MockedContentTypes.CreateTextpageContentType();
            var content = MockedContent.CreateTextpageContent(contentType, "Textpage", -1);

            content.ResetDirtyProperties(false);

            content.ReleaseDate = DateTime.Now; // change content property
            content.ChangePublishedState(PublishedState.Saved); // saving
            Assert.IsTrue(content.RequiresNewVersion());
        }

        [Test]
        public void RequireNewVersion_When_NotPublishedAndPublishing_ShouldNot()
        {
            var contentType = MockedContentTypes.CreateTextpageContentType();
            var content = MockedContent.CreateTextpageContent(contentType, "Textpage", -1);

            content.ResetDirtyProperties(false);

            content.ChangePublishedState(PublishedState.Published); // publishing
            Assert.IsFalse(content.RequiresNewVersion());
        }

        [Test]
        public void RequireNewVersion_When_NotPublishedAndPublishingUserPropertyChanged_Should()
        {
            var contentType = MockedContentTypes.CreateTextpageContentType();
            var content = MockedContent.CreateTextpageContent(contentType, "Textpage", -1);

            content.ResetDirtyProperties(false);

            content.Properties.First().Value = "hello world"; // change user property
            content.ChangePublishedState(PublishedState.Published); // publishing
            Assert.IsTrue(content.RequiresNewVersion());
        }

        [Test]
        public void RequireNewVersion_When_NotPublishedAndPublishingContentPropertyChanged_Should()
        {
            var contentType = MockedContentTypes.CreateTextpageContentType();
            var content = MockedContent.CreateTextpageContent(contentType, "Textpage", -1);

            content.ResetDirtyProperties(false);

            content.ReleaseDate = DateTime.Now; // change content property
            content.ChangePublishedState(PublishedState.Published); // publishing
            Assert.IsTrue(content.RequiresNewVersion());
        }
        
        [Test] // debatable
        public void RequireNewVersion_When_NotPublishedAndUnpublishingNothingChanged_Should()
        {
            var contentType = MockedContentTypes.CreateTextpageContentType();
            var content = MockedContent.CreateTextpageContent(contentType, "Textpage", -1);

            content.ResetDirtyProperties(false);

            // just change to Unpublished will not be registered as a change,
            // have to change to something else then change again - that should
            // not really happen IRL - just documenting what would happen

            content.ChangePublishedState(PublishedState.Published); // publishing
            content.ChangePublishedState(PublishedState.Unpublished); // unpublishing
            Assert.IsTrue(content.RequiresNewVersion());
        }

        #endregion

        #region ClearPublishedFlag

        [Test]
        public void ClearPublishedFlag_When_UnpublishedAndPublishing_Should()
        {
            var contentType = MockedContentTypes.CreateTextpageContentType();
            var content = MockedContent.CreateTextpageContent(contentType, "Textpage", -1);

            content.ResetDirtyProperties(false);

            content.ChangePublishedState(PublishedState.Published); // publishing
            Assert.IsTrue(content.RequiresClearPublishedFlag());
        }

        [Test]
        public void ClearPublishedFlag_When_UnpublishedAndUnpublishing_ShouldNot()
        {
            var contentType = MockedContentTypes.CreateTextpageContentType();
            var content = MockedContent.CreateTextpageContent(contentType, "Textpage", -1);

            content.ResetDirtyProperties(false);

            content.ChangePublishedState(PublishedState.Unpublished); // unpublishing - does not "change it"
            Assert.IsFalse(content.RequiresClearPublishedFlag());
        }

        [Test]
        public void ClearPublishedFlag_When_UnpublishedAndForceUnpublishing_Should()
        {
            var contentType = MockedContentTypes.CreateTextpageContentType();
            var content = MockedContent.CreateTextpageContent(contentType, "Textpage", -1);

            content.ResetDirtyProperties(false);

            content.ChangePublishedState(PublishedState.Published);
            content.ChangePublishedState(PublishedState.Unpublished); // unpublishing - does "change it"
            Assert.IsTrue(content.RequiresClearPublishedFlag());
        }

        [Test]
        public void ClearPublishedFlag_When_UnpublishedAndSaving_ShouldNot()
        {
            var contentType = MockedContentTypes.CreateTextpageContentType();
            var content = MockedContent.CreateTextpageContent(contentType, "Textpage", -1);

            content.ResetDirtyProperties(false);

            content.ChangePublishedState(PublishedState.Saved); // saving
            Assert.IsFalse(content.RequiresClearPublishedFlag());
        }

        [Test]
        public void ClearPublishedFlag_When_PublishedAndSaving_ShouldNot()
        {
            var contentType = MockedContentTypes.CreateTextpageContentType();
            var content = MockedContent.CreateTextpageContent(contentType, "Textpage", -1);

            content.ChangePublishedState(PublishedState.Published); // published
            content.ResetDirtyProperties(false);

            content.ChangePublishedState(PublishedState.Saved); // saving
            Assert.IsFalse(content.RequiresClearPublishedFlag());
        }

        [Test]
        public void ClearPublishedFlag_When_PublishedAndUnpublishing_Should()
        {
            var contentType = MockedContentTypes.CreateTextpageContentType();
            var content = MockedContent.CreateTextpageContent(contentType, "Textpage", -1);

            content.ChangePublishedState(PublishedState.Published); // published
            content.ResetDirtyProperties(false);

            content.ChangePublishedState(PublishedState.Unpublished); // unpublishing
            Assert.IsTrue(content.RequiresClearPublishedFlag());
        }

        [Test]
        public void ClearPublishedFlag_When_PublishedAndPublishing_ShouldNot()
        {
            var contentType = MockedContentTypes.CreateTextpageContentType();
            var content = MockedContent.CreateTextpageContent(contentType, "Textpage", -1);

            content.ChangePublishedState(PublishedState.Published); // published
            content.ResetDirtyProperties(false);

            content.ChangePublishedState(PublishedState.Published); // publishing - does not "change it"
            Assert.IsFalse(content.RequiresClearPublishedFlag());
        }

        [Test]
        public void ClearPublishedFlag_When_PublishedAndForcePublishing_Should()
        {
            var contentType = MockedContentTypes.CreateTextpageContentType();
            var content = MockedContent.CreateTextpageContent(contentType, "Textpage", -1);

            content.ChangePublishedState(PublishedState.Published); // published
            content.ResetDirtyProperties(false);

            content.ChangePublishedState(PublishedState.Unpublished);
            content.ChangePublishedState(PublishedState.Published); // publishing - does "change it"
            Assert.IsTrue(content.RequiresClearPublishedFlag());
        }

        #endregion

        #region Misc.

        [Test]
        public void DirtyProperty_Reset_Clears_SavedPublishedState()
        {
            var contentType = MockedContentTypes.CreateTextpageContentType();
            var content = MockedContent.CreateTextpageContent(contentType, "Textpage", -1);

            content.ChangePublishedState(PublishedState.Saved); // saved
            content.ResetDirtyProperties(false); // reset to .Unpublished
            Assert.AreEqual(PublishedState.Unpublished, content.PublishedState);
        }

        [Test]
        public void DirtyProperty_OnlyIfActuallyChanged_Content()
        {
            var contentType = MockedContentTypes.CreateTextpageContentType();
            var content = MockedContent.CreateTextpageContent(contentType, "Textpage", -1);

            // if you assign a content property with its value it is not dirty
            // if you assign it with another value then back, it is dirty

            content.ResetDirtyProperties(false);
            Assert.IsFalse(content.IsPropertyDirty("Published"));
            content.Published = true;
            Assert.IsTrue(content.IsPropertyDirty("Published"));
            content.ResetDirtyProperties(false);
            Assert.IsFalse(content.IsPropertyDirty("Published"));
            content.Published = true;
            Assert.IsFalse(content.IsPropertyDirty("Published"));
            content.Published = false;
            content.Published = true;
            Assert.IsTrue(content.IsPropertyDirty("Published"));
        }

        [Test]
        public void DirtyProperty_OnlyIfActuallyChanged_User()
        {
            var contentType = MockedContentTypes.CreateTextpageContentType();
            var content = MockedContent.CreateTextpageContent(contentType, "Textpage", -1);
            var prop = content.Properties.First();

            // if you assign a user property with its value it is not dirty
            // if you assign it with another value then back, it is dirty

            prop.Value = "A";
            content.ResetDirtyProperties(false);
            Assert.IsFalse(prop.IsDirty());
            prop.Value = "B";
            Assert.IsTrue(prop.IsDirty());
            content.ResetDirtyProperties(false);
            Assert.IsFalse(prop.IsDirty());
            prop.Value = "B";
            Assert.IsFalse(prop.IsDirty());
            prop.Value = "A";
            prop.Value = "B";
            Assert.IsTrue(prop.IsDirty());
        }

        [Test]
        public void DirtyProperty_UpdateDate()
        {
            var contentType = MockedContentTypes.CreateTextpageContentType();
            var content = MockedContent.CreateTextpageContent(contentType, "Textpage", -1);
            var prop = content.Properties.First();

            content.ResetDirtyProperties(false);
            var d = content.UpdateDate;
            prop.Value = "A";
            Assert.IsTrue(content.IsAnyUserPropertyDirty());
            Assert.IsFalse(content.IsEntityDirty());
            Assert.AreEqual(d, content.UpdateDate);

            content.UpdateDate = DateTime.Now;
            Assert.IsTrue(content.IsEntityDirty());
            Assert.AreNotEqual(d, content.UpdateDate);

            // so... changing UpdateDate would count as a content property being changed
            // however in ContentRepository.PersistUpdatedItem, we change UpdateDate AFTER
            // we've tested for RequiresSaving & RequiresNewVersion so it's OK
        }

        #endregion
    }
}