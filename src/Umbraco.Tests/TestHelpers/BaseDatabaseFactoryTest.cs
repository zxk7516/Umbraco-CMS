using System;
using System.Configuration;
using System.Web.Routing;
using System.Xml;
using NUnit.Framework;
using Umbraco.Core;
using Umbraco.Core.Configuration;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Core.Persistence;
using Umbraco.Core.Persistence.Mappers;
using Umbraco.Core.Persistence.SqlSyntax;
using Umbraco.Core.Persistence.UnitOfWork;
using Umbraco.Core.PropertyEditors;
using Umbraco.Core.Services;
using Umbraco.Web;
using Umbraco.Web.PublishedCache;
using Umbraco.Web.PublishedCache.XmlPublishedCache;
using Umbraco.Web.Security;
using umbraco.BusinessLogic;
using Umbraco.Core.Events;
using Umbraco.Core.Scoping;

namespace Umbraco.Tests.TestHelpers
{
    /// <summary>
    /// Provides a base class for Umbraco application tests that require a database.
    /// </summary>
    /// <remarks>Can provide a SqlCE database populated with the Umbraco schema. The database should be accessed
    /// through the <see cref="DefaultDatabaseFactory"/>.</remarks>
    [TestFixture, RequiresSTA]
    public abstract class BaseDatabaseFactoryTest : BaseUmbracoApplicationTest
    {
        // indicates whether a test has already run for this session
        private static volatile bool _hasRunInTestSession;

        // indicates whether a test has already run for this feature
        private bool _hasRunInFeature = true;

        // indicates whether the current test is the first test of the test session
        private bool _isFirstRunInTestSession;

        // indicates whether the current test is the first test of the fixture
        private bool _isFirstRunInFeature;

        // indicates whether a database with schema has been created for the fixture
        private bool _hasFeatureSchemaDatabase;

        private static readonly object Locker = new object();

        private ApplicationContext _appContext;
        private DefaultDatabaseFactory _dbFactory;
        private DatabaseBehavior _databaseBehavior;
        private static TestDatabase _localDbDatabase; // one for *all* tests!
        private static TestDatabase _sqlCeTestDatabase; // one for *all* tests!
        private TestDatabase _testDatabase;

        protected virtual SupportedTestDatabase SupportedTestDatabase
        {
            // unknown = use what's best
            get { return SupportedTestDatabase.Unknown; }
        }

        [SetUp]
        public override void Initialize()
        {
            InitializeFirstRunFlags();

            var path = TestHelper.CurrentAssemblyDirectory;
            AppDomain.CurrentDomain.SetData("DataDirectory", path);

            // we probably don't need this here, as it's done in base.Initialize() already,
            // but these test classes are all weird, not going to change it now - v8
            SafeCallContext.Clear();

            switch (SupportedTestDatabase)
            {
                case SupportedTestDatabase.LocalDb:
                    // Get will throw if LocalDb is not available
                    _testDatabase = _localDbDatabase ?? (_localDbDatabase = TestDatabase.Get(TestHelper.CurrentAssemblyDirectory, SupportedTestDatabase));
                    break;
                case SupportedTestDatabase.SqlCe:
                    // SqlCe is always available
                    _testDatabase = _sqlCeTestDatabase ?? (_sqlCeTestDatabase = TestDatabase.Get(TestHelper.CurrentAssemblyDirectory, SupportedTestDatabase));
                    break;
                case SupportedTestDatabase.Unknown:
                    if (_localDbDatabase == null)
                    {
                        // use what we can
                        if (TestDatabase.LocalDbIsAvailable)
                            _testDatabase = _localDbDatabase = TestDatabase.Get(TestHelper.CurrentAssemblyDirectory, SupportedTestDatabase);
                        else
                            _testDatabase = _sqlCeTestDatabase ?? (_sqlCeTestDatabase = TestDatabase.Get(TestHelper.CurrentAssemblyDirectory, SupportedTestDatabase));
                    }
                    else
                    {
                        // use LocalDb
                        _testDatabase = _localDbDatabase;
                    }
                    break;
                default:
                    throw new NotSupportedException(SupportedTestDatabase.ToString());
            }

            _dbFactory = new DefaultDatabaseFactory(
                GetDbConnectionString(),
                GetDbProviderName(),
                Logger);

            // ensure we start tests in a clean state ie without any scope in context
            // anything that used a true 'Scope' would have removed it, but there could
            // be a rogue 'NoScope' there - and we want to make sure it is gone
            var scopeProvider = new ScopeProvider(null);
            if (scopeProvider.AmbientScope != null)
                scopeProvider.AmbientScope.Dispose(); // removes scope from context

            base.Initialize();

            using (ProfilingLogger.TraceDuration<BaseDatabaseFactoryTest>("init"))
            {
                //TODO: Somehow make this faster - takes 5s +

                DatabaseContext.Initialize(_dbFactory.ProviderName, _dbFactory.ConnectionString);

                InitializeDatabase();

                //ensure the configuration matches the current version for tests
                SettingsForTests.ConfigurationStatus = UmbracoVersion.GetSemanticVersion().ToSemanticString();
            }
        }

