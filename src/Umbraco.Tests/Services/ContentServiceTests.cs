using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Moq;
using NUnit.Framework;
using Umbraco.Core;
using Umbraco.Core.Configuration.UmbracoSettings;
using Umbraco.Core.IO;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Models.Membership;
using Umbraco.Core.Models.Rdbms;
using Umbraco.Core.Persistence;
using Umbraco.Core.Persistence.Repositories;
using Umbraco.Core.Persistence.UnitOfWork;
using Umbraco.Core.Services;
using Umbraco.Tests.TestHelpers;
using Umbraco.Tests.TestHelpers.Entities;

namespace Umbraco.Tests.Services
{
    /// <summary>
    /// Tests covering all methods in the ContentService class.
    /// This is more of an integration test as it involves multiple layers
    /// as well as configuration.
    /// </summary>
    [DatabaseTestBehavior(DatabaseBehavior.NewDbFileAndSchemaPerTest)]
    [TestFixture, RequiresSTA]
    [FacadeServiceBehavior(EnableRepositoryEvents = true)]
    public class ContentServiceTests : BaseServiceTest
    {
        [SetUp]
        public override void Initialize()
        {
	        base.Initialize();
            _taggedContentType = null;
        }
		
		[TearDown]
		public override void TearDown()
		{   
      		base.TearDown();
		}

        //TODO Add test to verify there is only ONE newest document/content in cmsDocument table after updating.
        //TODO Add test to delete specific version (with and without deleting prior versions) and versions by date.

        [Test]
        public void Remove_Scheduled_Publishing_Date()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;

            // Act
            var content = contentService.CreateContentWithIdentity("Test", -1, "umbTextpage", 0);

            content.ReleaseDate = DateTime.Now.AddHours(2);
            contentService.Save(content, 0);

            content = contentService.GetById(content.Id);
            content.ReleaseDate = null;
            contentService.Save(content, 0);


            // Assert
            Assert.IsTrue(contentService.PublishWithStatus(content).Success);
        }

        [Test]
        public void Count_All()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;

            // Act
            for (int i = 0; i < 20; i++)
            {
                contentService.CreateContentWithIdentity("Test", -1, "umbTextpage", 0);    
            }

