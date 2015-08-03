using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Umbraco.Core.Logging;
using Umbraco.Core.ObjectResolution;

namespace Umbraco.Core.Deploy
{
    /// <summary>
    /// Resolves the IDeployPropertyConverter objects.
    /// </summary>
    public sealed class DeployPropertyConvertersResolver : LazyManyObjectsResolverBase<DeployPropertyConvertersResolver, IDeployPropertyConverter>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DeployPropertyConvertersResolver"/> class with 
        /// an initial list of converter types.
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="typeListProducerList">The list of converter types</param>
        internal DeployPropertyConvertersResolver(ILogger logger, Func<IEnumerable<Type>> typeListProducerList)
            : base(new AppCtxServiceProvider(), logger, typeListProducerList)
        { }
        
        /// <summary>
        /// Gets the converters.
        /// </summary>
        public IEnumerable<IDeployPropertyConverter> Converters
        {
            get { return Values; }
        }

        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();
        private Tuple<IDeployPropertyConverter, DefaultDeployPropertyConverterAttribute>[] _defaults = null;

        /// <summary>
        /// Caches and gets the default converters with their metadata
        /// </summary>
        internal Tuple<IDeployPropertyConverter, DefaultDeployPropertyConverterAttribute>[] DefaultConverters
        {
            get
            {
                using (var locker = new UpgradeableReadLock(_lock))
                {
                    if (_defaults == null)
                    {
                        locker.UpgradeToWriteLock();

                        var defaultConvertersWithAttributes = Converters
                            .Select(x => new
                            {
                                attribute = x.GetType().GetCustomAttribute<DefaultDeployPropertyConverterAttribute>(false),
                                converter = x
                            })
                            .Where(x => x.attribute != null)
                            .ToArray();

                        _defaults = defaultConvertersWithAttributes
                            .Select(
                                x => new Tuple<IDeployPropertyConverter, DefaultDeployPropertyConverterAttribute>(x.converter, x.attribute))
                            .ToArray();
                    }

                    return _defaults;
                }
            }
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