        protected override ApplicationContext CreateApplicationContext()
        {
            var repositoryFactory = new RepositoryFactory(CacheHelper, Logger, SqlSyntax, SettingsForTests.GenerateMockSettings());

            var evtMsgs = new TransientMessagesFactory();
            var scopeProvider = new ScopeProvider(_dbFactory);
            _appContext = new ApplicationContext(
                //assign the db context
                new DatabaseContext(scopeProvider, Logger, SqlSyntax, GetDbProviderName()),
                //assign the service context
                new ServiceContext(repositoryFactory, new PetaPocoUnitOfWorkProvider(scopeProvider), CacheHelper, Logger, evtMsgs),
                CacheHelper,
                ProfilingLogger)
            {
                IsReady = true
            };

            return _appContext;
        }

        protected virtual ISqlSyntaxProvider SqlSyntax
        {
            get { return GetSyntaxProvider(); }
        }

        /// <summary>
        /// The database behavior to use for the test/fixture
        /// </summary>
        protected DatabaseBehavior DatabaseTestBehavior
        {
            get
            {
                if (_databaseBehavior != DatabaseBehavior.Unknown) return _databaseBehavior;
                var att = GetType().GetCustomAttribute<DatabaseTestBehaviorAttribute>(false);
                return _databaseBehavior = att != null ? att.Behavior : DatabaseBehavior.NoDatabasePerFixture;
            }
        }

        protected virtual ISqlSyntaxProvider GetSyntaxProvider()
        {
            return _testDatabase.SqlSyntaxProvider;
        }

        protected virtual string GetDbProviderName()
        {
            return _testDatabase.ProviderName;
        }

        /// <summary>
        /// Get the db conn string
        /// </summary>
        protected virtual string GetDbConnectionString()
        {
            return _testDatabase.ConnectionString;
        }

        protected virtual void InitializeDatabase()
        {
            if (_isFirstRunInTestSession)
                _testDatabase.Clear();

            if (DatabaseTestBehavior == DatabaseBehavior.NoDatabasePerFixture)
                return;

            var cstr = GetDbConnectionString();

            // set the legacy connection string just in case something is referencing it
            ConfigurationManager.AppSettings.Set(Constants.System.UmbracoConnectionName, cstr);

            switch (DatabaseTestBehavior)
            {
                case DatabaseBehavior.EmptyDbFilePerTest:
                    // we need to create a new, empty database for this test
                    AttachEmptyDatabase();
                    break;

                case DatabaseBehavior.NewDbFileAndSchemaPerFixture:
                    if (_hasFeatureSchemaDatabase) return;
                    // else we need to create a database for this feature
                    AttachSchemaDatabase();
                    _hasFeatureSchemaDatabase = true;
                    break;

                case DatabaseBehavior.NewDbFileAndSchemaPerTest:
                    // we need to create a new, schema database for this test
                    AttachSchemaDatabase();
                    break;
            }
        }

        protected void AttachEmptyDatabase()
        {
            if (_testDatabase.HasEmpty)
            {
                _testDatabase.AttachEmpty();
            }
            else
            {
                _testDatabase.Create();
                _testDatabase.CaptureEmpty();
            }
        }

        protected void AttachSchemaDatabase()
        {
            if (_testDatabase.HasSchema)
            {
                _testDatabase.AttachSchema();
            }
            else
            {
                AttachEmptyDatabase();
                var schemaHelper = new DatabaseSchemaHelper(DatabaseContext.Database, Logger, SqlSyntax);
                schemaHelper.CreateDatabaseSchema(false, ApplicationContext);
                _testDatabase.CaptureSchema();
            }
        }

