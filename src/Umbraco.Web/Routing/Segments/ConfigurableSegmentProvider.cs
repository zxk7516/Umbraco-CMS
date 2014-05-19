using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Newtonsoft.Json;
using Umbraco.Core;

namespace Umbraco.Web.Routing.Segments
{
    /// <summary>
    /// Similar to a normal segment provider but this lets admins of Umbraco choose a custom key/value to store in the current request
    /// based on a value that the provider returns.
    /// </summary>
    /// <remarks>
    /// An example of such a provider would be a 'ReferalProvider' for which the provider itself will inspect the current request's referrer, the 
    /// provider will return this value. If this provider is active, we will run the boolean logic configured for the provider 
    /// which would normally be a regex statement, if it matches the returned value then we will apply the configured key/value as a segment 
    /// in the request.
    /// 
    /// TODO: We need to decide if configurable segment provider can advertise segments to be used in variations - but I don't think so since they
    /// can be added/removed by users
    /// </remarks>
    public abstract class ConfigurableSegmentProvider
    {
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

        /// <summary>
        /// Returns the current provider's value (i.e. if the provider was a referal provider, this would return the current referrer)
        /// </summary>
        public abstract string CurrentValue { get; }

        /// <summary>
        /// By default this uses a regex statement to match but inheritors could do anything they want (i.e. dynamic compilation)
        /// </summary>
        /// <param name="matchStatement"></param>
        /// <returns></returns>
        public virtual bool IsMatch(string matchStatement)
        {
            return Regex.IsMatch(CurrentValue, matchStatement);
        }

        public virtual SegmentCollection GetSegmentsForRequest()
        {
            var config = ReadConfiguration();
            var result = config
                .Where(match => IsMatch(match.MatchStatement))
                .Select(match => new Segment(match.Key, match.Value));
            return new SegmentCollection(result);
        }

        public IEnumerable<SegmentProviderMatch> ReadConfiguration()
        {
            using (new ReadLock(_lock))
            {
                var fileName = GetType().Namespace.EnsureEndsWith('.') + GetType().Name + ".config.json";
                if (File.Exists(fileName) == false) return Enumerable.Empty<SegmentProviderMatch>();
                var content = File.ReadAllText(fileName);
                return JsonConvert.DeserializeObject<IEnumerable<SegmentProviderMatch>>(content);
            }
        } 

        public void SaveConfiguration(IEnumerable<SegmentProviderMatch> config)
        {
            using (new WriteLock(_lock))
            {
                var json = JsonConvert.SerializeObject(config);
                var fileName = GetType().Namespace.EnsureEndsWith('.') + GetType().Name + ".config.json";
                File.WriteAllText("~/App_Data/Segments/" + fileName, json); 
            }            
        }
    }
}