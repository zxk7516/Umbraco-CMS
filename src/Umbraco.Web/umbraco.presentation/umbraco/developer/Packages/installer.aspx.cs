using System;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Web;
using System.Web.SessionState;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Web.UI.HtmlControls;
using System.Xml;
using System.Xml.XPath;
using Umbraco.Core.IO;
using Umbraco.Core.Models.Packaging;
using Umbraco.Core.Services;
using Umbraco.Web;
using umbraco.BasePages;
using umbraco.BusinessLogic;
using umbraco.cms.presentation.Trees;
using Umbraco.Core;
using BizLogicAction = umbraco.BusinessLogic.Actions.Action;

namespace umbraco.presentation.developer.packages
{
    /// <summary>
    /// Summary description for packager.
    /// </summary>
    public partial class Installer : UmbracoEnsuredPage
    {
        public Installer()
        {
            CurrentApp = DefaultApps.developer.ToString();
        }

        private Control _configControl;
        private cms.businesslogic.packager.repositories.Repository _repo;

        internal PackagingService PackagingService
        {
            get { return (PackagingService)Services.PackagingService; }
        }

        private string _tempFileName = "";

        protected string RefreshQueryString { get; set; }

        protected void Page_Load(object sender, EventArgs e)
        {
            var ex = new Exception();
            if (!cms.businesslogic.packager.Settings.HasFileAccess(ref ex))
            {
                fb.Style.Add("margin-top", "7px");
                fb.type = uicontrols.Feedback.feedbacktype.error;
                fb.Text = "<strong>" + ui.Text("errors", "filePermissionsError") + ":</strong><br/>" + ex.Message;
            }

            if (!IsPostBack)
            {
                ButtonInstall.Attributes.Add("onClick", "jQuery(this).hide(); jQuery('#installingMessage').show();; return true;");
                ButtonLoadPackage.Attributes.Add("onClick", "jQuery(this).hide(); jQuery('#loadingbar').show();; return true;");
            }

            //if we are actually in the middle of installing something... meaning we keep redirecting back to this page with 
            // custom query strings 
            // TODO: SD: This process needs to be fixed/changed/etc... to use the InstallPackageController
            //	http://issues.umbraco.org/issue/U4-1047
            if (!string.IsNullOrEmpty(Request.GetItemAsString("installing")))
            {
                HideAllPanes();
                pane_installing.Visible = true;
                ProcessInstall(Request.GetItemAsString("installing")); //process the current step

            }
            else if (tempFile.Value.IsNullOrWhiteSpace() //if we haven't downloaded the .umb temp file yet
                && (!Request.GetItemAsString("guid").IsNullOrWhiteSpace() && !Request.GetItemAsString("repoGuid").IsNullOrWhiteSpace()))
            {
                //we'll fetch the local information we have about our repo, to find out what webservice to query.
                _repo = cms.businesslogic.packager.repositories.Repository.getByGuid(Request.GetItemAsString("repoGuid"));

                if (_repo != null && _repo.HasConnection())
                {
                    //from the webservice we'll fetch some info about the package.
                    var pack = _repo.Webservice.PackageByGuid(Request.GetItemAsString("guid"));

                    //if the package is protected we will ask for the users credentials. (this happens every time they try to fetch anything)
                    if (!pack.Protected)
                    {
                        //if it isn't then go straigt to the accept licens screen
                        //var meta = PackagingService.GetPackageMetaData(_repo.fetch(Request.GetItemAsString("guid")));
                        //tempFile.Value = meta.
                        var downloadedFile = _repo.fetch(Request.GetItemAsString("guid"));
                        var packageFile = GetPackageFilePath(downloadedFile);
                        //tempFile.Value = PackagingService.Import(_repo.fetch(Request.GetItemAsString("guid")));
                        tempFile.Value = packageFile;
                        var warnings = PackagingService.GetPackageWarnings(packageFile);
                        var metaData = PackagingService.GetPackageMetaData(packageFile);
                        UpdateSettings(warnings, metaData);

                    }
                    else if (!IsPostBack)
                    {

                        //Authenticate against the repo
                        HideAllPanes();
                        pane_authenticate.Visible = true;

                    }
                }
                else
                {
                    fb.Style.Add("margin-top", "7px");
                    fb.type = uicontrols.Feedback.feedbacktype.error;
                    fb.Text = "<strong>No connection to repository.</strong> Runway could not be installed as there was no connection to: '" + _repo.RepositoryUrl + "'";
                    pane_upload.Visible = false;
                }
            }
        }

