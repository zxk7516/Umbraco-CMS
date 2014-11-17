using System.Collections.Generic;
using Umbraco.Core.Models.Packaging;

namespace Umbraco.Core.Events
{
    internal class ImportPackageEventArgs<TEntity> : CancellableObjectEventArgs<IEnumerable<TEntity>>
    {
        private readonly PackageMetaData _packageMetaData;

        public ImportPackageEventArgs(TEntity eventObject, bool canCancel)
            : base(new[] { eventObject }, canCancel)
        {
        }

        public ImportPackageEventArgs(TEntity eventObject, PackageMetaData packageMetaData)
            : base(new[] { eventObject })
        {
            _packageMetaData = packageMetaData;
        }

        public PackageMetaData PackageMetaData
        {
            get { return _packageMetaData; }
        }
    }
}