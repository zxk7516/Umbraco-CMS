using Umbraco.Core;
using Umbraco.Core.Persistence.SqlSyntax;

namespace Umbraco.Tests.TestHelpers
{
    public interface IDbTestHelper
    {
        void CreateNewDb(ApplicationContext applicationContext, DatabaseBehavior testBehavior);
        void DeleteDatabase();
        void ClearDatabase(ApplicationContext applicationContext);
        void ConfigureForFirstRun(ApplicationContext applicationContext);
        string DbProviderName { get; }
        string DbConnectionString { get; }
        ISqlSyntaxProvider SqlSyntaxProvider { get; }
    }
}