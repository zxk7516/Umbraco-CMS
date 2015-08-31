using Umbraco.Core;
using Umbraco.Core.Logging;
using Umbraco.Core.Sync;

namespace Umbraco.Web
{
    /// <summary>
    /// Used to boot up the server messenger.
    /// </summary>
    internal class BatchedDatabaseServerMessengerStartup : IApplicationEventHandler
    {
        public void OnApplicationInitialized(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
        { }

        public void OnApplicationStarting(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
        { }

        public void OnApplicationStarted(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
        {
            // always run - up to Startup() to figure out the state of the application

            var messenger = ServerMessengerResolver.HasCurrent
                ? ServerMessengerResolver.Current.Messenger as BatchedDatabaseServerMessenger
                : null;

            if (messenger != null)
                messenger.Startup();
        }
    }
}