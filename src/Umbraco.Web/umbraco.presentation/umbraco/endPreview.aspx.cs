using System;
using System.Web;
using System.Web.UI;
using umbraco.BusinessLogic;
using Umbraco.Web;
using Umbraco.Web.PublishedCache;

namespace umbraco.presentation
{
    public partial class endPreview : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            var previewToken = (new HttpRequestWrapper(Request)).GetPreviewCookieValue();
            var service = FacadeServiceResolver.Current.Service;
            service.ExitPreview(previewToken);

            StateHelper.Cookies.Preview.Clear();
            //global::umbraco.presentation.preview.PreviewContent.ClearPreviewCookie();

            if (!Uri.IsWellFormedUriString(Request.QueryString["redir"], UriKind.Relative))
            {
                Response.Redirect("/", true);
            }
            Uri url;
            if (!Uri.TryCreate(Request.QueryString["redir"], UriKind.Relative, out url))
            {
                Response.Redirect("/", true);
            }

            Response.Redirect(url.ToString(), true);
        }
    }
}