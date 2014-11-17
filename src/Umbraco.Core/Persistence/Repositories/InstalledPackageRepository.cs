using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Umbraco.Core.IO;
using Umbraco.Core.Models.EntityBase;
using Umbraco.Core.Models.Packaging;
using Umbraco.Core.Persistence.UnitOfWork;

namespace Umbraco.Core.Persistence.Repositories
{
    internal class InstalledPackageRepository : DisposableObject, IInstalledPackageRepository, IUnitOfWorkRepository
    {
        private readonly IUnitOfWork _work;
        private readonly XDocument _xmlFile;

        public InstalledPackageRepository(IUnitOfWork uow)
        {
            if (uow == null) throw new ArgumentNullException("uow");
            _work = uow;
            var xmlFile = Path.Combine(SystemDirectories.Packages, "installed", "installedPackages.config");
            if (File.Exists(xmlFile)) throw new InvalidOperationException("The xml file " + xmlFile + " does not exist");
            _xmlFile = XDocument.Load(xmlFile);
        }

        public InstalledPackageRepository(IUnitOfWork uow, FileSystemInfo xmlFile)
        {
            if (uow == null) throw new ArgumentNullException("uow");
            if (xmlFile == null) throw new ArgumentNullException("xmlFile");
            _work = uow;
            if (xmlFile.Exists == false) throw new InvalidOperationException("The xml file " + xmlFile.FullName + " does not exist");
            _xmlFile = XDocument.Load(xmlFile.FullName);
        }

        /// <summary>
        /// Handles the disposal of resources. Derived from abstract class <see cref="DisposableObject"/> which handles common required locking logic.
        /// </summary>
        protected override void DisposeResources()
        {
        }

        public void PersistNewItem(IEntity entity)
        {
            var package = (InstalledPackage) entity;

        }

        public void PersistUpdatedItem(IEntity entity)
        {
            var package = (InstalledPackage)entity;
        }

        public void PersistDeletedItem(IEntity entity)
        {
            var package = (InstalledPackage)entity;
        }

        /// <summary>
        /// Adds or Updates an Entity
        /// </summary>
        /// <param name="entity"></param>
        public void AddOrUpdate(InstalledPackage entity)
        {
            if (_xmlFile.Document != null &&
                _xmlFile.Document.Elements("package").Any(x => ((string) x.Attribute("id")) == entity.Id.ToString(CultureInfo.InvariantCulture)))
            {
                //exists
                _work.RegisterChanged(entity, this);
            }
            else
            {
                //new
                _work.RegisterAdded(entity, this);
            }
        }

        /// <summary>
        /// Deletes an Entity
        /// </summary>
        /// <param name="entity"></param>
        public void Delete(InstalledPackage entity)
        {
            if (_work != null)
            {
                _work.RegisterRemoved(entity, this);
            }
        }

        /// <summary>
        /// Gets an Entity by Id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public InstalledPackage Get(int id)
        {
            if (_xmlFile.Document == null) return null;

            var found = _xmlFile.Document.Elements("package").First(x => ((string) x.Attribute("id")) == id.ToString(CultureInfo.InvariantCulture));

            if (found == null) return null;

            return FromXml(found);
        }

        /// <summary>
        /// Gets all entities of the spefified type
        /// </summary>
        /// <param name="ids"></param>
        /// <returns></returns>
        public IEnumerable<InstalledPackage> GetAll(params int[] ids)
        {
            ids = ids.Distinct().ToArray();

            return _xmlFile.Document == null
                ? Enumerable.Empty<InstalledPackage>()
                : ids.Any()
                    ? _xmlFile.Document.Elements("package").Where(x => ids.Contains((int) x.Attribute("id"))).Select(FromXml)
                    : _xmlFile.Document.Elements("package").Select(FromXml);
        }

        /// <summary>
        /// Boolean indicating whether an Entity with the specified Id exists
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public bool Exists(int id)
        {
            return Get(id) != null;
        }

        private InstalledPackage FromXml(XElement xml)
        {
            return new InstalledPackage()
            {
                Id = (int) xml.Attribute("id"),
                Actions = (string) xml.Element("actions"),
                DataTypes = CollectionFromElement<int>(xml.Element("datatypes")),
                DictionaryItems = CollectionFromElement<int>(xml.Element("dictionaryitems")),
                Languages = CollectionFromElement<int>(xml.Element("languages")),
                DocumentTypes = CollectionFromElement<int>(xml.Element("documenttypes")),
                Stylesheets = CollectionFromElement<int>(xml.Element("stylesheets")),
                Templates = CollectionFromElement<int>(xml.Element("templates")),
                Macros = CollectionFromElement<int>(xml.Element("macros")),
                PackageRepositoryId = (Guid) xml.Attribute("repositoryGuid"),
                MetaData = new PackageMetaData()
                {
                    Name = (string) xml.Attribute("name"),
                    Version = (string) xml.Attribute("version"),
                    Url = (string) xml.Attribute("url"),
                    Control = (string) xml.Element("loadcontrol"),
                    License = (string) xml.Element("license"),
                    LicenseUrl = xml.Element("license") == null ? null : (string) xml.Element("license").Attribute("url"),
                    Readme = (string) xml.Element("readme"),
                    AuthorName = (string) xml.Element("author"),
                    AuthorUrl = xml.Element("author") == null ? null : (string) xml.Element("author").Attribute("url")
                },
                SkinRepositoryId = (Guid) xml.Attribute("skinRepoGuid"),
                EnableSkins = (bool) xml.Attribute("enableSkins"),
                HasUpdate = (bool) xml.Attribute("hasUpdate")
            };
        }

        private IEnumerable<T> CollectionFromElement<T>(XElement xml)
        {
            if (xml == null)
            {
                return Enumerable.Empty<T>();
            }
            var val = xml.Value;
            if (val.IsNullOrWhiteSpace())
            {
                return Enumerable.Empty<T>();
            }
            var result = new List<T>();
            foreach (var v in val.Split(new []{','}, StringSplitOptions.RemoveEmptyEntries))
            {
                var attempt = v.TryConvertTo<T>();
                if (attempt)
                {
                    result.Add(attempt.Result);
                }
            }
            return result;
        } 
    }
}