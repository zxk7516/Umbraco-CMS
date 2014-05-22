using System.Collections.Generic;
using System.IO;
using System.Threading;
using Newtonsoft.Json;
using Umbraco.Core;
using Umbraco.Core.IO;

namespace Umbraco.Web.Routing.Segments
{

    public class ContentSegmentProvidersStatus
    {
        private readonly static ReaderWriterLockSlim Lock = new ReaderWriterLockSlim();
        
        /// <summary>
        /// Returns a dictionary containing the provider type name and whether it is enabled or not
        /// </summary>
        /// <returns></returns>
        public static IDictionary<string, bool> GetProviderStatus()
        {
            using (new ReadLock(Lock))
            {
                var file = IOHelper.MapPath("~/App_Data/Segments/providers.config.json");
                if (File.Exists(file) == false)
                {
                    //empty
                    return new Dictionary<string, bool>();
                }
                var contents = File.ReadAllText(file);
                var result = JsonConvert.DeserializeObject<IDictionary<string, bool>>(contents);
                return result;
            }
        }

        public static void SaveProvidersStatus(IDictionary<string, bool> providersStatus)
        {
            using (new WriteLock(Lock))
            {
                var file = IOHelper.MapPath("~/App_Data/Segments/providers.config.json");
                var result = JsonConvert.SerializeObject(providersStatus);
                Directory.CreateDirectory(IOHelper.MapPath("~/App_Data/Segments"));
                File.WriteAllText(file, result);
            }
        }
    }
}