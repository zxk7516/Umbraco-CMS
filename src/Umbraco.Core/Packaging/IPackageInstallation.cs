using System;
using System.Xml.Linq;
using Umbraco.Core.Models.Packaging;
using Umbraco.Core.Persistence.Repositories;

namespace Umbraco.Core.Packaging
{
    internal interface IPackageInstallation
    {
        /// <summary>
        /// This will install all of the files for the package - this needs to be done before the package data is installed
        /// </summary>
        /// <param name="packageFile"></param>
        /// <returns></returns>
        InstalledPackage InstallPackageFiles(string packageFile);

        /// <summary>
        /// This will install the business logic data for the package - this can only be called after the package files are installed
        /// </summary>
        /// <param name="packageFile"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        InstallationSummary InstallPackageData(string packageFile, int userId);

        PackageMetaData GetMetaData(string packageFilePath);
        PreInstallWarnings GetPreInstallWarnings(string packageFilePath);
        XElement GetConfigXmlElement(string packageFilePath);
    }
}