        /// <summary>
        /// sets up resolvers before resolution is frozen
        /// </summary>
        protected override void FreezeResolution()
        {
            PropertyEditorResolver.Current = new PropertyEditorResolver(
                new ActivatorServiceProvider(), Logger,
                () => PluginManager.Current.ResolvePropertyEditors(),
                ApplicationContext.ApplicationCache.RuntimeCache);

            DataTypesResolver.Current = new DataTypesResolver(
                new ActivatorServiceProvider(), Logger,
                () => PluginManager.Current.ResolveDataTypes());

            MappingResolver.Current = new MappingResolver(
                new ActivatorServiceProvider(), Logger,
               () => PluginManager.Current.ResolveAssignedMapperTypes());

            if (PropertyValueConvertersResolver.HasCurrent == false)
                PropertyValueConvertersResolver.Current = new PropertyValueConvertersResolver(new ActivatorServiceProvider(), Logger);

            if (PublishedContentModelFactoryResolver.HasCurrent == false)
                PublishedContentModelFactoryResolver.Current = new PublishedContentModelFactoryResolver();

            base.FreezeResolution();
        }

        /// <summary>
        /// When all tests are completed
        /// </summary>
        [TestFixtureTearDown]
        public void FixtureTearDown()
        {
            _testDatabase.Drop();
        }

        [TearDown]
        public override void TearDown()
        {
            using (ProfilingLogger.TraceDuration<BaseDatabaseFactoryTest>("teardown"))
            {
                _isFirstRunInFeature = false; //ensure this is false before anything!

                // ensure we don't leak a connection
                if (ApplicationContext != null
                    && ApplicationContext.DatabaseContext != null
                    && ApplicationContext.DatabaseContext.ScopeProvider != null)
                {
                    ApplicationContext.DatabaseContext.ScopeProvider.Reset();
                }

                AppDomain.CurrentDomain.SetData("DataDirectory", null);

                SqlSyntaxContext.SqlSyntaxProvider = null;
            }

            base.TearDown();
        }

        private void InitializeFirstRunFlags()
        {
            _isFirstRunInTestSession = _isFirstRunInFeature = false;

            if (_hasRunInTestSession == false || _hasRunInFeature == false)
            {
                lock (Locker)
                {
                    if (_hasRunInTestSession == false)
                    {
                        _isFirstRunInTestSession = true;
                        _hasRunInTestSession = true;
                    }

                    if (_hasRunInFeature == false)
                    {
                        _isFirstRunInFeature = true;
                        _hasRunInFeature = true;
                    }
                }
            }
        }

        protected DatabaseContext DatabaseContext
        {
            get { return ApplicationContext.DatabaseContext; }
        }

        protected UmbracoContext GetUmbracoContext(string url, int templateId, RouteData routeData = null, bool setSingleton = false)
        {
            var cache = new PublishedContentCache();

            cache.GetXmlDelegate = (context, preview) =>
                {
                    var doc = new XmlDocument();
                    doc.LoadXml(GetXmlContent(templateId));
                    return doc;
                };

            PublishedContentCache.UnitTesting = true;

            var httpContext = GetHttpContextFactory(url, routeData).HttpContext;
            var ctx = new UmbracoContext(
                httpContext,
                ApplicationContext,
                new PublishedCaches(cache, new PublishedMediaCache(ApplicationContext)),
                new WebSecurity(httpContext, ApplicationContext));

            if (setSingleton)
            {
                UmbracoContext.Current = ctx;
            }

            return ctx;
        }

        protected FakeHttpContextFactory GetHttpContextFactory(string url, RouteData routeData = null)
        {
            var factory = routeData != null
                            ? new FakeHttpContextFactory(url, routeData)
                            : new FakeHttpContextFactory(url);


            //set the state helper
            StateHelper.HttpContext = factory.HttpContext;

            return factory;
        }

