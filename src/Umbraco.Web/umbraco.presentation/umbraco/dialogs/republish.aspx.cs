using System;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Web;
using System.Web.SessionState;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Web.UI.HtmlControls;
using Umbraco.Web;
using Umbraco.Web.Cache;
using Umbraco.Web.PublishedCache;
using Umbraco.Web.PublishedCache.XmlPublishedCache;

namespace umbraco.cms.presentation
{
	/// <summary>
	/// Summary description for republish.
	/// </summary>
	public partial class republish : BasePages.UmbracoEnsuredPage
	{
	    public republish()
	    {
            CurrentApp = BusinessLogic.DefaultApps.content.ToString();
	    }

        // triggered by OnClick="go" in aspx page
        protected void go(object sender, EventArgs e) {

            // note: not really "re-publishing" here but making sure the content cache
            // is in sync with the content service (ie the data in database) - will not
            // trigger any of the publishing events

            if (Request.GetItemAsString("xml") != "")
            {
                // this is XML-cache specific
                // re-generate cmsContentXml and cmsPreviewXml content - for content only
                // default action when the form is used
                var svc = PublishedCachesServiceResolver.Current.Service as PublishedCachesService;
                if (svc == null)
                    throw new NotSupportedException("Unsupported IPublishedCachesService, only the Xml one is supported.");

                Server.ScriptTimeout = 100000; // may take time
                svc.RebuildContentAndPreviewXml();
            }
            else if (Request.GetItemAsString("previews") != "")
            {
                // this is XML-cache specific
                // re-generate cmsPreviewXml content 
                // there's no interface for that one - has to be called manually
                // we don't support it anymore
                throw new NotSupportedException("Obsolete.");
            }
            else if (Request.GetItemAsString("refreshNodes") != "")
            {
                // this is XML-cache specific
                // re-generate cmsContentXml for all children immediately below a document
                // there's no interface for that one - has to be called manually
                // we don't support it anymore
                throw new NotSupportedException("Obsolete.");
            }

            // tell each of the distributed caches to reset themselves, ie to
            // reload from whatever "central" intermediate cache they use, if any,
            // eg the cmsContentXml table for the XML-cache.
            DistributedCache.Instance.RefreshAllPublishedContentCache();

            p_republish.Visible = false;
            p_done.Visible = true;
        }

		protected void Page_Load(object sender, System.EventArgs e)
		{
			bt_go.Text = ui.Text("republish");
		}

		
	}
}
