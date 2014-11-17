using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Umbraco.Core.Models.Packaging
{

    /// <summary>
    /// A summary of all package item installed
    /// </summary>
    [Serializable]
    [DataContract(IsReference = true)]
    internal class InstallationSummary 
    {
        public InstallationSummary(PackageMetaData metaData)
            :this()
        {
            InstalledPackage.MetaData = metaData;
        }

        public InstallationSummary()
        {
            Actions = new List<PackageAction>();
            ContentInstalled = new List<IContent>();
            ContentTypesInstalled = new List<IContentType>();
            DataTypesInstalled = new List<IDataTypeDefinition>();
            DictionaryItemsInstalled = new List<IDictionaryItem>();
            LanguagesInstalled = new List<ILanguage>();
            MacrosInstalled = new List<IMacro>();
            TemplatesInstalled = new List<ITemplate>();

            InstalledPackage = new InstalledPackage();
        }

        public InstalledPackage InstalledPackage { get; set; }

        public IEnumerable<IDataTypeDefinition> DataTypesInstalled { get; set; }
        public IEnumerable<ILanguage> LanguagesInstalled { get; set; }
        public IEnumerable<IDictionaryItem> DictionaryItemsInstalled { get; set; }
        public IEnumerable<IMacro> MacrosInstalled { get; set; }
        public IEnumerable<ITemplate> TemplatesInstalled { get; set; }
        public IEnumerable<IContentType> ContentTypesInstalled { get; set; }
        public IEnumerable<IFile> StylesheetsInstalled { get; set; }
        public IEnumerable<IContent> ContentInstalled { get; set; }
        public IEnumerable<PackageAction> Actions { get; set; }

    }
    
}