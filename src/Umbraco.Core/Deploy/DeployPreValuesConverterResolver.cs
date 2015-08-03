using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.ObjectResolution;

namespace Umbraco.Core.Deploy
{
    public sealed class DeployPreValuesConverterResolver : LazyManyObjectsResolverBase<DeployPreValuesConverterResolver, IDeployPreValuesConverter>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DeployPreValuesConverterResolver"/> class with 
        /// an initial list of converter types.
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="typeListProducerList">The list of converter types</param>
        internal DeployPreValuesConverterResolver(ILogger logger, Func<IEnumerable<Type>> typeListProducerList)
            : base(new AppCtxServiceProvider(), logger, typeListProducerList)
        { }

        /// <summary>
        /// Gets the converters.
        /// </summary>
        public IEnumerable<IDeployPreValuesConverter> Converters
        {
            get { return Values; }
        }

        /// <summary>
        /// Get a converter for a property type
        /// </summary>
        /// <param name="dataType"></param>
        /// <returns></returns>
        public IDeployPreValuesConverter GetConverter(IDataTypeDefinition dataType)
        {
            return Converters.FirstOrDefault(x => x.IsConverter(dataType));
        }

        //This is like a super crappy DI - in v8 we have real DI
        private class AppCtxServiceProvider : IServiceProvider
        {
            public object GetService(Type serviceType)
            {
                var normalArgs = new[] { typeof(ApplicationContext) };
                var found = serviceType.GetConstructor(normalArgs);
                if (found != null)
                    return found.Invoke(new object[]
                    {
                        ApplicationContext.Current
                    });
                //use normal ctor
                return Activator.CreateInstance(serviceType);
            }
        }
    }
}