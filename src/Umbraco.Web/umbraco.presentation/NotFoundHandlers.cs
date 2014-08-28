using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Web;
using System.Xml;
using Umbraco.Core.Configuration;
using Umbraco.Core.Logging;
using umbraco.BusinessLogic;
using umbraco.cms.businesslogic.member;
using umbraco.cms.businesslogic.template;
using umbraco.cms.businesslogic.web;
using umbraco.interfaces;
using Umbraco.Core.IO;
using umbraco.NodeFactory;

namespace umbraco {


    /// <summary>
    /// THIS CLASS IS PURELY HERE TO SUPPORT THE QUERYBYXPATH METHOD WHICH IS USED BY OTHER LEGACY BITS
    /// </summary>    
    // this class is not used anymore?
    // is internal, and is not referenced by any of our projects?!
    //internal class LegacyRequestHandler
    //{ }

    // classes below were never executed because the new handlers were used in place
    // of the old ones (see NotFoundHandlerHelper.SubstituteFinder) so just remove their
    // implementation - but keep them here so it does not break configuration.

    public class SearchForAlias : INotFoundHandler 
    {
        public bool CacheUrl
        {
            get { throw new NotImplementedException(); }
        }

        public bool Execute(string url)
        {
            throw new NotImplementedException();
        }

        public int redirectID
        {
            get { throw new NotImplementedException(); }
        }
    }

    public class SearchForTemplate : INotFoundHandler
    {
        public bool CacheUrl
        {
            get { throw new NotImplementedException(); }
        }

        public bool Execute(string url)
        {
            throw new NotImplementedException();
        }

        public int redirectID
        {
            get { throw new NotImplementedException(); }
        }
    }

    public class SearchForProfile : INotFoundHandler
    {
        public bool CacheUrl
        {
            get { throw new NotImplementedException(); }
        }

        public bool Execute(string url)
        {
            throw new NotImplementedException();
        }

        public int redirectID
        {
            get { throw new NotImplementedException(); }
        }
    }

    public class handle404 : INotFoundHandler 
    {
        public bool CacheUrl {
            get { throw new NotImplementedException(); }
        }

        public bool Execute(string url) {
            throw new NotImplementedException();
        }

        public int redirectID {
            get { throw new NotImplementedException(); }
        }
    }
}