        private string GetPackageFilePath(string inputFile)
        {
            if (File.Exists(IOHelper.MapPath(SystemDirectories.Data + Path.DirectorySeparatorChar + inputFile)))
            {
                var fi = new FileInfo(IOHelper.MapPath(SystemDirectories.Data + Path.DirectorySeparatorChar + inputFile));
                // Check if the file is a valid package
                if (fi.Extension.ToLower() == ".umb")
                {
                    return fi.FullName;
                }
                throw new Exception("Error - file isn't a package (doesn't have a .umb extension). Check if the file automatically got named '.zip' upon download.");
            }
            throw new Exception("Error - file not found. Could find file named '" + IOHelper.MapPath(SystemDirectories.Data + Path.DirectorySeparatorChar + inputFile) + "'");
        }


        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);
            acceptCheckbox.Attributes.Add("onmouseup", "document.getElementById('" + ButtonInstall.ClientID + "').disabled = false;");
        }

        protected void uploadFile(object sender, EventArgs e)
        {
            try
            {
                _tempFileName = Guid.NewGuid() + ".umb";
                string fileName = SystemDirectories.Data + Path.DirectorySeparatorChar + _tempFileName;
                file1.PostedFile.SaveAs(IOHelper.MapPath(fileName));
                var packageFile = GetPackageFilePath(_tempFileName);
                var warnings = PackagingService.GetPackageWarnings(packageFile);
                var metaData = PackagingService.GetPackageMetaData(packageFile);
                tempFile.Value = packageFile;
                //tempFile.Value = PackagingService.Import(_tempFileName);
                UpdateSettings(warnings, metaData);
            }
            catch (Exception ex)
            {
                fb.type = global::umbraco.uicontrols.Feedback.feedbacktype.error;
                fb.Text = "<strong>Could not upload file</strong><br/>" + ex.ToString();
            }
        }

        //this fetches the protected package from the repo.
        protected void fetchProtectedPackage(object sender, EventArgs e)
        {
            //we auth against the webservice. This key will be used to fetch the protected package.
            string memberGuid = _repo.Webservice.authenticate(tb_email.Text, library.md5(tb_password.Text));

            //if we auth correctly and get a valid key back, we will fetch the file from the repo webservice.
            if (string.IsNullOrEmpty(memberGuid) == false)
            {
                var downloadedFile = _repo.fetch(helper.Request("guid"), memberGuid);
                var packageFile = GetPackageFilePath(downloadedFile);
                var warnings = PackagingService.GetPackageWarnings(packageFile);
                var metaData = PackagingService.GetPackageMetaData(packageFile);
                tempFile.Value = packageFile;
                //tempFile.Value = PackagingService.Import(_repo.fetch(helper.Request("guid"), memberGuid));
                UpdateSettings(warnings, metaData);
            }
        }

        //this loads the accept license screen
        private void UpdateSettings(PreInstallWarnings warnings, PackageMetaData metaData)
        {
            HideAllPanes();

            pane_acceptLicense.Visible = true;
            pane_acceptLicenseInner.Text = "Installing the package: " + metaData.Name;
            Panel1.Text = "Installing the package: " + metaData.Name;


            if (warnings.ServerSideScripts != null && warnings.ServerSideScripts.Any())
            {
                pp_unsecureFiles.Visible = true;
                foreach (var str in warnings.ServerSideScripts)
                {
                    lt_files.Text += "<li>" + str.Value + "</li>";
                }
            }

            if (warnings.AssembliesWithLegacyPropertyEditors != null && warnings.AssembliesWithLegacyPropertyEditors.Any())
            {
                LegacyPropertyEditorPanel.Visible = true;
            }

            if (warnings.AssemblyErrors != null && warnings.AssemblyErrors.Any())
            {
                BinaryFileErrorsPanel.Visible = true;
                foreach (var str in warnings.AssemblyErrors)
                {
                    BinaryFileErrorReport.Text += "<li>" + str + "</li>";
                }
            }

            if (warnings.ConflictingMacroAliases != null && warnings.ConflictingMacroAliases.Any())
            {
                pp_macroConflicts.Visible = true;
                foreach (var item in warnings.ConflictingMacroAliases)
                {
                    ltrMacroAlias.Text += "<li>" + item.Key + " (Alias: " + item.Alias + ")</li>";
                }
            }

            if (warnings.ConflictingTemplateAliases != null && warnings.ConflictingTemplateAliases.Any())
            {
                pp_templateConflicts.Visible = true;
                foreach (var item in warnings.ConflictingTemplateAliases)
                {
                    ltrTemplateAlias.Text += "<li>" + item.Key + " (Alias: " + item.Alias + ")</li>";
                }
            }

            if (warnings.ConflictingStylesheetNames != null && warnings.ConflictingStylesheetNames.Any())
            {
                pp_stylesheetConflicts.Visible = true;
                foreach (var item in warnings.ConflictingStylesheetNames)
                {
                    ltrStylesheetNames.Text += "<li>" + item.Key + " (Alias: " + item.Alias + ")</li>";
                }
            }

            LabelName.Text = metaData.Name + " Version: " + metaData.Version;
            LabelMore.Text = "<a href=\"" + metaData.Url + "\" target=\"_blank\">" + metaData.Url + "</a>";
            LabelAuthor.Text = "<a href=\"" + metaData.AuthorUrl + "\" target=\"_blank\">" + metaData.AuthorName + "</a>";
            LabelLicense.Text = "<a href=\"" + metaData.LicenseUrl + "\" target=\"_blank\">" + metaData.License + "</a>";

            if (metaData.Readme != "")
                readme.Text = "<div style=\"border: 1px solid #999; padding: 5px; overflow: auto; width: 370px; height: 160px;\">" + library.ReplaceLineBreaks(library.StripHtml(metaData.Readme)) + "</div>";
            else
                readme.Text = "<span style=\"color: #999\">No information</span><br/>";
        }


        private void ProcessInstall(string currentStep)
        {
            var packageFile = Request.GetItemAsString("pck");
            var packageId = 0;
            int.TryParse(Request.GetItemAsString("pId"), out packageId);

            switch (currentStep)
            {
                case "businesslogic":

                    var result = PackagingService.InstallPackageData(packageFile, CurrentUser.Id, raiseEvents: true);

                    ////first load in the config from the temporary directory
                    ////this will ensure that the installer have access to all the new files and the package manifest
                    //PackagingService.LoadConfig(dir);
                    //PackagingService.InstallBusinessLogic(packageId, dir);


                    //making sure that publishing actions performed from the cms layer gets pushed to the presentation
                    library.RefreshContent();

                    if (string.IsNullOrEmpty(result.InstalledPackage.MetaData.Control) == false)
                    {
                        Response.Redirect("installer.aspx?installing=refresh&pid=" + result.InstalledPackage.Id + "&pck=" + packageFile + "&customControl=" + Server.UrlEncode(result.InstalledPackage.MetaData.Control) + "&customUrl=" + Server.UrlEncode(result.InstalledPackage.MetaData.Url));
                    }
                    else
                    {
                        Response.Redirect("installer.aspx?installing=refresh&pid=" + result.InstalledPackage.Id + "&pck=" + packageFile + "&customUrl=" + Server.UrlEncode(result.InstalledPackage.MetaData.Url));
                    }
                    break;
                case "customInstaller":
                    var customControl = Request.GetItemAsString("customControl");

                    if (customControl.IsNullOrWhiteSpace() == false)
                    {
                        HideAllPanes();

                        _configControl = LoadControl(SystemDirectories.Root + customControl);
                        _configControl.ID = "packagerConfigControl";

                        pane_optional.Controls.Add(_configControl);
                        pane_optional.Visible = true;

                        if (!IsPostBack)
                        {
                            //We still need to clean everything up which is normally done in the Finished Action
                            PerformPostInstallCleanup(packageFile);
                        }

                    }
                    else
                    {
                        //if the custom installer control is empty here (though it should never be because we've already checked for it previously)
                        //then we should run the normal FinishedAction
                        PerformFinishedAction(packageId, packageFile, Request.GetItemAsString("customUrl"));
                    }
                    break;
                case "refresh":
                    PerformRefreshAction(packageId, packageFile, Request.GetItemAsString("customUrl"), Request.GetItemAsString("customControl"));
                    break;
                case "finished":
                    PerformFinishedAction(packageId, packageFile, Request.GetItemAsString("customUrl"));
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Perform the 'Finished' action of the installer
        /// </summary>
        /// <param name="packageId"></param>
        /// <param name="packageFile"></param>
        /// <param name="url"></param>
        private void PerformFinishedAction(int packageId, string packageFile, string url)
        {
            HideAllPanes();
            //string url = PackagingService.Url;
            string packageViewUrl = "installedPackage.aspx?id=" + packageId.ToString(CultureInfo.InvariantCulture);

            bt_viewInstalledPackage.OnClientClick = "document.location = '" + packageViewUrl + "'; return false;";

            if (!string.IsNullOrEmpty(url))
                lit_authorUrl.Text = " <em>" + ui.Text("or") + "</em> <a href='" + url + "' target=\"_blank\">" + ui.Text("viewPackageWebsite") + "</a>";


            pane_success.Visible = true;

            PerformPostInstallCleanup(packageFile);
        }

        /// <summary>
        /// Perform the 'Refresh' action of the installer
        /// </summary>
        /// <param name="packageId"></param>
        /// <param name="packageFile"></param>
        /// <param name="url"></param>
        /// <param name="customControl"></param>
        private void PerformRefreshAction(int packageId, string packageFile, string url, string customControl)
        {
            HideAllPanes();

            //create the URL to refresh to
            // /umbraco/developer/packages/installer.aspx?installing=finished
            //          &dir=X:\Projects\Umbraco\Umbraco_7.0\src\Umbraco.Web.UI\App_Data\aef8c41f-63a0-494b-a1e2-10d761647033
            //          &pId=3
            //          &customUrl=http:%2f%2four.umbraco.org%2fprojects%2fwebsite-utilities%2fmerchello

            if (customControl.IsNullOrWhiteSpace())
            {
                RefreshQueryString = Server.UrlEncode(string.Format(
                "installing=finished&pck={0}&customUrl={1}&pid={2}",
                packageFile, url, packageId));
            }
            else
            {
                RefreshQueryString = Server.UrlEncode(string.Format(
                "installing=customInstaller&pck={0}&customUrl={1}&customControl={2}&pid={3}",
                packageFile, url, customControl, packageId));
            }

            pane_refresh.Visible = true;

            PerformPostInstallCleanup(packageFile);
        }

        /// <summary>
        /// Runs Post refresh actions such reloading the correct tree nodes, etc...
        /// </summary>
        private void PerformPostRefreshAction()
        {
            BasePage.Current.ClientTools.ReloadActionNode(true, true);
        }

        /// <summary>
        /// Runs Post install actions such as clearning any necessary cache, reloading the correct tree nodes, etc...
        /// </summary>
        /// <param name="packageFile"></param>
        private void PerformPostInstallCleanup(string packageFile)
        {
            if (File.Exists(packageFile))
            {
                File.Delete(packageFile);
            }
            //PackagingService.InstallCleanUp(packageId, packageFile);

            // Update ClientDependency version
            var clientDependencyConfig = new Umbraco.Core.Configuration.ClientDependencyConfiguration();
            var clientDependencyUpdated = clientDependencyConfig.IncreaseVersionNumber();
            
            //clear the tree cache - we'll do this here even though the browser will reload, but just in case it doesn't can't hurt.
            ClientTools.ClearClientTreeCache().RefreshTree("packager");
            TreeDefinitionCollection.Instance.ReRegisterTrees();
            BizLogicAction.ReRegisterActionsAndHandlers();
        }

        //this accepts the package, creates the manifest and then installs the files.
        protected void startInstall(object sender, System.EventArgs e)
        {
            //we will now create the installer manifest, which means that umbraco can register everything that gets added to the system
            //this returns an id of the manifest.

            //PackagingService.LoadConfig(tempFile.Value);

            //int pId = PackagingService.CreateManifest(tempFile.Value, helper.Request("guid"), helper.Request("repoGuid"));

            //and then copy over the files. This will take some time if it contains .dlls that will reboot the system..
            var result = PackagingService.InstallPackageFiles(tempFile.Value, Guid.Parse(helper.Request("guid")), Guid.Parse(helper.Request("repoGuid")), CurrentUser.Id, raiseEvents:true);

            //TODO: This is a total hack, we need to refactor the installer to be just like the package installer during the 
            // install process and use AJAX to ensure that app pool restarts and restarts PROPERLY before installing the business
            // logic. Until then, we are going to put a thread sleep here for 2 seconds in hopes that we always fluke out and the app 
            // pool will be restarted after redirect.
            Thread.Sleep(2000);

            Response.Redirect("installer.aspx?installing=businesslogic&dir=" + tempFile.Value + "&pId=" + result.Id);
        }

        private void HideAllPanes()
        {
            pane_authenticate.Visible = false;
            pane_acceptLicense.Visible = false;
            pane_installing.Visible = false;
            pane_optional.Visible = false;
            pane_success.Visible = false;
            pane_refresh.Visible = false;
            pane_upload.Visible = false;
        }

        /// <summary>
        /// Panel1 control.
        /// </summary>
        /// <remarks>
        /// Auto-generated field.
        /// To modify move field declaration from designer file to code-behind file.
        /// </remarks>
        protected global::umbraco.uicontrols.UmbracoPanel Panel1;

        /// <summary>
        /// fb control.
        /// </summary>
        /// <remarks>
        /// Auto-generated field.
        /// To modify move field declaration from designer file to code-behind file.
        /// </remarks>
        protected global::umbraco.uicontrols.Feedback fb;

        /// <summary>
        /// pane_upload control.
        /// </summary>
        /// <remarks>
        /// Auto-generated field.
        /// To modify move field declaration from designer file to code-behind file.
        /// </remarks>
        protected global::umbraco.uicontrols.Pane pane_upload;

        /// <summary>
        /// PropertyPanel9 control.
        /// </summary>
        /// <remarks>
        /// Auto-generated field.
        /// To modify move field declaration from designer file to code-behind file.
        /// </remarks>
        protected global::umbraco.uicontrols.PropertyPanel PropertyPanel9;

        /// <summary>
        /// file1 control.
        /// </summary>
        /// <remarks>
        /// Auto-generated field.
        /// To modify move field declaration from designer file to code-behind file.
        /// </remarks>
        protected global::System.Web.UI.HtmlControls.HtmlInputFile file1;

        /// <summary>
        /// ButtonLoadPackage control.
        /// </summary>
        /// <remarks>
        /// Auto-generated field.
        /// To modify move field declaration from designer file to code-behind file.
        /// </remarks>
        protected global::System.Web.UI.WebControls.Button ButtonLoadPackage;

        /// <summary>
        /// progbar1 control.
        /// </summary>
        /// <remarks>
        /// Auto-generated field.
        /// To modify move field declaration from designer file to code-behind file.
        /// </remarks>
        protected global::umbraco.uicontrols.ProgressBar progbar1;

        /// <summary>
        /// pane_authenticate control.
        /// </summary>
        /// <remarks>
        /// Auto-generated field.
        /// To modify move field declaration from designer file to code-behind file.
        /// </remarks>
        protected global::umbraco.uicontrols.Pane pane_authenticate;

        /// <summary>
        /// tb_email control.
        /// </summary>
        /// <remarks>
        /// Auto-generated field.
        /// To modify move field declaration from designer file to code-behind file.
        /// </remarks>
        protected global::System.Web.UI.WebControls.TextBox tb_email;

        /// <summary>
        /// PropertyPanel1 control.
        /// </summary>
        /// <remarks>
        /// Auto-generated field.
        /// To modify move field declaration from designer file to code-behind file.
        /// </remarks>
        protected global::umbraco.uicontrols.PropertyPanel PropertyPanel1;

        /// <summary>
        /// tb_password control.
        /// </summary>
        /// <remarks>
        /// Auto-generated field.
        /// To modify move field declaration from designer file to code-behind file.
        /// </remarks>
        protected global::System.Web.UI.WebControls.TextBox tb_password;

        /// <summary>
        /// PropertyPanel2 control.
        /// </summary>
        /// <remarks>
        /// Auto-generated field.
        /// To modify move field declaration from designer file to code-behind file.
        /// </remarks>
        protected global::umbraco.uicontrols.PropertyPanel PropertyPanel2;

        /// <summary>
        /// Button1 control.
        /// </summary>
        /// <remarks>
        /// Auto-generated field.
        /// To modify move field declaration from designer file to code-behind file.
        /// </remarks>
        protected global::System.Web.UI.WebControls.Button Button1;

        /// <summary>
        /// pane_acceptLicense control.
        /// </summary>
        /// <remarks>
        /// Auto-generated field.
        /// To modify move field declaration from designer file to code-behind file.
        /// </remarks>
        protected global::System.Web.UI.WebControls.Panel pane_acceptLicense;

        /// <summary>
        /// pane_acceptLicenseInner control.
        /// </summary>
        /// <remarks>
        /// Auto-generated field.
        /// To modify move field declaration from designer file to code-behind file.
        /// </remarks>
        protected global::umbraco.uicontrols.Pane pane_acceptLicenseInner;

        /// <summary>
        /// PropertyPanel3 control.
        /// </summary>
        /// <remarks>
        /// Auto-generated field.
        /// To modify move field declaration from designer file to code-behind file.
        /// </remarks>
        protected global::umbraco.uicontrols.PropertyPanel PropertyPanel3;

        /// <summary>
        /// LabelName control.
        /// </summary>
        /// <remarks>
        /// Auto-generated field.
        /// To modify move field declaration from designer file to code-behind file.
        /// </remarks>
        protected global::System.Web.UI.WebControls.Label LabelName;

        /// <summary>
        /// PropertyPanel5 control.
        /// </summary>
        /// <remarks>
        /// Auto-generated field.
        /// To modify move field declaration from designer file to code-behind file.
        /// </remarks>
        protected global::umbraco.uicontrols.PropertyPanel PropertyPanel5;

        /// <summary>
        /// LabelAuthor control.
        /// </summary>
        /// <remarks>
        /// Auto-generated field.
        /// To modify move field declaration from designer file to code-behind file.
        /// </remarks>
        protected global::System.Web.UI.WebControls.Label LabelAuthor;

        /// <summary>
        /// PropertyPanel4 control.
        /// </summary>
        /// <remarks>
        /// Auto-generated field.
        /// To modify move field declaration from designer file to code-behind file.
        /// </remarks>
        protected global::umbraco.uicontrols.PropertyPanel PropertyPanel4;

        /// <summary>
        /// LabelMore control.
        /// </summary>
        /// <remarks>
        /// Auto-generated field.
        /// To modify move field declaration from designer file to code-behind file.
        /// </remarks>
        protected global::System.Web.UI.WebControls.Label LabelMore;

        /// <summary>
        /// PropertyPanel6 control.
        /// </summary>
        /// <remarks>
        /// Auto-generated field.
        /// To modify move field declaration from designer file to code-behind file.
        /// </remarks>
        protected global::umbraco.uicontrols.PropertyPanel PropertyPanel6;

        /// <summary>
        /// LabelLicense control.
        /// </summary>
        /// <remarks>
        /// Auto-generated field.
        /// To modify move field declaration from designer file to code-behind file.
        /// </remarks>
        protected global::System.Web.UI.WebControls.Label LabelLicense;

        /// <summary>
        /// PropertyPanel7 control.
        /// </summary>
        /// <remarks>
        /// Auto-generated field.
        /// To modify move field declaration from designer file to code-behind file.
        /// </remarks>
        protected global::umbraco.uicontrols.PropertyPanel PropertyPanel7;

        /// <summary>
        /// acceptCheckbox control.
        /// </summary>
        /// <remarks>
        /// Auto-generated field.
        /// To modify move field declaration from designer file to code-behind file.
        /// </remarks>
        protected global::System.Web.UI.WebControls.CheckBox acceptCheckbox;

        /// <summary>
        /// PropertyPanel8 control.
        /// </summary>
        /// <remarks>
        /// Auto-generated field.
        /// To modify move field declaration from designer file to code-behind file.
        /// </remarks>
        protected global::umbraco.uicontrols.PropertyPanel PropertyPanel8;

        /// <summary>
        /// readme control.
        /// </summary>
        /// <remarks>
        /// Auto-generated field.
        /// To modify move field declaration from designer file to code-behind file.
        /// </remarks>
        protected global::System.Web.UI.WebControls.Literal readme;

        /// <summary>
        /// pp_unsecureFiles control.
        /// </summary>
        /// <remarks>
        /// Auto-generated field.
        /// To modify move field declaration from designer file to code-behind file.
        /// </remarks>
        protected global::umbraco.uicontrols.PropertyPanel pp_unsecureFiles;

        /// <summary>
        /// lt_files control.
        /// </summary>
        /// <remarks>
        /// Auto-generated field.
        /// To modify move field declaration from designer file to code-behind file.
        /// </remarks>
        protected global::System.Web.UI.WebControls.Literal lt_files;

        /// <summary>
        /// pp_macroConflicts control.
        /// </summary>
        /// <remarks>
        /// Auto-generated field.
        /// To modify move field declaration from designer file to code-behind file.
        /// </remarks>
        protected global::umbraco.uicontrols.PropertyPanel pp_macroConflicts;

        /// <summary>
        /// ltrMacroAlias control.
        /// </summary>
        /// <remarks>
        /// Auto-generated field.
        /// To modify move field declaration from designer file to code-behind file.
        /// </remarks>
        protected global::System.Web.UI.WebControls.Literal ltrMacroAlias;

        /// <summary>
        /// pp_templateConflicts control.
        /// </summary>
        /// <remarks>
        /// Auto-generated field.
        /// To modify move field declaration from designer file to code-behind file.
        /// </remarks>
        protected global::umbraco.uicontrols.PropertyPanel pp_templateConflicts;

        protected global::umbraco.uicontrols.PropertyPanel BinaryFileErrorsPanel;
        protected global::umbraco.uicontrols.PropertyPanel LegacyPropertyEditorPanel;
        protected global::System.Web.UI.WebControls.Literal BinaryFileErrorReport;

        /// <summary>
        /// ltrTemplateAlias control.
        /// </summary>
        /// <remarks>
        /// Auto-generated field.
        /// To modify move field declaration from designer file to code-behind file.
        /// </remarks>
        protected global::System.Web.UI.WebControls.Literal ltrTemplateAlias;

        /// <summary>
        /// pp_stylesheetConflicts control.
        /// </summary>
        /// <remarks>
        /// Auto-generated field.
        /// To modify move field declaration from designer file to code-behind file.
        /// </remarks>
        protected global::umbraco.uicontrols.PropertyPanel pp_stylesheetConflicts;

        /// <summary>
        /// ltrStylesheetNames control.
        /// </summary>
        /// <remarks>
        /// Auto-generated field.
        /// To modify move field declaration from designer file to code-behind file.
        /// </remarks>
        protected global::System.Web.UI.WebControls.Literal ltrStylesheetNames;

        /// <summary>
        /// _progbar1 control.
        /// </summary>
        /// <remarks>
        /// Auto-generated field.
        /// To modify move field declaration from designer file to code-behind file.
        /// </remarks>
        protected global::umbraco.uicontrols.ProgressBar _progbar1;

        /// <summary>
        /// ButtonInstall control.
        /// </summary>
        /// <remarks>
        /// Auto-generated field.
        /// To modify move field declaration from designer file to code-behind file.
        /// </remarks>
        protected global::System.Web.UI.WebControls.Button ButtonInstall;

        /// <summary>
        /// pane_installing control.
        /// </summary>
        /// <remarks>
        /// Auto-generated field.
        /// To modify move field declaration from designer file to code-behind file.
        /// </remarks>
        protected global::umbraco.uicontrols.Pane pane_installing;

        /// <summary>
        /// progBar2 control.
        /// </summary>
        /// <remarks>
        /// Auto-generated field.
        /// To modify move field declaration from designer file to code-behind file.
        /// </remarks>
        protected global::umbraco.uicontrols.ProgressBar progBar2;

        /// <summary>
        /// lit_installStatus control.
        /// </summary>
        /// <remarks>
        /// Auto-generated field.
        /// To modify move field declaration from designer file to code-behind file.
        /// </remarks>
        protected global::System.Web.UI.WebControls.Literal lit_installStatus;

        /// <summary>
        /// pane_optional control.
        /// </summary>
        /// <remarks>
        /// Auto-generated field.
        /// To modify move field declaration from designer file to code-behind file.
        /// </remarks>
        protected global::umbraco.uicontrols.Pane pane_optional;

        /// <summary>
        /// pane_success control.
        /// </summary>
        /// <remarks>
        /// Auto-generated field.
        /// To modify move field declaration from designer file to code-behind file.
        /// </remarks>
        protected global::umbraco.uicontrols.Pane pane_success;

        protected global::umbraco.uicontrols.Pane pane_refresh;

        /// <summary>
        /// bt_viewInstalledPackage control.
        /// </summary>
        /// <remarks>
        /// Auto-generated field.
        /// To modify move field declaration from designer file to code-behind file.
        /// </remarks>
        protected global::System.Web.UI.WebControls.Button bt_viewInstalledPackage;

        /// <summary>
        /// lit_authorUrl control.
        /// </summary>
        /// <remarks>
        /// Auto-generated field.
        /// To modify move field declaration from designer file to code-behind file.
        /// </remarks>
        protected global::System.Web.UI.WebControls.Literal lit_authorUrl;

        /// <summary>
        /// tempFile control.
        /// </summary>
        /// <remarks>
        /// Auto-generated field.
        /// To modify move field declaration from designer file to code-behind file.
        /// </remarks>
        protected global::System.Web.UI.HtmlControls.HtmlInputHidden tempFile;

        /// <summary>
        /// processState control.
        /// </summary>
        /// <remarks>
        /// Auto-generated field.
        /// To modify move field declaration from designer file to code-behind file.
        /// </remarks>
        protected global::System.Web.UI.HtmlControls.HtmlInputHidden processState;
    }
}
