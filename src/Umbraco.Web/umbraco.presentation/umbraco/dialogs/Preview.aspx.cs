using System;
using umbraco.BasePages;
using umbraco.BusinessLogic;
using Umbraco.Web;
using Umbraco.Web.PublishedCache;

namespace umbraco.presentation.dialogs
{
    public partial class Preview : UmbracoEnsuredPage
    {
        public Preview()
        {
            CurrentApp = DefaultApps.content.ToString();
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            var user = Umbraco.Web.UmbracoContext.Current.Security.CurrentUser;
            var contentId = Request.GetItemAs<int>("id");

            var service = FacadeServiceResolver.Current.Service;
            var previewToken = service.EnterPreview(user, contentId);

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