            // Assert
            Assert.AreEqual(24, contentService.Count());
        }

        [Test]
        public void Count_By_Content_Type()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;
            var contentTypeService = ServiceContext.ContentTypeService;
            var contentType = MockedContentTypes.CreateSimpleContentType("umbBlah", "test Doc Type");
            contentTypeService.Save(contentType);

            // Act
            for (int i = 0; i < 20; i++)
            {
                contentService.CreateContentWithIdentity("Test", -1, "umbBlah", 0);
            }

            // Assert
            Assert.AreEqual(20, contentService.Count(contentTypeAlias: "umbBlah"));
        }

        [Test]
        public void Count_Children()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;
            var contentTypeService = ServiceContext.ContentTypeService;
            var contentType = MockedContentTypes.CreateSimpleContentType("umbBlah", "test Doc Type");
            contentTypeService.Save(contentType);
            var parent = contentService.CreateContentWithIdentity("Test", -1, "umbBlah", 0);

            // Act
            for (int i = 0; i < 20; i++)
            {
                contentService.CreateContentWithIdentity("Test", parent, "umbBlah");
            }

            // Assert
            Assert.AreEqual(20, contentService.CountChildren(parent.Id));
        }

        [Test]
        public void Count_Descendants()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;
            var contentTypeService = ServiceContext.ContentTypeService;
            var contentType = MockedContentTypes.CreateSimpleContentType("umbBlah", "test Doc Type");
            contentTypeService.Save(contentType);
            var parent = contentService.CreateContentWithIdentity("Test", -1, "umbBlah", 0);

            // Act
            IContent current = parent;
            for (int i = 0; i < 20; i++)
            {
                current = contentService.CreateContentWithIdentity("Test", current, "umbBlah");
            }

            // Assert
            Assert.AreEqual(20, contentService.CountDescendants(parent.Id));
        }

        [Test]
        public void PublishWithStatus()
        {
            var contentType = MockedContentTypes.CreateTextpageContentType();
            ServiceContext.ContentTypeService.Save(contentType);
            var content = MockedContent.CreateTextpageContent(contentType, "Textpage", -1);
            ServiceContext.ContentService.PublishWithStatus(content);
            content.Properties["title"].Value = "changed";
            ServiceContext.ContentService.PublishWithStatus(content);
            var c = ServiceContext.ContentService.GetById(content.Id);
            Assert.AreEqual("changed", c.Properties["title"].Value);
        }

        [Test]
        public void PublishWithChildrenWithStatus()
        {
            var contentType = MockedContentTypes.CreateTextpageContentType();
            ServiceContext.ContentTypeService.Save(contentType);
            var content = MockedContent.CreateTextpageContent(contentType, "Textpage", -1);
            ServiceContext.ContentService.PublishWithStatus(content);
            content.Properties["title"].Value = "changed";
            ServiceContext.ContentService.PublishWithChildrenWithStatus(content);
            var c = ServiceContext.ContentService.GetById(content.Id);
            Assert.AreEqual("changed", c.Properties["title"].Value);
        }

        #region Tags

        private IContentType _taggedContentType;

        private IContent CreateTaggedContent(string name)
        {
            if (_taggedContentType == null)
            {
                var contentTypeService = ServiceContext.ContentTypeService;
                _taggedContentType = MockedContentTypes.CreateSimpleContentType("umbMandatory", "Mandatory Doc Type", true);
                _taggedContentType.PropertyGroups.First().PropertyTypes.Add(
                new PropertyType("test", DataTypeDatabaseType.Ntext, "tags")
                    {
                        DataTypeDefinitionId = 1041
                    });
                contentTypeService.Save(_taggedContentType);
            }
            return MockedContent.CreateSimpleContent(_taggedContentType, name);
        }

        // NOTES
        //
        // ContentRepository.PersistNewItem calls UpdatePropertyTags if
        //  the content is published
        // ContentRepository.PersistUpdatedItem calls UpdatePropertyTags if
        //  the content is newly published and ClearEntityTags if the
        //  content has just been trashed or unpublished
        // FIXED: remove tags ONLY if explicitely unpublished else not
        //  and TagQuery takes care of false positives (see below)
        //
        // Database contains
        //  cmsTags (id, tag, group, parent)
        //  cmsTagRelationship (nodeId, tagId, propertyTypeId)
        //
        // ClearEntityTags calls TagRepository.ClearTagsFromEntity which
        //  clears cmsTagRelationship entirely for that entity but does
        //  not touch cmsTags
        // UpdatePropertyTags updates ??? depending on the property's
        //  PropertyTagBehavior which has been set when updating the tags,
        //  and indicate whether we want to remove, replace or merge tags.
        //  remove = TagRepository.RemoveTagsFromProperty
        //   clears cmsTagRelationship for the specified tags
        //  replace or merge = TagRepository.AssignTagsToProperty
        //   inserts missing tags in cmsTags
        //   inserts missing relationships in cmsTagRelationship
        //
        // all this is done with zany queries but they seem to work
        //
        // GetTagsForEntity will use cmsTagRelationship to find what
        //  tags are associated with the entity, regardless of whether
        //  it is published, or draft, or anything - just the entity
        // GetTaggedEntitiesByTag will use cmsTagRelationship to find
        //  entities with a given tag - just returning the entity ID
        //
        // TagQuery.GetContentByTag uses GetTaggedEntitiesByTag and then
        //  TypedContent(...) and then exclude nulls so it's checking that
        //  the content is actually published
        //
        // in which case we decide that
        // - we clear tags when an entity is explicitely unpublished
        // - we clear tags when it's masked (or trashed)
        // - we update tags anytime it's published
        //
        // Questions
        // - is there any place where we delete from cmsTags?
        // - not sure of what happens when a property with tags is deleted
        //
        // for medias & members, we only have to manage Trashed (see repos)

        [Test]
        public void Tags_When_EntityIsCreated_Nothing()
        {
            var contentService = ServiceContext.ContentService;

            var content = CreateTaggedContent("test");
            content.SetTags("tags", new[] { "hello", "world", "some", "tags" }, true);
            contentService.Save(content);

            var propertyTypeId = content.ContentType.PropertyTypes.Single(x => x.Alias == "tags").Id;

            // the property has been updated
            Assert.AreEqual(4, content.Properties["tags"].Value.ToString().Split(',').Distinct().Count());

            // the tags have not been updated
            Assert.AreEqual(0, DatabaseContext.Database.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM cmsTags"));

            // the relationships have not been updated
            Assert.AreEqual(0, DatabaseContext.Database.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM cmsTagRelationship WHERE nodeId=@nodeId AND propertyTypeId=@propTypeId",
                new { nodeId = content.Id, propTypeId = propertyTypeId }));

            var tagService = ServiceContext.TagService;
            var tags = tagService.GetTagsForEntity(content.Id);
            Assert.AreEqual(0, tags.Count());
            var allTags = tagService.GetAllContentTags(); // all that have a relation
            Assert.AreEqual(0, allTags.Count());
        }

        [Test]
        public void Tags_When_EntityIsPublished_CreateTagsAndRelations()
        {
            var contentService = ServiceContext.ContentService;

            var content = CreateTaggedContent("test");
            content.SetTags("tags", new[] { "hello", "world", "some", "tags" }, true);
            contentService.Publish(content);

            var propertyTypeId = content.ContentType.PropertyTypes.Single(x => x.Alias == "tags").Id;

            // the property has been updated
            Assert.AreEqual(4, content.Properties["tags"].Value.ToString().Split(',').Distinct().Count());

            // the tags have been created
            Assert.AreEqual(4, DatabaseContext.Database.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM cmsTags"));

            // the relationships have been updated
            Assert.AreEqual(4, DatabaseContext.Database.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM cmsTagRelationship WHERE nodeId=@nodeId AND propertyTypeId=@propTypeId",
                new { nodeId = content.Id, propTypeId = propertyTypeId }));

            var tagService = ServiceContext.TagService;
            var tags = tagService.GetTagsForEntity(content.Id);
            Assert.AreEqual(4, tags.Count());
            var allTags = tagService.GetAllContentTags(); // all that have a relation
            Assert.AreEqual(4, allTags.Count());
        }

        [Test]
        public void Tags_When_PublishedEntityIsSaved_Nothing()
        {
            var contentService = ServiceContext.ContentService;

            var content = CreateTaggedContent("test");
            content.SetTags("tags", new[] { "hello", "world", "some", "tags" }, true);
            contentService.Publish(content);

            content.SetTags("tags", new[] { "another", "world" }, false);
            contentService.Save(content);

            var propertyTypeId = content.ContentType.PropertyTypes.Single(x => x.Alias == "tags").Id;

            // the property has been updated
            Assert.AreEqual(5, content.Properties["tags"].Value.ToString().Split(',').Distinct().Count());

            // the tags have not been updated
            Assert.AreEqual(4, DatabaseContext.Database.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM cmsTags"));

            // the relationships have not been updated
            Assert.AreEqual(4, DatabaseContext.Database.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM cmsTagRelationship WHERE nodeId=@nodeId AND propertyTypeId=@propTypeId",
                new { nodeId = content.Id, propTypeId = propertyTypeId }));

            var tagService = ServiceContext.TagService;
            var tags = tagService.GetTagsForEntity(content.Id);
            Assert.AreEqual(4, tags.Count());
            var allTags = tagService.GetAllContentTags(); // all that have a relation
            Assert.AreEqual(4, allTags.Count());
        }

        [Test]
        public void Tags_When_PublishedEntityIsUnpublished_ClearRelations1()
        {
            var contentService = ServiceContext.ContentService;

            var content = CreateTaggedContent("test");
            content.SetTags("tags", new[] { "hello", "world", "some", "tags" }, true);
            contentService.Publish(content);
            contentService.UnPublish(content);

            var propertyTypeId = content.ContentType.PropertyTypes.Single(x => x.Alias == "tags").Id;

            // the property has been updated
            Assert.AreEqual(4, content.Properties["tags"].Value.ToString().Split(',').Distinct().Count());

            // the tags have not been updated
            Assert.AreEqual(4, DatabaseContext.Database.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM cmsTags"));

            // the relationships have been updated
            Assert.AreEqual(0, DatabaseContext.Database.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM cmsTagRelationship WHERE nodeId=@nodeId AND propertyTypeId=@propTypeId",
                new { nodeId = content.Id, propTypeId = propertyTypeId }));

            var tagService = ServiceContext.TagService;
            var tags = tagService.GetTagsForEntity(content.Id);
            Assert.AreEqual(0, tags.Count());
            var allTags = tagService.GetAllContentTags(); // all that have a relation
            Assert.AreEqual(0, allTags.Count());
        }

        [Test]
        public void Tags_When_PublishedEntityIsUnpublished_ClearRelations2()
        {
            var contentService = ServiceContext.ContentService;

            var content1 = CreateTaggedContent("Tagged content 1");
            content1.SetTags("tags", new[] { "hello", "world", "some", "tags" }, true);
            contentService.Publish(content1);

            var content2 = CreateTaggedContent("Tagged content 2");
            content2.SetTags("tags", new[] { "hello", "world", "some", "tags" }, true);
            contentService.Publish(content2);

            contentService.UnPublish(content1);

            var propertyTypeId = content1.ContentType.PropertyTypes.Single(x => x.Alias == "tags").Id;

            // the property has been updated
            Assert.AreEqual(4, content1.Properties["tags"].Value.ToString().Split(',').Distinct().Count());
            Assert.AreEqual(4, content2.Properties["tags"].Value.ToString().Split(',').Distinct().Count());

            // the tags have not been updated
            Assert.AreEqual(4, DatabaseContext.Database.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM cmsTags"));

            // the relationships have (not) been updated
            Assert.AreEqual(0, DatabaseContext.Database.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM cmsTagRelationship WHERE nodeId=@nodeId AND propertyTypeId=@propTypeId",
                new { nodeId = content1.Id, propTypeId = propertyTypeId }));
            Assert.AreEqual(4, DatabaseContext.Database.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM cmsTagRelationship WHERE nodeId=@nodeId AND propertyTypeId=@propTypeId",
                new { nodeId = content2.Id, propTypeId = propertyTypeId }));

            var tagService = ServiceContext.TagService;
            var tags = tagService.GetTagsForEntity(content1.Id);
            Assert.AreEqual(0, tags.Count());
            tags = tagService.GetTagsForEntity(content2.Id);
            Assert.AreEqual(4, tags.Count());
            var allTags = tagService.GetAllContentTags(); // all that have a relation
            Assert.AreEqual(4, allTags.Count());
        }

        [Test]
        public void Tags_When_PublishedEntityIsUnpublished_ClearRelations3()
        {
            var contentService = ServiceContext.ContentService;

            var content1 = CreateTaggedContent("Tagged content 1");
            content1.SetTags("tags", new[] { "hello", "world", "some", "tags" }, true);
            contentService.Publish(content1);

            var content2 = CreateTaggedContent("Tagged content 2");
            content2.SetTags("tags", new[] { "hello", "world", "some", "tags" }, true);
            contentService.Publish(content2);

            contentService.UnPublish(content1);
            contentService.UnPublish(content2);

            var propertyTypeId = content1.ContentType.PropertyTypes.Single(x => x.Alias == "tags").Id;

            // the property has been updated
            Assert.AreEqual(4, content1.Properties["tags"].Value.ToString().Split(',').Distinct().Count());
            Assert.AreEqual(4, content2.Properties["tags"].Value.ToString().Split(',').Distinct().Count());

            // the tags have not been updated
            Assert.AreEqual(4, DatabaseContext.Database.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM cmsTags"));

            // the relationships have been updated
            Assert.AreEqual(0, DatabaseContext.Database.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM cmsTagRelationship WHERE nodeId=@nodeId AND propertyTypeId=@propTypeId",
                new { nodeId = content1.Id, propTypeId = propertyTypeId }));
            Assert.AreEqual(0, DatabaseContext.Database.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM cmsTagRelationship WHERE nodeId=@nodeId AND propertyTypeId=@propTypeId",
                new { nodeId = content2.Id, propTypeId = propertyTypeId }));

            var tagService = ServiceContext.TagService;
            var tags = tagService.GetTagsForEntity(content1.Id);
            Assert.AreEqual(0, tags.Count());
            tags = tagService.GetTagsForEntity(content2.Id);
            Assert.AreEqual(0, tags.Count());
            var allTags = tagService.GetAllContentTags(); // all that have a relation
            Assert.AreEqual(0, allTags.Count());
        }

        [Test]
        public void Tags_When_PublishedEntityIsTrashed_ClearRelations1()
        {
            var contentService = ServiceContext.ContentService;

            var content = CreateTaggedContent("test");
            content.SetTags("tags", new[] { "hello", "world", "some", "tags" }, true);
            contentService.Publish(content);
            contentService.MoveToRecycleBin(content); // because it unpublishes!

            var propertyTypeId = content.ContentType.PropertyTypes.Single(x => x.Alias == "tags").Id;

            // the property has been updated
            Assert.AreEqual(4, content.Properties["tags"].Value.ToString().Split(',').Distinct().Count());

            // the tags have not been updated
            Assert.AreEqual(4, DatabaseContext.Database.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM cmsTags"));

            // the relationships have been updated
            Assert.AreEqual(0, DatabaseContext.Database.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM cmsTagRelationship WHERE nodeId=@nodeId AND propertyTypeId=@propTypeId",
                new { nodeId = content.Id, propTypeId = propertyTypeId }));

            var tagService = ServiceContext.TagService;
            var tags = tagService.GetTagsForEntity(content.Id);
            Assert.AreEqual(0, tags.Count());
            var allTags = tagService.GetAllContentTags(); // all that have a relation
            Assert.AreEqual(0, allTags.Count());
        }

        [Test]
        public void Tags_When_PublishedEntityIsTrashed_ClearRelations2()
        {
            var contentService = ServiceContext.ContentService;

            var content = CreateTaggedContent("test");
            content.SetTags("tags", new[] { "hello", "world", "some", "tags" }, true);
            contentService.Publish(content);

            var content2 = CreateTaggedContent("test");
            content2.SetTags("tags", new[] { "hello", "world" }, true);
            contentService.Publish(content2);

            contentService.MoveToRecycleBin(content); // because it unpublishes!

            var propertyTypeId = content.ContentType.PropertyTypes.Single(x => x.Alias == "tags").Id;

            // the property has been updated
            Assert.AreEqual(4, content.Properties["tags"].Value.ToString().Split(',').Distinct().Count());

            // the tags have not been updated
            Assert.AreEqual(4, DatabaseContext.Database.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM cmsTags"));

            // the relationships have been updated
            Assert.AreEqual(0, DatabaseContext.Database.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM cmsTagRelationship WHERE nodeId=@nodeId AND propertyTypeId=@propTypeId",
                new { nodeId = content.Id, propTypeId = propertyTypeId }));

            var tagService = ServiceContext.TagService;
            var tags = tagService.GetTagsForEntity(content.Id);
            Assert.AreEqual(0, tags.Count());
            var allTags = tagService.GetAllContentTags(); // all that have a relation
            Assert.AreEqual(2, allTags.Count());
        }

        [Test]
        public void Tags_When_PublishedEntityIsTrashed_ClearRelations3()
        {
            var contentService = ServiceContext.ContentService;

            var content = CreateTaggedContent("test");
            content.SetTags("tags", new[] { "hello", "world", "some", "tags" }, true);
            contentService.Publish(content);

            var content2 = CreateTaggedContent("test");
            content2.SetTags("tags", new[] { "hello", "world" }, true);
            contentService.Publish(content2);

            contentService.MoveToRecycleBin(content);
            contentService.MoveToRecycleBin(content2);

            var propertyTypeId = content.ContentType.PropertyTypes.Single(x => x.Alias == "tags").Id;

            // the property has been updated
            Assert.AreEqual(4, content.Properties["tags"].Value.ToString().Split(',').Distinct().Count());

            // the tags have not been updated
            Assert.AreEqual(4, DatabaseContext.Database.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM cmsTags"));

            // the relationships have been updated
            Assert.AreEqual(0, DatabaseContext.Database.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM cmsTagRelationship WHERE nodeId=@nodeId AND propertyTypeId=@propTypeId",
                new { nodeId = content.Id, propTypeId = propertyTypeId }));

            var tagService = ServiceContext.TagService;
            var tags = tagService.GetTagsForEntity(content.Id);
            Assert.AreEqual(0, tags.Count());
            var allTags = tagService.GetAllContentTags(); // all that have a relation
            Assert.AreEqual(0, allTags.Count());
        }

        [Test]
        public void Tags_When_PublishedEntityIsUpdatedAndPublished_UpdateTagsAndRelations1()
        {
            var contentService = ServiceContext.ContentService;

            var content = CreateTaggedContent("test");
            content.SetTags("tags", new[] { "hello", "world", "some", "tags" }, true);
            contentService.PublishWithStatus(content);

            content.SetTags("tags", new[] { "another", "world" }, false);
            contentService.PublishWithStatus(content);

            var propertyTypeId = content.ContentType.PropertyTypes.Single(x => x.Alias == "tags").Id;

            // the property has been updated
            Assert.AreEqual(5, content.Properties["tags"].Value.ToString().Split(',').Distinct().Count());

            // the tags have been updated
            Assert.AreEqual(5, DatabaseContext.Database.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM cmsTags"));

            // the relationships have been updated
            Assert.AreEqual(5, DatabaseContext.Database.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM cmsTagRelationship WHERE nodeId=@nodeId AND propertyTypeId=@propTypeId",
                new { nodeId = content.Id, propTypeId = propertyTypeId }));

            var tagService = ServiceContext.TagService;
            var tags = tagService.GetTagsForEntity(content.Id);
            Assert.AreEqual(5, tags.Count());
            var allTags = tagService.GetAllContentTags(); // all that have a relation
            Assert.AreEqual(5, allTags.Count());
        }

        [Test]
        public void Tags_When_PublishedEntityIsUpdatedAndPublished_UpdateTagsAndRelations2()
        {
            var contentService = ServiceContext.ContentService;

            var content = CreateTaggedContent("test");
            content.SetTags("tags", new[] { "hello", "world", "some", "tags" }, true);
            contentService.PublishWithStatus(content);

            content.RemoveTags("tags", new[] { "some", "world" });
            contentService.PublishWithStatus(content);

            var content2 = CreateTaggedContent("test2");
            content2.SetTags("tags", new[] { "hello", "foo", "bar" }, true);
            contentService.PublishWithStatus(content2);

            var propertyTypeId = content.ContentType.PropertyTypes.Single(x => x.Alias == "tags").Id;

            // the property has been updated
            Assert.AreEqual(2, content.Properties["tags"].Value.ToString().Split(',').Distinct().Count());

            // the tags have not been updated
            Assert.AreEqual(6, DatabaseContext.Database.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM cmsTags"));

            // the relationships have been updated
            Assert.AreEqual(2, DatabaseContext.Database.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM cmsTagRelationship WHERE nodeId=@nodeId AND propertyTypeId=@propTypeId",
                new { nodeId = content.Id, propTypeId = propertyTypeId }));

            var tagService = ServiceContext.TagService;
            var tags = tagService.GetTagsForEntity(content.Id);
            Assert.AreEqual(2, tags.Count());
            var allTags = tagService.GetAllContentTags(); // all that have a relation
            Assert.AreEqual(4, allTags.Count());
        }

        [Test]
        public void Tags_When_MovingMaskingEntity_ClearRelations()
        {
            var contentService = ServiceContext.ContentService;

            var content1 = CreateTaggedContent("Tagged content 1");
            content1.SetTags("tags", new[] { "hello", "world", "some", "tags" }, true);
            contentService.Publish(content1);

            var content2 = CreateTaggedContent("Tagged content 2");
            content2.SetTags("tags", new[] { "hello", "world", "some", "tags" }, true);
            contentService.Publish(content2);

            contentService.UnPublish(content2);
            contentService.Move(content1, content2.Id);

            var propertyTypeId = content1.ContentType.PropertyTypes.Single(x => x.Alias == "tags").Id;

            // the property has been updated
            Assert.AreEqual(4, content1.Properties["tags"].Value.ToString().Split(',').Distinct().Count());
            Assert.AreEqual(4, content2.Properties["tags"].Value.ToString().Split(',').Distinct().Count());

            // the tags have not been updated
            Assert.AreEqual(4, DatabaseContext.Database.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM cmsTags"));

            // the relationships have been updated
            Assert.AreEqual(0, DatabaseContext.Database.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM cmsTagRelationship WHERE nodeId=@nodeId AND propertyTypeId=@propTypeId",
                new { nodeId = content1.Id, propTypeId = propertyTypeId }));
            Assert.AreEqual(0, DatabaseContext.Database.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM cmsTagRelationship WHERE nodeId=@nodeId AND propertyTypeId=@propTypeId",
                new { nodeId = content2.Id, propTypeId = propertyTypeId }));

            var tagService = ServiceContext.TagService;
            var tags = tagService.GetTagsForEntity(content1.Id);
            Assert.AreEqual(0, tags.Count());
            tags = tagService.GetTagsForEntity(content2.Id);
            Assert.AreEqual(0, tags.Count());
            var allTags = tagService.GetAllContentTags(); // all that have a relation
            Assert.AreEqual(0, allTags.Count());
        }

        [Test]
        public void Tags_When_MovingUnmaskingEntity_UpdateRelations()
        {
            var contentService = ServiceContext.ContentService;

            var content1 = CreateTaggedContent("Tagged content 1");
            content1.SetTags("tags", new[] { "hello", "world", "some", "tags" }, true);
            contentService.Publish(content1);

            var content2 = CreateTaggedContent("Tagged content 2");
            content2.SetTags("tags", new[] { "hello", "world", "some", "tags" }, true);
            contentService.Publish(content2);

            contentService.UnPublish(content2);
            contentService.Move(content1, content2.Id);
            contentService.Move(content1, -1);

            var propertyTypeId = content1.ContentType.PropertyTypes.Single(x => x.Alias == "tags").Id;

            // the property has been updated
            Assert.AreEqual(4, content1.Properties["tags"].Value.ToString().Split(',').Distinct().Count());
            Assert.AreEqual(4, content2.Properties["tags"].Value.ToString().Split(',').Distinct().Count());

            // the tags have not been updated
            Assert.AreEqual(4, DatabaseContext.Database.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM cmsTags"));

            // the relationships have been updated
            Assert.AreEqual(4, DatabaseContext.Database.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM cmsTagRelationship WHERE nodeId=@nodeId AND propertyTypeId=@propTypeId",
                new { nodeId = content1.Id, propTypeId = propertyTypeId }));
            Assert.AreEqual(0, DatabaseContext.Database.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM cmsTagRelationship WHERE nodeId=@nodeId AND propertyTypeId=@propTypeId",
                new { nodeId = content2.Id, propTypeId = propertyTypeId }));

            var tagService = ServiceContext.TagService;
            var tags = tagService.GetTagsForEntity(content1.Id);
            Assert.AreEqual(4, tags.Count());
            tags = tagService.GetTagsForEntity(content2.Id);
            Assert.AreEqual(0, tags.Count());
            var allTags = tagService.GetAllContentTags(); // all that have a relation
            Assert.AreEqual(4, allTags.Count());
        }

        [Test]
        public void Tags_When_TrashingBranch_ClearRelations()
        {
            var contentService = ServiceContext.ContentService;

            var content1 = CreateTaggedContent("Tagged content 1");
            content1.SetTags("tags", new[] { "hello", "world", "some", "tags" }, true);
            contentService.Publish(content1);

            var content2 = CreateTaggedContent("Tagged content 2");
            content2.SetTags("tags", new[] { "hello", "world", "some", "tags" }, true);
            contentService.Publish(content2);

            contentService.Move(content1, content2.Id);
            contentService.MoveToRecycleBin(content2);

            var propertyTypeId = content1.ContentType.PropertyTypes.Single(x => x.Alias == "tags").Id;

            // the property has been updated
            Assert.AreEqual(4, content1.Properties["tags"].Value.ToString().Split(',').Distinct().Count());
            Assert.AreEqual(4, content2.Properties["tags"].Value.ToString().Split(',').Distinct().Count());

            // the tags have not been updated
            Assert.AreEqual(4, DatabaseContext.Database.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM cmsTags"));

            // the relationships have been updated
            Assert.AreEqual(0, DatabaseContext.Database.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM cmsTagRelationship WHERE nodeId=@nodeId AND propertyTypeId=@propTypeId",
                new { nodeId = content1.Id, propTypeId = propertyTypeId }));
            Assert.AreEqual(0, DatabaseContext.Database.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM cmsTagRelationship WHERE nodeId=@nodeId AND propertyTypeId=@propTypeId",
                new { nodeId = content2.Id, propTypeId = propertyTypeId }));

            var tagService = ServiceContext.TagService;
            var tags = tagService.GetTagsForEntity(content1.Id);
            Assert.AreEqual(0, tags.Count());
            tags = tagService.GetTagsForEntity(content2.Id);
            Assert.AreEqual(0, tags.Count());
            var allTags = tagService.GetAllContentTags(); // all that have a relation
            Assert.AreEqual(0, allTags.Count());
        }

        [Test]
        public void Tags_When_EntityIsUnpublishedAndRepublished_ClearAndUpdateTagsAndRelations()
        {
            var contentService = ServiceContext.ContentService;

            var content1 = CreateTaggedContent("Tagged content 1");
            content1.SetTags("tags", new[] { "hello", "world", "some", "tags" }, true);
            contentService.Publish(content1);

            var content2 = CreateTaggedContent("Tagged content 2");
            content2.SetTags("tags", new[] { "hello", "world", "some", "things" }, true);
            contentService.Publish(content2);

            contentService.UnPublish(content1);
            contentService.UnPublish(content2);

            contentService.Publish(content1);

            var propertyTypeId = content1.ContentType.PropertyTypes.Single(x => x.Alias == "tags").Id;

            // the property has (not) been updated
            Assert.AreEqual(4, content1.Properties["tags"].Value.ToString().Split(',').Distinct().Count());
            Assert.AreEqual(4, content2.Properties["tags"].Value.ToString().Split(',').Distinct().Count());

            // the tags have (not) been updated
            Assert.AreEqual(5, DatabaseContext.Database.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM cmsTags"));

            // the relationships have (not) been updated
            Assert.AreEqual(4, DatabaseContext.Database.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM cmsTagRelationship WHERE nodeId=@nodeId AND propertyTypeId=@propTypeId",
                new { nodeId = content1.Id, propTypeId = propertyTypeId }));
            Assert.AreEqual(0, DatabaseContext.Database.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM cmsTagRelationship WHERE nodeId=@nodeId AND propertyTypeId=@propTypeId",
                new { nodeId = content2.Id, propTypeId = propertyTypeId }));

            var tagService = ServiceContext.TagService;
            var tags = tagService.GetTagsForEntity(content1.Id);
            Assert.AreEqual(4, tags.Count());
            tags = tagService.GetTagsForEntity(content2.Id);
            Assert.AreEqual(0, tags.Count());
            var allTags = tagService.GetAllContentTags(); // all that have a relation
            Assert.AreEqual(4, allTags.Count());
        }

        [Test]
        public void Tags_When_PublishingBranch()
        {
            var contentService = ServiceContext.ContentService;

            var contentTypeService = ServiceContext.ContentTypeService;
            var dataTypeService = ServiceContext.DataTypeService;

            dataTypeService.SavePreValues(1041, new Dictionary<string, PreValue>
            {
                {"group", new PreValue("test")},
                {"storageType", new PreValue("Csv")}
            });

            var contentType = MockedContentTypes.CreateSimpleContentType("umbMandatory", "Mandatory Doc Type", true);
            contentType.PropertyGroups.First().PropertyTypes.Add(
                new PropertyType("test", DataTypeDatabaseType.Ntext, "tags")
                {
                    DataTypeDefinitionId = 1041
                });
            //contentTypeService.Save(contentType);
            contentType.AllowedContentTypes = new[] { new ContentTypeSort(new Lazy<int>(() => contentType.Id), 0, contentType.Alias) };
            contentTypeService.Save(contentType);

            var content = MockedContent.CreateSimpleContent(contentType, "Tagged content");
            content.SetTags("tags", new[] { "hello", "world", "some", "tags" }, true);
            contentService.Save(content);

            var child1 = MockedContent.CreateSimpleContent(contentType, "child 1 content", content.Id);
            child1.SetTags("tags", new[] { "hello1", "world1", "some1" }, true);
            contentService.Save(child1);

            var child2 = MockedContent.CreateSimpleContent(contentType, "child 2 content", content.Id);
            child2.SetTags("tags", new[] { "hello2", "world2" }, true);
            contentService.Save(child2);

            contentService.PublishWithChildrenWithStatus(content, includeUnpublished: true);

            var propertyTypeId = contentType.PropertyTypes.Single(x => x.Alias == "tags").Id;

            // the relationships have been updated
            Assert.AreEqual(4, DatabaseContext.Database.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM cmsTagRelationship WHERE nodeId=@nodeId AND propertyTypeId=@propTypeId",
                new { nodeId = content.Id, propTypeId = propertyTypeId }));
            Assert.AreEqual(3, DatabaseContext.Database.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM cmsTagRelationship WHERE nodeId=@nodeId AND propertyTypeId=@propTypeId",
                new { nodeId = child1.Id, propTypeId = propertyTypeId }));
            Assert.AreEqual(2, DatabaseContext.Database.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM cmsTagRelationship WHERE nodeId=@nodeId AND propertyTypeId=@propTypeId",
                new { nodeId = child2.Id, propTypeId = propertyTypeId }));

            // the tags have been updated
            Assert.AreEqual(9, DatabaseContext.Database.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM cmsTags"));
        }

        [Test]
        public void Tags_When_EntityIsCopied_Nothing()
        {
            var contentService = ServiceContext.ContentService;

            var content = CreateTaggedContent("test");
            content.SetTags("tags", new[] { "hello", "world" }, true);
            contentService.PublishWithStatus(content);

            var copy = contentService.Copy(content, content.ParentId, false);

            var propertyTypeId = content.ContentType.PropertyTypes.Single(x => x.Alias == "tags").Id;

            // the property has (not) been updated
            Assert.AreEqual(2, content.Properties["tags"].Value.ToString().Split(',').Distinct().Count());
            Assert.AreEqual(2, copy.Properties["tags"].Value.ToString().Split(',').Distinct().Count());

            // the tags have (not) been updated
            Assert.AreEqual(2, DatabaseContext.Database.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM cmsTags"));

            // the relationships have (not) been updated
            Assert.AreEqual(2, DatabaseContext.Database.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM cmsTagRelationship WHERE nodeId=@nodeId AND propertyTypeId=@propTypeId",
                new { nodeId = content.Id, propTypeId = propertyTypeId }));
            Assert.AreEqual(0, DatabaseContext.Database.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM cmsTagRelationship WHERE nodeId=@nodeId AND propertyTypeId=@propTypeId",
                new { nodeId = copy.Id, propTypeId = propertyTypeId }));

            var tagService = ServiceContext.TagService;
            var tags = tagService.GetTagsForEntity(content.Id);
            Assert.AreEqual(2, tags.Count());
            tags = tagService.GetTagsForEntity(copy.Id);
            Assert.AreEqual(0, tags.Count());
            var allTags = tagService.GetAllContentTags(); // all that have a relation
            Assert.AreEqual(2, allTags.Count());
        }

        #endregion

	    [Test]
	    public void Can_Remove_Property_Type()
	    {
            // Arrange
            var contentService = ServiceContext.ContentService;

            // Act
            var content = contentService.CreateContent("Test", -1, "umbTextpage", 0);

            // Assert
            Assert.That(content, Is.Not.Null);
            Assert.That(content.HasIdentity, Is.False);
	    }

	    [Test]
        public void Can_Create_Content()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;

            // Act
            var content = contentService.CreateContent("Test", -1, "umbTextpage", 0);

            // Assert
            Assert.That(content, Is.Not.Null);
            Assert.That(content.HasIdentity, Is.False);
        }
        
        [Test]
        public void Can_Create_Content_Without_Explicitly_Set_User()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;

            // Act
            var content = contentService.CreateContent("Test", -1, "umbTextpage");

            // Assert
            Assert.That(content, Is.Not.Null);
            Assert.That(content.HasIdentity, Is.False);
            Assert.That(content.CreatorId, Is.EqualTo(0));//Default to zero/administrator
        }

        [Test]
        public void Can_Save_New_Content_With_Explicit_User()
        {
            var user = new User(ServiceContext.UserService.GetUserTypeByAlias("admin"))
                {
                    Name = "Test",
                    Email = "test@test.com",
                    Username = "test",
                RawPasswordValue = "test"
                };
            ServiceContext.UserService.Save(user);
            var content = new Content("Test", -1, ServiceContext.ContentTypeService.Get("umbTextpage"));

            // Act
            ServiceContext.ContentService.Save(content, (int)user.Id);

            // Assert
            Assert.That(content.CreatorId, Is.EqualTo(user.Id));
            Assert.That(content.WriterId, Is.EqualTo(user.Id));
        }

        [Test]
        public void Cannot_Create_Content_With_Non_Existing_ContentType_Alias()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;

            // Act & Assert
            Assert.Throws<Exception>(() => contentService.CreateContent("Test", -1, "umbAliasDoesntExist"));
        }

        [Test]
        public void Can_Get_Content_By_Id()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;

            // Act
            var content = contentService.GetById(NodeDto.NodeIdSeed + 1);

            // Assert
            Assert.That(content, Is.Not.Null);
            Assert.That(content.Id, Is.EqualTo(NodeDto.NodeIdSeed + 1));
        }

        [Test]
        public void Can_Get_Content_By_Guid_Key()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;

            // Act
            var content = contentService.GetById(new Guid("B58B3AD4-62C2-4E27-B1BE-837BD7C533E0"));

            // Assert
            Assert.That(content, Is.Not.Null);
            Assert.That(content.Id, Is.EqualTo(NodeDto.NodeIdSeed + 1));
        }

        [Test]
        public void Can_Get_Content_By_Level()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;

            // Act
            var contents = contentService.GetByLevel(2).ToList();

            // Assert
            Assert.That(contents, Is.Not.Null);
            Assert.That(contents.Any(), Is.True);
            Assert.That(contents.Count(), Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void Can_Get_Children_Of_Content_Id()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;

            // Act
            var contents = contentService.GetChildren(NodeDto.NodeIdSeed + 1).ToList();

            // Assert
            Assert.That(contents, Is.Not.Null);
            Assert.That(contents.Any(), Is.True);
            Assert.That(contents.Count(), Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void Can_Get_Descendents_Of_Content()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;
            var hierarchy = CreateContentHierarchy();
            contentService.Save(hierarchy, 0);

            // Act
            var contents = contentService.GetDescendants(NodeDto.NodeIdSeed + 1).ToList();

            // Assert
            Assert.That(contents, Is.Not.Null);
            Assert.That(contents.Any(), Is.True);
            Assert.That(contents.Count(), Is.EqualTo(52));
        }

        [Test]
        public void Can_Get_All_Versions_Of_Content()
        {
            var contentService = ServiceContext.ContentService;

            var parent = contentService.GetById(NodeDto.NodeIdSeed + 1);
            contentService.Publish(parent); // publishing root, so Text Page 2 can be updated.
            var subpage2 = contentService.GetById(NodeDto.NodeIdSeed + 3);
            subpage2.Name = "Text Page 2 Updated";
            subpage2.SetValue("author", "Jane Doe");

            // publishing an unpublished content does not create a new version
            contentService.SaveAndPublishWithStatus(subpage2);

            // publishing *again* does create a new version
            subpage2.SetValue("author", "Jane Dox");
            contentService.SaveAndPublishWithStatus(subpage2);

            var versions = contentService.GetVersions(NodeDto.NodeIdSeed + 3).ToList();

            Assert.That(versions.Any(), Is.True);
            Assert.That(versions.Count(), Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void Can_Get_Root_Content()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;

            // Act
            var contents = contentService.GetRootContent().ToList();

            // Assert
            Assert.That(contents, Is.Not.Null);
            Assert.That(contents.Any(), Is.True);
            Assert.That(contents.Count(), Is.EqualTo(1));
        }

        [Test]
        public void Can_Get_Content_For_Expiration()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;
            var root = contentService.GetById(NodeDto.NodeIdSeed + 1);
            contentService.SaveAndPublishWithStatus(root);
            var content = contentService.GetById(NodeDto.NodeIdSeed + 3);
            content.ExpireDate = DateTime.Now.AddSeconds(1);
            contentService.SaveAndPublishWithStatus(content);

            // Act
            Thread.Sleep(new TimeSpan(0, 0, 0, 2));
            var contents = contentService.GetContentForExpiration().ToList();

            // Assert
            Assert.That(contents, Is.Not.Null);
            Assert.That(contents.Any(), Is.True);
            Assert.That(contents.Count(), Is.EqualTo(1));
        }

        [Test]
        public void Can_Get_Content_For_Release()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;

            // Act
            var contents = contentService.GetContentForRelease().ToList();

            // Assert
            Assert.That(DateTime.Now.AddMinutes(-5) <= DateTime.Now);
            Assert.That(contents, Is.Not.Null);
            Assert.That(contents.Any(), Is.True);
            Assert.That(contents.Count(), Is.EqualTo(1));
        }

        [Test]
        public void Can_Get_Content_In_RecycleBin()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;

            // Act
            var contents = contentService.GetContentInRecycleBin().ToList();

            // Assert
            Assert.That(contents, Is.Not.Null);
            Assert.That(contents.Any(), Is.True);
            Assert.That(contents.Count(), Is.EqualTo(1));
        }

        [Test]
        public void Can_UnPublish_Content()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;
            var content = contentService.GetById(NodeDto.NodeIdSeed + 1);
            bool published = contentService.Publish(content, 0);

            var provider = new PetaPocoUnitOfWorkProvider(Logger);
            var uow = provider.GetUnitOfWork();
            Assert.IsTrue(uow.Database.Exists<ContentXmlDto>(content.Id));

            // Act
            bool unpublished = contentService.UnPublish(content, 0);

            // Assert
            Assert.That(published, Is.True);
            Assert.That(unpublished, Is.True);
            Assert.That(content.Published, Is.False);

            uow = provider.GetUnitOfWork();
            Assert.IsFalse(uow.Database.Exists<ContentXmlDto>(content.Id));
        }

        /// <summary>
        /// This test is ignored because the way children are handled when
        /// parent is unpublished is treated differently now then from when this test
        /// was written.
        /// The correct case is now that Root is UnPublished removing the children
        /// from cache, but still having them "Published" in the "background".
        /// Once the Parent is Published the Children should re-appear as published.
        /// </summary>
        [Test, NUnit.Framework.Ignore]
        public void Can_UnPublish_Root_Content_And_Verify_Children_Is_UnPublished()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;
            var published = contentService.RePublishAll(0);
            var content = contentService.GetById(NodeDto.NodeIdSeed + 1);

            // Act
            bool unpublished = contentService.UnPublish(content, 0);
            var children = contentService.GetChildren(NodeDto.NodeIdSeed + 1).ToList();

            // Assert
            Assert.That(published, Is.True);//Verify that everything was published

            //Verify that content with Id (NodeDto.NodeIdSeed + 1) was unpublished
            Assert.That(unpublished, Is.True);
            Assert.That(content.Published, Is.False);

            //Verify that all children was unpublished
            Assert.That(children.Any(x => x.Published), Is.False);
            Assert.That(children.First(x => x.Id == NodeDto.NodeIdSeed + 2).Published, Is.False);//Released 5 mins ago, but should be unpublished
            Assert.That(children.First(x => x.Id == NodeDto.NodeIdSeed + 2).ReleaseDate.HasValue, Is.False);//Verify that the release date has been removed
            Assert.That(children.First(x => x.Id == NodeDto.NodeIdSeed + 3).Published, Is.False);//Expired 5 mins ago, so isn't be published
        }

        [Test]
        [NUnit.Framework.Ignore("The whole \"republish all\" concept is obsolete.")]
        public void Can_RePublish_All_Content()
        {
            // Arrange
            var contentService = (ContentService) ServiceContext.ContentService;
            var rootContent = contentService.GetRootContent().ToList();
            foreach (var c in rootContent)
            {
                contentService.PublishWithChildren(c);
            }
            var allContent = rootContent.Concat(rootContent.SelectMany(x => x.Descendants()));
            //for testing we need to clear out the contentXml table so we can see if it worked
            var provider = new PetaPocoUnitOfWorkProvider(Logger);
            using (var uow = provider.GetUnitOfWork())
            {
                uow.Database.TruncateTable("cmsContentXml");
            }


            //for this test we are also going to save a revision for a content item that is not published, this is to ensure
            //that it's published version still makes it into the cmsContentXml table!
            contentService.Save(allContent.Last());

            // Act
            var published = contentService.RePublishAll(0);

            // Assert
            Assert.IsTrue(published);
            using (var uow = provider.GetUnitOfWork())
            {
                Assert.AreEqual(allContent.Count(),
                    uow.Database.ExecuteScalar<int>("select count(*) from cmsContentXml"));
            }
        }

        [Test]
        [NUnit.Framework.Ignore("The whole \"republish all\" concept is obsolete.")]
        public void Can_RePublish_All_Content_Of_Type()
        {
            // Arrange
            var contentService = (ContentService) ServiceContext.ContentService;
            var rootContent = contentService.GetRootContent().ToList();
            foreach (var c in rootContent)
            {
                contentService.PublishWithChildren(c);
            }
            var allContent = rootContent.Concat(rootContent.SelectMany(x => x.Descendants())).ToList();
            //for testing we need to clear out the contentXml table so we can see if it worked
            var provider = new PetaPocoUnitOfWorkProvider(Logger);

            using (var uow = provider.GetUnitOfWork())
            {
                uow.Database.TruncateTable("cmsContentXml");
            }
            //for this test we are also going to save a revision for a content item that is not published, this is to ensure
            //that it's published version still makes it into the cmsContentXml table!
            contentService.Save(allContent.Last());

            // Act
            contentService.RePublishAll(new int[] {allContent.Last().ContentTypeId});

            // Assert            
            using (var uow = provider.GetUnitOfWork())
            {
                Assert.AreEqual(allContent.Count(),
                    uow.Database.ExecuteScalar<int>("select count(*) from cmsContentXml"));
            }
        }

        [Test]
        public void Can_Publish_Content()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;
            var content = contentService.GetById(NodeDto.NodeIdSeed + 1);

            // Act
            bool published = contentService.Publish(content, 0);

            // Assert
            Assert.That(published, Is.True);
            Assert.That(content.Published, Is.True);
        }

        [Test]
        public void Can_Publish_Only_Valid_Content()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;
            var contentTypeService = ServiceContext.ContentTypeService;
            var contentType = MockedContentTypes.CreateSimpleContentType("umbMandatory", "Mandatory Doc Type", true);
            contentTypeService.Save(contentType);

            Content content = MockedContent.CreateSimpleContent(contentType, "Invalid Content", NodeDto.NodeIdSeed + 1);
            content.SetValue("author", string.Empty);
            contentService.Save(content, 0);

            // Act
            var parent = contentService.GetById(NodeDto.NodeIdSeed + 1);
            bool parentPublished = contentService.Publish(parent, 0);
            bool published = contentService.Publish(content, 0);

            // Assert
            Assert.That(parentPublished, Is.True);
            Assert.That(published, Is.False);
            Assert.That(content.IsValid(), Is.False);
            Assert.That(parent.Published, Is.True);
            Assert.That(content.Published, Is.False);
        }

        [Test]
        public void Can_Publish_Content_Children()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;
            var content = contentService.GetById(NodeDto.NodeIdSeed + 1);

            // Act
            bool published = contentService.PublishWithChildren(content, 0);
            var children = contentService.GetChildren(NodeDto.NodeIdSeed + 1);

            // Assert
            Assert.That(published, Is.True);//Nothing was cancelled, so should be true
            Assert.That(content.Published, Is.True);//No restrictions, so should be published
            Assert.That(children.First(x => x.Id == NodeDto.NodeIdSeed + 2).Published, Is.True);//Released 5 mins ago, so should be published
        }

        [Test]
        public void Cannot_Publish_Expired_Content()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;
            var content = contentService.GetById(NodeDto.NodeIdSeed + 3); //This Content expired 5min ago
            content.ExpireDate = DateTime.Now.AddMinutes(-5);
            contentService.Save(content);

            var parent = contentService.GetById(NodeDto.NodeIdSeed + 1);
            bool parentPublished = contentService.Publish(parent, 0);//Publish root Home node to enable publishing of 'NodeDto.NodeIdSeed + 3'

            // Act
            bool published = contentService.Publish(content, 0);

            // Assert
            Assert.That(parentPublished, Is.True);
            Assert.That(published, Is.False);
            Assert.That(content.Published, Is.False);
        }

        [Test]
        public void Cannot_Publish_Content_Awaiting_Release()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;
            var content = contentService.GetById(NodeDto.NodeIdSeed + 2);
            content.ReleaseDate = DateTime.Now.AddHours(2);
            contentService.Save(content, 0);

            var parent = contentService.GetById(NodeDto.NodeIdSeed + 1);
            bool parentPublished = contentService.Publish(parent, 0);//Publish root Home node to enable publishing of 'NodeDto.NodeIdSeed + 3'

            // Act
            bool published = contentService.Publish(content, 0);

            // Assert
            Assert.That(parentPublished, Is.True);
            Assert.That(published, Is.False);
            Assert.That(content.Published, Is.False);
        }

        [Test]
        public void Cannot_Publish_Content_Where_Parent_Is_Unpublished()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;
            var content = contentService.CreateContent("Subpage with Unpublisehed Parent", NodeDto.NodeIdSeed + 1, "umbTextpage", 0);
            contentService.Save(content, 0);

            // Act
            bool published = contentService.PublishWithChildren(content, 0);

            // Assert
            Assert.That(published, Is.False);
            Assert.That(content.Published, Is.False);
        }

        [Test]
        public void Cannot_Publish_Trashed_Content()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;
            var content = contentService.GetById(NodeDto.NodeIdSeed + 4);

            // Act
            bool published = contentService.Publish(content, 0);

            // Assert
            Assert.That(published, Is.False);
            Assert.That(content.Published, Is.False);
            Assert.That(content.Trashed, Is.True);
        }

        [Test]
        public void Can_Save_And_Publish_Content()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;
            var content = contentService.CreateContent("Home US", - 1, "umbTextpage", 0);
            content.SetValue("author", "Barack Obama");

            // Act
            bool published = contentService.SaveAndPublish(content, 0);

            // Assert
            Assert.That(content.HasIdentity, Is.True);
            Assert.That(content.Published, Is.True);
            Assert.That(published, Is.True);
        }

        [Test]
        public void Can_Get_Published_Descendant_Versions()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;
            var root = contentService.GetById(NodeDto.NodeIdSeed + 1);
            var rootPublished = contentService.Publish(root);
            var content = contentService.GetById(NodeDto.NodeIdSeed + 3);
            content.Properties["title"].Value = content.Properties["title"].Value + " Published";
            bool published = contentService.SaveAndPublish(content);

            var publishedVersion = content.Version;

            content.Properties["title"].Value = content.Properties["title"].Value + " Saved";
            contentService.Save(content);

            var savedVersion = content.Version;

            // Act
            var publishedDescendants = ((ContentService) contentService).GetPublishedDescendants(root).ToList();

            // Assert
            Assert.That(rootPublished, Is.True);
            Assert.That(published, Is.True);
            Assert.That(publishedDescendants.Any(x => x.Version == publishedVersion), Is.True);
            Assert.That(publishedDescendants.Any(x => x.Version == savedVersion), Is.False);

            //Ensure that the published content version has the correct property value and is marked as published
            var publishedContentVersion = publishedDescendants.First(x => x.Version == publishedVersion);
            Assert.That(publishedContentVersion.Published, Is.True);
            Assert.That(publishedContentVersion.Properties["title"].Value, Contains.Substring("Published"));

            //Ensure that the saved content version has the correct property value and is not marked as published
            var savedContentVersion = contentService.GetByVersion(savedVersion);
            Assert.That(savedContentVersion.Published, Is.False);
            Assert.That(savedContentVersion.Properties["title"].Value, Contains.Substring("Saved"));

            //Ensure that the latest version of the content is the saved and not-yet-published one
            var currentContent = contentService.GetById(NodeDto.NodeIdSeed + 3);
            Assert.That(currentContent.Published, Is.False);
            Assert.That(currentContent.Properties["title"].Value, Contains.Substring("Saved"));
            Assert.That(currentContent.Version, Is.EqualTo(savedVersion));
        }

        [Test]
        public void Can_Save_Content()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;
            var content = contentService.CreateContent("Home US", - 1, "umbTextpage", 0);
            content.SetValue("author", "Barack Obama");

            // Act
            contentService.Save(content, 0);

            // Assert
            Assert.That(content.HasIdentity, Is.True);
        }

        [Test]
        public void Can_Bulk_Save_Content()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;
            var contentTypeService = ServiceContext.ContentTypeService;

            var contentType = contentTypeService.Get("umbTextpage");
            Content subpage = MockedContent.CreateSimpleContent(contentType, "Text Subpage 1", NodeDto.NodeIdSeed + 2);
            Content subpage2 = MockedContent.CreateSimpleContent(contentType, "Text Subpage 2", NodeDto.NodeIdSeed + 2);
            var list = new List<IContent> {subpage, subpage2};

            // Act
            contentService.Save(list, 0);

            // Assert
            Assert.That(list.Any(x => !x.HasIdentity), Is.False);
        }

        [Test]
        public void Can_Bulk_Save_New_Hierarchy_Content()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;
            var hierarchy = CreateContentHierarchy().ToList();

            // Act
            contentService.Save(hierarchy, 0);

            Assert.That(hierarchy.Any(), Is.True);
			Assert.That(hierarchy.Any(x => x.HasIdentity == false), Is.False);
			//all parent id's should be ok, they are lazy and if they equal zero an exception will be thrown
			Assert.DoesNotThrow(() => hierarchy.Any(x => x.ParentId != 0));

        }

        [Test]
        public void Can_Delete_Content_Of_Specific_ContentType()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;
            var contentTypeService = ServiceContext.ContentTypeService;
            var contentType = contentTypeService.Get("umbTextpage");

            // Act
            contentService.DeleteContentOfType(contentType.Id);
            var rootContent = contentService.GetRootContent();
            var contents = contentService.GetContentOfContentType(contentType.Id);

            // Assert
            Assert.That(rootContent.Any(), Is.False);
            Assert.That(contents.Any(x => !x.Trashed), Is.False);
        }

        [Test]
        public void Can_Delete_Content()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;
            var content = contentService.GetById(NodeDto.NodeIdSeed + 4);

            // Act
            contentService.Delete(content, 0);
            var deleted = contentService.GetById(NodeDto.NodeIdSeed + 4);

            // Assert
            Assert.That(deleted, Is.Null);
        }

        [Test]
        public void Can_Move_Content_To_RecycleBin()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;
            var content = contentService.GetById(NodeDto.NodeIdSeed + 3);

            // Act
            contentService.MoveToRecycleBin(content, 0);

            // Assert
            Assert.That(content.ParentId, Is.EqualTo(-20));
            Assert.That(content.Trashed, Is.True);
        }

        [Test]
        public void Can_Move_Content_Structure_To_RecycleBin_And_Empty_RecycleBin()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;
            var contentType = ServiceContext.ContentTypeService.Get("umbTextpage");
            Content subsubpage = MockedContent.CreateSimpleContent(contentType, "Text Page 3", NodeDto.NodeIdSeed + 2);
            contentService.Save(subsubpage, 0);

            var content = contentService.GetById(NodeDto.NodeIdSeed + 1);

            // Act
            contentService.MoveToRecycleBin(content, 0);
            var descendants = contentService.GetDescendants(content).ToList();

            // Assert
            Assert.That(content.ParentId, Is.EqualTo(-20));
            Assert.That(content.Trashed, Is.True);
            Assert.That(descendants.Count(), Is.EqualTo(3));
            Assert.That(descendants.Any(x => x.Path.Contains("-20") == false), Is.False);

            //Empty Recycle Bin
            contentService.EmptyRecycleBin();
            var trashed = contentService.GetContentInRecycleBin();

            Assert.That(trashed.Any(), Is.False);
        }

        [Test]
        public void Can_Empty_RecycleBin()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;

            // Act
            contentService.EmptyRecycleBin();
            var contents = contentService.GetContentInRecycleBin();

            // Assert
            Assert.That(contents.Any(), Is.False);
        }

        [Test]
        public void Can_Move_Content()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;
            var content = contentService.GetById(NodeDto.NodeIdSeed + 4);

            // Act - moving out of recycle bin
            contentService.Move(content, NodeDto.NodeIdSeed + 1, 0);

            // Assert
            Assert.That(content.ParentId, Is.EqualTo(NodeDto.NodeIdSeed + 1));
            Assert.That(content.Trashed, Is.False);
            Assert.That(content.Published, Is.False);
        }

        [Test]
        public void Can_Copy_Content()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;
            var temp = contentService.GetById(NodeDto.NodeIdSeed + 3);

            // Act
            var copy = contentService.Copy(temp, temp.ParentId, false, 0);
            var content = contentService.GetById(NodeDto.NodeIdSeed + 3);

            // Assert
            Assert.That(copy, Is.Not.Null);
            Assert.That(copy.Id, Is.Not.EqualTo(content.Id));
            Assert.AreNotSame(content, copy);
            foreach (var property in copy.Properties)
            {
                Assert.AreNotEqual(property.Id, content.Properties[property.Alias].Id);
                Assert.AreEqual(property.Value, content.Properties[property.Alias].Value);
            }
            //Assert.AreNotEqual(content.Name, copy.Name);
        }

        [Test]
        public void Can_Copy_Recursive()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;
            var temp = contentService.GetById(NodeDto.NodeIdSeed + 1);
            Assert.AreEqual("Home", temp.Name);
            Assert.AreEqual(2, temp.Children().Count());

            // Act
            var copy = contentService.Copy(temp, temp.ParentId, false, true, 0);
            var content = contentService.GetById(NodeDto.NodeIdSeed + 1);

            // Assert
            Assert.That(copy, Is.Not.Null);
            Assert.That(copy.Id, Is.Not.EqualTo(content.Id));
            Assert.AreNotSame(content, copy);
            Assert.AreEqual(2, copy.Children().Count());

            var child = contentService.GetById(NodeDto.NodeIdSeed + 2);
            var childCopy = copy.Children().First();
            Assert.AreEqual(childCopy.Name, child.Name);
            Assert.AreNotEqual(childCopy.Id, child.Id);
            Assert.AreNotEqual(childCopy.Key, child.Key);
        }

        [Test]
        public void Can_Copy_NonRecursive()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;
            var temp = contentService.GetById(NodeDto.NodeIdSeed + 1);
            Assert.AreEqual("Home", temp.Name);
            Assert.AreEqual(2, temp.Children().Count());

            // Act
            var copy = contentService.Copy(temp, temp.ParentId, false, false, 0);
            var content = contentService.GetById(NodeDto.NodeIdSeed + 1);

            // Assert
            Assert.That(copy, Is.Not.Null);
            Assert.That(copy.Id, Is.Not.EqualTo(content.Id));
            Assert.AreNotSame(content, copy);
            Assert.AreEqual(0, copy.Children().Count());
        }

        [Test, NUnit.Framework.Ignore]
        public void Can_Send_To_Publication()
        { }

        [Test]
        public void Can_Rollback_Version_On_Content()
        {
            var contentService = ServiceContext.ContentService;

            var parent = contentService.GetById(NodeDto.NodeIdSeed + 1);
            contentService.Publish(parent); // publishing root, so Text Page 2 can be updated.

            var subpage2 = contentService.GetById(NodeDto.NodeIdSeed + 3);
            var version = subpage2.Version;

            // publishing an unpublished content does not create a new version
            subpage2.Name = "test 1";
            subpage2.SetValue("author", "Jane Doe");
            contentService.SaveAndPublishWithStatus(subpage2);

            // publishing *again* does create a new version
            subpage2.Name = "test 2";
            subpage2.SetValue("author", "Jane Dox");
            contentService.SaveAndPublishWithStatus(subpage2);

            var rollback = contentService.Rollback(NodeDto.NodeIdSeed + 3, version);

            Assert.IsNotNull(rollback);
            Assert.AreNotEqual(rollback.Version, subpage2.Version);
            Assert.AreEqual("Jane Doe", rollback.GetValue<string>("author"));
            Assert.IsFalse(rollback.Published);
            Assert.IsTrue(rollback.HasPublishedVersion);
            Assert.AreEqual(rollback.PublishedVersionGuid, subpage2.Version);
            Assert.AreEqual("test 1", rollback.Name);
        }

        [Test]
        public void Can_Save_Lazy_Content()
        {	        
	        var unitOfWork = PetaPocoUnitOfWorkProvider.CreateUnitOfWork(Mock.Of<ILogger>());
            var contentType = ServiceContext.ContentTypeService.Get("umbTextpage");
            var root = ServiceContext.ContentService.GetById(NodeDto.NodeIdSeed + 1);

            var c = new Lazy<IContent>(() => MockedContent.CreateSimpleContent(contentType, "Hierarchy Simple Text Page", root.Id));
            var c2 = new Lazy<IContent>(() => MockedContent.CreateSimpleContent(contentType, "Hierarchy Simple Text Subpage", c.Value.Id));
            var list = new List<Lazy<IContent>> {c, c2};

            ContentTypeRepository contentTypeRepository;
            using (var repository = CreateRepository(unitOfWork, out contentTypeRepository))
            {
                foreach (var content in list)
                {
                    repository.AddOrUpdate(content.Value);
                    unitOfWork.Commit();
                }

                Assert.That(c.Value.HasIdentity, Is.True);
                Assert.That(c2.Value.HasIdentity, Is.True);

                Assert.That(c.Value.Id > 0, Is.True);
                Assert.That(c2.Value.Id > 0, Is.True);

                Assert.That(c.Value.ParentId > 0, Is.True);
                Assert.That(c2.Value.ParentId > 0, Is.True);    
            }
            
        }

        [Test]
        public void Can_Verify_Content_Has_Published_Version()
        {
            // Arrange
            var contentService = ServiceContext.ContentService;
            var content = contentService.GetById(NodeDto.NodeIdSeed + 1);
            bool published = contentService.PublishWithChildren(content, 0);
            var homepage = contentService.GetById(NodeDto.NodeIdSeed + 1);
            homepage.Name = "Homepage";
            ServiceContext.ContentService.Save(homepage);

            // Act
            bool hasPublishedVersion = ServiceContext.ContentService.HasPublishedVersion(NodeDto.NodeIdSeed + 1);

            // Assert
            Assert.That(published, Is.True);
            Assert.That(homepage.Published, Is.False);
            Assert.That(hasPublishedVersion, Is.True);
        }

	    [Test]
	    public void Can_Verify_Property_Types_On_Content()
	    {
            // Arrange
	        var contentTypeService = ServiceContext.ContentTypeService;
            var contentType = MockedContentTypes.CreateAllTypesContentType("allDataTypes", "All DataTypes");
            contentTypeService.Save(contentType);
	        var contentService = ServiceContext.ContentService;
	        var content = MockedContent.CreateAllTypesContent(contentType, "Random Content", -1);
            contentService.Save(content);
	        var id = content.Id;

            // Act
	        var sut = contentService.GetById(id);

            // Arrange
            Assert.That(sut.GetValue<bool>("isTrue"), Is.True);
            Assert.That(sut.GetValue<int>("number"), Is.EqualTo(42));
            Assert.That(sut.GetValue<string>("bodyText"), Is.EqualTo("Lorem Ipsum Body Text Test"));
            Assert.That(sut.GetValue<string>("singleLineText"), Is.EqualTo("Single Line Text Test"));
            Assert.That(sut.GetValue<string>("multilineText"), Is.EqualTo("Multiple lines \n in one box"));
            Assert.That(sut.GetValue<string>("upload"), Is.EqualTo("/media/1234/koala.jpg"));
            Assert.That(sut.GetValue<string>("label"), Is.EqualTo("Non-editable label"));
            //SD: This is failing because the 'content' call to GetValue<DateTime> always has empty milliseconds
            //MCH: I'm guessing this is an issue because of the format the date is actually stored as, right? Cause we don't do any formatting when saving or loading
            Assert.That(sut.GetValue<DateTime>("dateTime").ToString("G"), Is.EqualTo(content.GetValue<DateTime>("dateTime").ToString("G")));
            Assert.That(sut.GetValue<string>("colorPicker"), Is.EqualTo("black"));
	        Assert.That(sut.GetValue<string>("folderBrowser"), Is.Null);
            Assert.That(sut.GetValue<string>("ddlMultiple"), Is.EqualTo("1234,1235"));
            Assert.That(sut.GetValue<string>("rbList"), Is.EqualTo("random"));
            Assert.That(sut.GetValue<DateTime>("date").ToString("G"), Is.EqualTo(content.GetValue<DateTime>("date").ToString("G")));
            Assert.That(sut.GetValue<string>("ddl"), Is.EqualTo("1234"));
            Assert.That(sut.GetValue<string>("chklist"), Is.EqualTo("randomc"));
            Assert.That(sut.GetValue<int>("contentPicker"), Is.EqualTo(1090));
            Assert.That(sut.GetValue<int>("mediaPicker"), Is.EqualTo(1091));
            Assert.That(sut.GetValue<int>("memberPicker"), Is.EqualTo(1092));
            Assert.That(sut.GetValue<string>("relatedLinks"), Is.EqualTo("<links><link title=\"google\" link=\"http://google.com\" type=\"external\" newwindow=\"0\" /></links>"));
            Assert.That(sut.GetValue<string>("tags"), Is.EqualTo("this,is,tags"));
	    }

	    [Test]
	    public void Can_Delete_Previous_Versions_Not_Latest()
	    {
            // Arrange
            var contentService = ServiceContext.ContentService;
            var content = contentService.GetById(NodeDto.NodeIdSeed + 4);
	        var version = content.Version;

	        // Act
            contentService.DeleteVersion(NodeDto.NodeIdSeed + 4, version, true, 0);
            var sut = contentService.GetById(NodeDto.NodeIdSeed + 4);

            // Assert
            Assert.That(sut.Version, Is.EqualTo(version));
	    }

        [Test]
        public void Ensure_Content_Xml_Created()
        {
            var contentService = ServiceContext.ContentService;
            var content = contentService.CreateContent("Home US", -1, "umbTextpage", 0);
            content.SetValue("author", "Barack Obama");

            contentService.Save(content);

            var provider = new PetaPocoUnitOfWorkProvider(Logger);

            using (var uow = provider.GetUnitOfWork())
            {
                Assert.IsFalse(uow.Database.Exists<ContentXmlDto>(content.Id));
            }

            contentService.Publish(content);
            
            using (var uow = provider.GetUnitOfWork())
            {
                Assert.IsTrue(uow.Database.Exists<ContentXmlDto>(content.Id));
            }
        }

        [Test]
        public void Ensure_Preview_Xml_Created()
        {
            var contentService = ServiceContext.ContentService;
            var content = contentService.CreateContent("Home US", -1, "umbTextpage", 0);
            content.SetValue("author", "Barack Obama");

            contentService.Save(content);

            var provider = new PetaPocoUnitOfWorkProvider(Logger);
            
            using (var uow = provider.GetUnitOfWork())
            {
                Assert.IsTrue(uow.Database.SingleOrDefault<PreviewXmlDto>("WHERE nodeId=@nodeId", new{nodeId = content.Id}) != null);
            }
        }

        [Test]
        public void Created_HasPublishedVersion_Not()
        {
            var contentService = ServiceContext.ContentService;
            var content = contentService.CreateContent("Home US", -1, "umbTextpage", 0);
            content.SetValue("author", "Barack Obama");

            contentService.Save(content);

            Assert.IsTrue(content.HasIdentity);
            Assert.IsFalse(content.HasPublishedVersion);

            content = contentService.GetById(content.Id);
            Assert.IsFalse(content.HasPublishedVersion);
        }

        [Test]
        public void Published_HasPublishedVersion_Self()
        {
            var contentService = ServiceContext.ContentService;
            var content = contentService.CreateContent("Home US", -1, "umbTextpage", 0);
            content.SetValue("author", "Barack Obama");

            contentService.SaveAndPublishWithStatus(content);

            Assert.IsTrue(content.HasIdentity);
            Assert.IsTrue(content.HasPublishedVersion);
            Assert.AreEqual(content.PublishedVersionGuid, content.Version);

            content = contentService.GetById(content.Id);
            Assert.IsTrue(content.HasPublishedVersion);
            Assert.AreEqual(content.PublishedVersionGuid, content.Version);
        }

        [Test]
        public void PublishedWithChanges_HasPublishedVersion_Other()
        {
            var contentService = ServiceContext.ContentService;
            var content = contentService.CreateContent("Home US", -1, "umbTextpage", 0);
            content.SetValue("author", "Barack Obama");

            contentService.SaveAndPublishWithStatus(content);
            content.SetValue("author", "James Dean");
            contentService.Save(content);

            Assert.IsTrue(content.HasIdentity);
            Assert.IsTrue(content.HasPublishedVersion);
            Assert.AreNotEqual(content.PublishedVersionGuid, content.Version);

            content = contentService.GetById(content.Id);
            Assert.IsFalse(content.Published);
            Assert.IsTrue(content.HasPublishedVersion);
            Assert.AreNotEqual(content.PublishedVersionGuid, content.Version);

            var published = contentService.GetPublishedVersion(content);
            Assert.IsTrue(published.Published);
            Assert.IsTrue(published.HasPublishedVersion);
            Assert.AreEqual(published.PublishedVersionGuid, published.Version);
        }

        [Test]
        public void Unpublished_HasPublishedVersion_Not()
        {
            var contentService = ServiceContext.ContentService;
            var content = contentService.CreateContent("Home US", -1, "umbTextpage", 0);
            content.SetValue("author", "Barack Obama");

            contentService.SaveAndPublishWithStatus(content);
            contentService.UnPublish(content);

            Assert.IsTrue(content.HasIdentity);
            Assert.IsFalse(content.HasPublishedVersion);

            content = contentService.GetById(content.Id);
            Assert.IsFalse(content.HasPublishedVersion);
        }

        [Test]
        public void HasPublishedVersion_Method()
        {
            var contentService = ServiceContext.ContentService;
            var content = contentService.CreateContent("Home US", -1, "umbTextpage", 0);
            content.SetValue("author", "Barack Obama");

            contentService.Save(content);
            Assert.IsTrue(content.HasIdentity);
            Assert.IsFalse(content.HasPublishedVersion);
            Assert.IsFalse(content.HasPublishedVersion());

            contentService.SaveAndPublishWithStatus(content);
            Assert.IsTrue(content.HasPublishedVersion);
            Assert.IsTrue(content.HasPublishedVersion());
        }

        private IEnumerable<IContent> CreateContentHierarchy()
        {
            var contentType = ServiceContext.ContentTypeService.Get("umbTextpage");
            var root = ServiceContext.ContentService.GetById(NodeDto.NodeIdSeed + 1);

			var list = new List<IContent>();

            for (int i = 0; i < 10; i++)
            {
				var content = MockedContent.CreateSimpleContent(contentType, "Hierarchy Simple Text Page " + i, root);

                list.Add(content);
                list.AddRange(CreateChildrenOf(contentType, content, 4));

                Console.WriteLine("Created: 'Hierarchy Simple Text Page {0}'", i);
            }

            return list;
        }

		private IEnumerable<IContent> CreateChildrenOf(IContentType contentType, IContent content, int depth)
        {
            var list = new List<IContent>();
            for (int i = 0; i < depth; i++)
            {
				var c = MockedContent.CreateSimpleContent(contentType, "Hierarchy Simple Text Subpage " + i, content);
                list.Add(c);

                Console.WriteLine("Created: 'Hierarchy Simple Text Subpage {0}' - Depth: {1}", i, depth);
            }
            return list;
        }

        private ContentRepository CreateRepository(IDatabaseUnitOfWork unitOfWork, out ContentTypeRepository contentTypeRepository)
        {
            var templateRepository = new TemplateRepository(unitOfWork, CacheHelper.CreateDisabledCacheHelper(), Mock.Of<ILogger>(), SqlSyntax, Mock.Of<IFileSystem>(), Mock.Of<IFileSystem>(), Mock.Of<ITemplatesSection>());
            var tagRepository = new TagRepository(unitOfWork, CacheHelper.CreateDisabledCacheHelper(), Mock.Of<ILogger>(), SqlSyntax);
            contentTypeRepository = new ContentTypeRepository(unitOfWork, CacheHelper.CreateDisabledCacheHelper(), Mock.Of<ILogger>(), SqlSyntax, templateRepository);
            var repository = new ContentRepository(unitOfWork, CacheHelper.CreateDisabledCacheHelper(), Mock.Of<ILogger>(), SqlSyntax, contentTypeRepository, templateRepository, tagRepository, Mock.Of<IContentSection>());
            return repository;
        }
    }
}