using System;
using System.Collections.Generic;
using System.Web;

namespace umbraco.presentation
{
    public class UmbracoPage : System.Web.UI.Page
    {
        public int PageId { get; set; }

        protected override void OnPreInit(EventArgs e)
        {
            if (UmbracoContext.Current == null)
            {
                // Set umbraco context
                UmbracoContext.Current = new UmbracoContext(HttpContext.Current);
            }

            HttpContext.Current.Items["pageID"] = PageId;

            // setup page properties
            // assuming an UmbracoPage always run with an Umbraco.Web.UmbracoContext
            var context = Umbraco.Web.UmbracoContext.Current;
            if (context == null)
                throw new Exception("UmbracoContext is null.");
            var request = context.PublishedContentRequest;
            if (request.HasPublishedContent == false)
                throw new Exception("PublishedContentRequest has no current content.");
            page pageObject = new page(request.PublishedContent);
            System.Web.HttpContext.Current.Items.Add("pageElements", pageObject.Elements);

            base.OnPreInit(e);
        }
    }
}
