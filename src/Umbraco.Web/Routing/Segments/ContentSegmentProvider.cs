using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Web;
using Newtonsoft.Json;
using Umbraco.Core;
using Umbraco.Core.IO;
using Umbraco.Web.Models.Segments;

namespace Umbraco.Web.Routing.Segments
{
    /// <summary>
    /// Returns the segment names to assign to the current request
    /// </summary>
    /// <remarks>
    /// The provider also exposes via attributes which static segments can be applied to content variations
    /// </remarks>
    public abstract class ContentSegmentProvider
    {
        protected ContentSegmentProvider()
        {
            //ensure attributes exist
            var type = GetType();
            var nameAtt = type.GetCustomAttribute<DisplayNameAttribute>(false);
            var descAtt = type.GetCustomAttribute<DescriptionAttribute>(false);
            if (nameAtt == null || descAtt == null)
            {
                throw new ApplicationException(
                    "The segment provider " + type + " must be attributed with two attributes: " + typeof(DisplayNameAttribute) + " and " + typeof(DescriptionAttribute));
            }
            Name = nameAtt.DisplayName;
            Description = descAtt.Description;
        }

        public string Name { get; private set; }
        public string Description { get; private set; }

        /// <summary>
        /// Returns the Content Variants that this provider supports
        /// </summary>
        public IEnumerable<ContentVariantAttribute> AssignableContentVariants
        {
            get { return GetType().GetCustomAttributes<ContentVariantAttribute>(false).ToArray(); }
        }

        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

        /// <summary>
        /// Returns the status of enabled/disabled variants for a given provider
        /// </summary>
        /// <returns></returns>
        public IDictionary<string, bool> ReadVariantConfiguration()
        {
            using (new ReadLock(_lock))
            {
                var fileName = IOHelper.MapPath("~/App_Data/Segments/" + GetType().Namespace.EnsureEndsWith('.') + GetType().Name + ".variants.json");
                if (File.Exists(fileName) == false) return new Dictionary<string, bool>();
                var contents = File.ReadAllText(fileName);
                var result = JsonConvert.DeserializeObject<IDictionary<string, bool>>(contents);
                return result;
            }
        }

        /// <summary>
        /// Writes the status of enabled/disabled variants for a given provider
        /// </summary>
        /// <param name="variantConfig"></param>
        public void WriteVariantConfiguration(IDictionary<string, bool> variantConfig)
        {
            using (new WriteLock(_lock))
            {
                var json = JsonConvert.SerializeObject(variantConfig);
                var fileName = GetType().Namespace.EnsureEndsWith('.') + GetType().Name + ".variants.json";
                Directory.CreateDirectory(IOHelper.MapPath("~/App_Data/Segments"));
                File.WriteAllText(IOHelper.MapPath("~/App_Data/Segments/" + fileName), json);
            }
        }

        /// <summary>
        /// Returns the segment names and values to assign to the current request
        /// </summary>
        /// <param name="originalRequestUrl"></param>
        /// <param name="cleanedRequestUrl"></param>
        /// <param name="httpRequest"></param>
        /// <returns></returns>
        public abstract SegmentCollection GetSegmentsForRequest(
            Uri originalRequestUrl,
            Uri cleanedRequestUrl,
            HttpRequestBase httpRequest);

    }
}
