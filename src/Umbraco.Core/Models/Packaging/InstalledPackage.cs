using System;
using System.Collections;
using System.Collections.Generic;
using Umbraco.Core.Models.EntityBase;

namespace Umbraco.Core.Models.Packaging
{
    /// <summary>
    /// Represents an installed package
    /// </summary>
    internal class InstalledPackage : Entity
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Object"/> class.
        /// </summary>
        public InstalledPackage()
        {
            MetaData = new PackageMetaData();
            Macros = new List<int>();
            Templates = new List<int>();
            Stylesheets = new List<int>();
            DocumentTypes = new List<int>();
            Languages = new List<int>();
            DictionaryItems = new List<int>();
            DataTypes = new List<int>();
            Files = new List<string>();
        }

        /// <summary>
        /// The unique package identifier 
        /// </summary>
        public Guid PackageIdentifier { get; set; }

        public bool EnableSkins { get; set; }
        public Guid SkinRepositoryId { get; set; }
        public Guid PackageRepositoryId { get; set; }

        public bool HasUpdate { get; set; }

        public PackageMetaData MetaData { get; set; }

        public string Actions { get; set; }

        public int? ContentNodeId { get; set; }
        public bool LoadChildContentNodes { get; set; }

        public IEnumerable<int> Macros { get; set; }
        public IEnumerable<int> Templates { get; set; }
        public IEnumerable<int> Stylesheets { get; set; }
        public IEnumerable<int> DocumentTypes { get; set; }
        public IEnumerable<int> Languages { get; set; }
        public IEnumerable<int> DictionaryItems { get; set; }
        public IEnumerable<int> DataTypes { get; set; }

        public IEnumerable<string> Files { get; set; }

        

    }
}