        protected virtual string GetXmlContent(int templateId)
        {
            return @"<?xml version=""1.0"" encoding=""utf-8""?>
<!DOCTYPE root[
<!ELEMENT Home ANY>
<!ATTLIST Home id ID #REQUIRED>
<!ELEMENT CustomDocument ANY>
<!ATTLIST CustomDocument id ID #REQUIRED>
]>
<root id=""-1"">
	<Home id=""1046"" parentID=""-1"" level=""1"" writerID=""0"" creatorID=""0"" nodeType=""1044"" template=""" + templateId + @""" sortOrder=""1"" createDate=""2012-06-12T14:13:17"" updateDate=""2012-07-20T18:50:43"" nodeName=""Home"" urlName=""home"" writerName=""admin"" creatorName=""admin"" path=""-1,1046"" isDoc="""">
		<content><![CDATA[]]></content>
		<umbracoUrlAlias><![CDATA[this/is/my/alias, anotheralias]]></umbracoUrlAlias>
		<umbracoNaviHide>1</umbracoNaviHide>
		<Home id=""1173"" parentID=""1046"" level=""2"" writerID=""0"" creatorID=""0"" nodeType=""1044"" template=""" + templateId + @""" sortOrder=""2"" createDate=""2012-07-20T18:06:45"" updateDate=""2012-07-20T19:07:31"" nodeName=""Sub1"" urlName=""sub1"" writerName=""admin"" creatorName=""admin"" path=""-1,1046,1173"" isDoc="""">
			<content><![CDATA[<div>This is some content</div>]]></content>
			<umbracoUrlAlias><![CDATA[page2/alias, 2ndpagealias]]></umbracoUrlAlias>
			<Home id=""1174"" parentID=""1173"" level=""3"" writerID=""0"" creatorID=""0"" nodeType=""1044"" template=""" + templateId + @""" sortOrder=""2"" createDate=""2012-07-20T18:07:54"" updateDate=""2012-07-20T19:10:27"" nodeName=""Sub2"" urlName=""sub2"" writerName=""admin"" creatorName=""admin"" path=""-1,1046,1173,1174"" isDoc="""">
				<content><![CDATA[]]></content>
				<umbracoUrlAlias><![CDATA[only/one/alias]]></umbracoUrlAlias>
				<creatorName><![CDATA[Custom data with same property name as the member name]]></creatorName>
			</Home>
			<Home id=""1176"" parentID=""1173"" level=""3"" writerID=""0"" creatorID=""0"" nodeType=""1044"" template=""" + templateId + @""" sortOrder=""3"" createDate=""2012-07-20T18:08:08"" updateDate=""2012-07-20T19:10:52"" nodeName=""Sub 3"" urlName=""sub-3"" writerName=""admin"" creatorName=""admin"" path=""-1,1046,1173,1176"" isDoc="""">
				<content><![CDATA[]]></content>
			</Home>
			<CustomDocument id=""1177"" parentID=""1173"" level=""3"" writerID=""0"" creatorID=""0"" nodeType=""1234"" template=""" + templateId + @""" sortOrder=""4"" createDate=""2012-07-16T15:26:59"" updateDate=""2012-07-18T14:23:35"" nodeName=""custom sub 1"" urlName=""custom-sub-1"" writerName=""admin"" creatorName=""admin"" path=""-1,1046,1173,1177"" isDoc="""" />
			<CustomDocument id=""1178"" parentID=""1173"" level=""3"" writerID=""0"" creatorID=""0"" nodeType=""1234"" template=""" + templateId + @""" sortOrder=""4"" createDate=""2012-07-16T15:26:59"" updateDate=""2012-07-16T14:23:35"" nodeName=""custom sub 2"" urlName=""custom-sub-2"" writerName=""admin"" creatorName=""admin"" path=""-1,1046,1173,1178"" isDoc="""" />
		</Home>
		<Home id=""1175"" parentID=""1046"" level=""2"" writerID=""0"" creatorID=""0"" nodeType=""1044"" template=""" + templateId + @""" sortOrder=""3"" createDate=""2012-07-20T18:08:01"" updateDate=""2012-07-20T18:49:32"" nodeName=""Sub 2"" urlName=""sub-2"" writerName=""admin"" creatorName=""admin"" path=""-1,1046,1175"" isDoc=""""><content><![CDATA[]]></content>
		</Home>
	</Home>
	<CustomDocument id=""1172"" parentID=""-1"" level=""1"" writerID=""0"" creatorID=""0"" nodeType=""1234"" template=""" + templateId + @""" sortOrder=""2"" createDate=""2012-07-16T15:26:59"" updateDate=""2012-07-18T14:23:35"" nodeName=""Test"" urlName=""test-page"" writerName=""admin"" creatorName=""admin"" path=""-1,1172"" isDoc="""" />
</root>";
        }
    }
}