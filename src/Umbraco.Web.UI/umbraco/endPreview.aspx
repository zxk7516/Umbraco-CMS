<%@ Page Language="C#" AutoEventWireup="true" Inherits="System.Web.UI.Page" %>
<%@ Import Namespace="umbraco.BusinessLogic" %>
<%@ Import Namespace="Umbraco.Web.PublishedCache" %>
<%@ Import Namespace="Umbraco.Web" %>

<script runat="server">

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        var factory = PublishedCachesServiceResolver.Current.Service;
        var previewToken = (new HttpRequestWrapper(Request)).GetPreviewCookieValue();
        factory.ExitPreview(previewToken);

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

</script>