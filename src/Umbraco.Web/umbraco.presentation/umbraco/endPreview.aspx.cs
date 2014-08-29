using System;
using System.Web;
using umbraco.BusinessLogic;
using Umbraco.Web.PublishedCache;
using Umbraco.Web;

namespace umbraco.presentation
{
    public partial class endPreview : BasePages.UmbracoEnsuredPage
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            var factory = PublishedCachesServiceResolver.Current.Service;
            var previewToken = (new HttpRequestWrapper(Request)).GetPreviewCookieValue();
            factory.ExitPreview(previewToken);

            StateHelper.Cookies.Preview.Clear();
            //preview.PreviewContent.ClearPreviewCookie();

            Response.Redirect(helper.Request("redir"), true);
        }

        /// <summary>
        /// form1 control.
        /// </summary>
        /// <remarks>
        /// Auto-generated field.
        /// To modify move field declaration from designer file to code-behind file.
        /// </remarks>
        protected global::System.Web.UI.HtmlControls.HtmlForm form1;
    }
}
