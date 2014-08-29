using System;
using System.Collections;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Web;
using System.Web.Security;
using System.Web.UI;
using System.Web.UI.HtmlControls;
using System.Web.UI.WebControls;
using System.Web.UI.WebControls.WebParts;
using System.Xml.Linq;
using Umbraco.Web;
using umbraco.cms.businesslogic.web;
using umbraco.BusinessLogic;
using Umbraco.Web.PublishedCache;

namespace umbraco.presentation.dialogs
{
    public partial class Preview : BasePages.UmbracoEnsuredPage
    {
        public Preview()
        {
            CurrentApp = DefaultApps.content.ToString();
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            var factory = PublishedCachesFactoryResolver.Current.Factory;
            var contentId = Request.GetItemAs<int>("id");
            var user = Umbraco.Web.UmbracoContext.Current.Security.CurrentUser;
            var previewToken = factory.EnterPreview(user, contentId);
            StateHelper.Cookies.Preview.SetValue(previewToken);

            //var previewContent = new PreviewContent(user, Guid.NewGuid(), false);
            //previewContent.CreatePreviewSet(contentId, true); // preview branch below that content
            //previewContent.ActivatePreviewCookie();

            // wtf would we update some fields and then redirect?!
            //var content = ApplicationContext.Services.ContentService.GetById(contentId);
            //docLit.Text = content.Name;
            //changeSetUrl.Text = previewContent._previewSetPath;

            // use a numeric url because content may not be in cache and so .Url would fail
            Response.Redirect("../../" + contentId + ".aspx", true);
        }
    }
}
