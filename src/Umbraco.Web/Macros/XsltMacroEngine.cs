using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Web;
using System.Web.Caching;
using System.Web.UI;
using System.Xml;
using System.Xml.XPath;
using System.Xml.Xsl;
using umbraco;
using umbraco.cms.businesslogic.macro;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Events;
using Umbraco.Core.IO;
using Umbraco.Core.Logging;
using Umbraco.Core.Macros;
using Umbraco.Core.Xml.XPath;
using Umbraco.Web.Templates;
using XsltExtensionAttribute = umbraco.XsltExtensionAttribute;

namespace Umbraco.Web.Macros
{
    public class XsltMacroEngine : IMacroEngine
    {
        private readonly Func<HttpContextBase> _getHttpContext;
        private readonly Func<UmbracoContext> _getUmbracoContext;
        private static readonly XsltSettings XsltSettings; // cache xslt settings

        public const string EngineName = "Xslt Macro Engine";

        public XsltMacroEngine()
        {
            _getHttpContext = () =>
            {
                if (HttpContext.Current == null)
                    throw new InvalidOperationException("The Xslt Macro Engine cannot execute with a null HttpContext.Current reference");
                return new HttpContextWrapper(HttpContext.Current);
            };

            _getUmbracoContext = () =>
            {
                if (UmbracoContext.Current == null)
                    throw new InvalidOperationException("The Xslt Macro Engine cannot execute with a null UmbracoContext.Current reference");
                return UmbracoContext.Current;
            };
        }

        static XsltMacroEngine()
        {
            XsltSettings = GlobalSettings.ApplicationTrustLevel > AspNetHostingPermissionLevel.Medium
                ? XsltSettings.TrustedXslt
                : XsltSettings.Default;
        }

        #region IMacroEngine stuff

        public string Name
        {
            get { return EngineName; }
        }

        //NOTE: We do not return any supported extensions because we don't want the MacroEngineFactory to return this
        // macro engine when searching for engines via extension. Those types of engines are reserved for files that are
        // stored in the ~/macroScripts folder and each engine must support unique extensions. This is a total Hack until 
        // we rewrite how macro engines work.
        public IEnumerable<string> SupportedExtensions
        {
            get { return Enumerable.Empty<string>(); }
        }

        //NOTE: We do not return any supported extensions because we don't want the MacroEngineFactory to return this
        // macro engine when searching for engines via extension. Those types of engines are reserved for files that are
        // stored in the ~/macroScripts folder and each engine must support unique extensions. This is a total Hack until 
        // we rewrite how macro engines work.
        public IEnumerable<string> SupportedUIExtensions
        {
            get { return Enumerable.Empty<string>(); }
        }

        public Dictionary<string, global::umbraco.interfaces.IMacroGuiRendering> SupportedProperties
        {
            get { throw new NotSupportedException(); }
        }

        #endregion

        // fixme - no idea what that is
        public bool Validate(string code, string tempFileName, global::umbraco.interfaces.INode currentPage, out string errorMessage)
        {
            errorMessage = string.Empty;
            return true;
        }

        public string Execute(MacroModel macro, global::umbraco.interfaces.INode currentPage)
        {
            return Execute(macro);
        }

        #region Execute Xslt

        // executes the macro, relying on GetXsltTransform
        // will pick XmlDocument or Navigator mode depending on the capabilities of the published caches
        private string Execute(MacroModel model)
        {
            if (model.Xslt.Trim() == string.Empty)
            {
                LogHelper.Warn<XsltMacroEngine>("Xslt is empty");
                return string.Empty;
            }

            var httpContext = _getHttpContext();

            using (DisposableTimer.DebugDuration<macro>("Executing XSLT: " + model.Xslt))
            {
                XmlDocument macroXml = null;
                MacroNavigator macroNavigator = null;
                NavigableNavigator contentNavigator = null;

                var canNavigate =
                    UmbracoContext.Current.ContentCache.XPathNavigatorIsNavigable &&
                    UmbracoContext.Current.MediaCache.XPathNavigatorIsNavigable;

                if (canNavigate)
                {
                    // the content & media caches can be navigated via XPath
                    // this is the preferred way to render the macro

                    contentNavigator = UmbracoContext.Current.ContentCache.GetXPathNavigator() as NavigableNavigator;
                    var mediaNavigator = UmbracoContext.Current.MediaCache.GetXPathNavigator() as NavigableNavigator;

                    var parameters = new List<MacroNavigator.MacroParameter>();
                    foreach (var prop in model.Properties)
                        AddMacroParameter(parameters, contentNavigator, mediaNavigator, prop.Key, prop.Type, prop.Value);

                    macroNavigator = new MacroNavigator(parameters);
                }
                else
                {
                    // the content or media cache can not be navigated via XPath
                    // so we have to render the macro on top of the Xml document

                    var cache = UmbracoContext.Current.ContentCache.InnerCache as PublishedCache.XmlPublishedCache.PublishedContentCache;
                    if (cache == null) throw new Exception("Unsupported IPublishedContentCache, only the Xml one is supported.");
                    var umbracoXml = cache.GetXml(UmbracoContext.Current, UmbracoContext.Current.InPreviewMode);

                    macroXml = new XmlDocument();
                    macroXml.LoadXml("<macro/>");

                    foreach (var prop in model.Properties)
                        AddMacroXmlNode(umbracoXml, macroXml, prop.Key, prop.Type, prop.Value);
                }

                // fixme - not sure we need to keep this ugly stuff
                if (httpContext.Request.QueryString["umbDebug"] != null && GlobalSettings.DebugMode)
                {
                    var outerXml = macroXml == null ? macroNavigator.OuterXml : macroXml.OuterXml;
                    return string.Format("<div style=\"border: 2px solid green; padding: 5px;\"><b>Debug from {0}<b><br />{1}</div>",
                        model.Name, HttpUtility.HtmlEncode(outerXml));
                }

                // get the transform
                XslCompiledTransform transform;
                try
                {
                    transform = GetCachedXsltTransform(model.Xslt);
                }
                catch (Exception e)
                {
                    throw new Exception(string.Format("Failed to read Xslt file \"{0}\".", model.Xslt), e);
                }

                using (DisposableTimer.DebugDuration<macro>("Performing transformation"))
                {
                    try
                    {
                        return canNavigate
                            ? XsltTransform(macroNavigator, transform, contentNavigator)
                            : XsltTransform(macroXml, transform);
                    }
                    catch (Exception e)
                    {
                        throw new Exception(string.Format("Failed to exec Xslt file \"{0}\".", model.Xslt), e);
                    }
                }
            }
        }

        /// <summary>
        /// Adds the XSLT extension namespaces to the XSLT header using 
        /// {0} as the container for the namespace references and
        /// {1} as the container for the exclude-result-prefixes
        /// </summary>
        /// <param name="xslt">The XSLT</param>
        /// <returns>The XSLT with {0} and {1} replaced.</returns>
        /// <remarks>This is done here because it needs the engine's XSLT extensions.</remarks>
        public static string AddXsltExtensionsToHeader(string xslt)
        {
            var namespaceList = new StringBuilder();
            var namespaceDeclaractions = new StringBuilder();
            foreach (var extension in GetXsltExtensions())
            {
                namespaceList.Append(extension.Key).Append(' ');
                namespaceDeclaractions.AppendFormat("xmlns:{0}=\"urn:{0}\" ", extension.Key);
            }

            // parse xslt
            xslt = xslt.Replace("{0}", namespaceDeclaractions.ToString());
            xslt = xslt.Replace("{1}", namespaceList.ToString());
            return xslt;
        }

        #endregion

        #region XmlDocument mode

        // add parameters to the <macro> root node
        // fixme - contains dirty code...
        private static void AddMacroXmlNode(XmlDocument umbracoXml, XmlDocument macroXml,
            string macroPropertyAlias, string macroPropertyType, string macroPropertyValue)
        {
            var macroXmlNode = macroXml.CreateNode(XmlNodeType.Element, macroPropertyAlias, string.Empty);

            // if no value is passed, then use the current "pageID" as value
            var contentId = macroPropertyValue == string.Empty ? UmbracoContext.Current.PageId.ToString() : macroPropertyValue;

            LogHelper.Info<XsltMacroEngine>(string.Format("Xslt node adding search start ({0},{1})", macroPropertyAlias, macroPropertyValue));

            switch (macroPropertyType)
            {
                case "contentTree":
                    var nodeId = macroXml.CreateAttribute("nodeID");
                    nodeId.Value = contentId;
                    macroXmlNode.Attributes.SetNamedItem(nodeId);

                    // Get subs
                    try
                    {
                        macroXmlNode.AppendChild(macroXml.ImportNode(umbracoXml.GetElementById(contentId), true));
                    }
                    catch
                    { }
                    break;

                case "contentCurrent":
                    var importNode = macroPropertyValue == string.Empty
                        ? umbracoXml.GetElementById(contentId)
                        : umbracoXml.GetElementById(macroPropertyValue);

                    var currentNode = macroXml.ImportNode(importNode, true);

                    // remove all sub content nodes
                    foreach (XmlNode n in currentNode.SelectNodes("node|*[@isDoc]"))
                        currentNode.RemoveChild(n);

                    macroXmlNode.AppendChild(currentNode);

                    break;

                case "contentSubs": // disable that one, it does not work anyway...
                    //x.LoadXml("<nodes/>");
                    //x.FirstChild.AppendChild(x.ImportNode(umbracoXml.GetElementById(contentId), true));
                    //macroXmlNode.InnerXml = TransformMacroXml(x, "macroGetSubs.xsl");
                    break;

                case "contentAll":
                    macroXmlNode.AppendChild(macroXml.ImportNode(umbracoXml.DocumentElement, true));
                    break;

                case "contentRandom":
                    XmlNode source = umbracoXml.GetElementById(contentId);
                    if (source != null)
                    {
                        var sourceList = source.SelectNodes("node|*[@isDoc]");
                        if (sourceList.Count > 0)
                        {
                            int rndNumber;
                            var r = library.GetRandom();
                            lock (r)
                            {
                                rndNumber = r.Next(sourceList.Count);
                            }
                            var node = macroXml.ImportNode(sourceList[rndNumber], true);
                            // remove all sub content nodes
                            foreach (XmlNode n in node.SelectNodes("node|*[@isDoc]"))
                                node.RemoveChild(n);

                            macroXmlNode.AppendChild(node);
                        }
                        else
                            LogHelper.Warn<XsltMacroEngine>("Error adding random node - parent (" + macroPropertyValue + ") doesn't have children!");
                    }
                    else
                        LogHelper.Warn<XsltMacroEngine>("Error adding random node - parent (" + macroPropertyValue + ") doesn't exists!");
                    break;

                case "mediaCurrent":
                    if (string.IsNullOrEmpty(macroPropertyValue) == false)
                    {
                        var c = new global::umbraco.cms.businesslogic.Content(int.Parse(macroPropertyValue));
                        macroXmlNode.AppendChild(macroXml.ImportNode(c.ToXml(global::umbraco.content.Instance.XmlContent, false), true));
                    }
                    break;

                default:
                    macroXmlNode.InnerText = HttpUtility.HtmlDecode(macroPropertyValue);
                    break;
            }
            macroXml.FirstChild.AppendChild(macroXmlNode);
        }

        // gets the result of the xslt transform - XmlDocument mode
        public static string XsltTransform(IXPathNavigable macroXml, XslCompiledTransform xslt,
            IDictionary<string, object> parameters = null)
        {
            TextWriter tw = new StringWriter();

            XsltArgumentList xslArgs;

            using (DisposableTimer.DebugDuration<macro>("Adding XSLT Extensions"))
            {
                xslArgs = GetXsltArgumentListWithExtensions();
                var lib = new library();
                xslArgs.AddExtensionObject("urn:umbraco.library", lib);
            }

            // add parameters
            if (parameters == null || parameters.ContainsKey("currentPage") == false)
                xslArgs.AddParam("currentPage", string.Empty, library.GetXmlNodeCurrent());

            if (parameters != null)
                foreach (var parameter in parameters)
                    xslArgs.AddParam(parameter.Key, string.Empty, parameter.Value);

            // transform
            var nav = macroXml.CreateNavigator();
            if (nav == null)
                throw new Exception("Internal error, navigator is null.");
            using (DisposableTimer.DebugDuration<macro>("Executing XSLT transform"))
            {
                xslt.Transform(nav, xslArgs, tw);
            }
            return TemplateUtilities.ResolveUrlsFromTextString(tw.ToString());
        }

        #endregion

        #region Navigator mode

        // add parameters to the macro parameters collection
        private static void AddMacroParameter(ICollection<MacroNavigator.MacroParameter> parameters,
            NavigableNavigator contentNavigator, NavigableNavigator mediaNavigator,
            string macroPropertyAlias, string macroPropertyType, string macroPropertyValue)
        {
            // if no value is passed, then use the current "pageID" as value
            var contentId = macroPropertyValue == string.Empty ? UmbracoContext.Current.PageId.ToString() : macroPropertyValue;

            LogHelper.Info<XsltMacroEngine>(string.Format("Xslt node adding search start ({0},{1})", macroPropertyAlias, macroPropertyValue));

            // beware! do not use the raw content- or media- navigators, but clones !!

            switch (macroPropertyType)
            {
                case "contentTree":
                    parameters.Add(new MacroNavigator.MacroParameter(
                        macroPropertyAlias,
                        contentNavigator.CloneWithNewRoot(contentId), // null if not found - will be reported as empty
                        attributes: new Dictionary<string, string> { { "nodeID", contentId } }));

                    break;

                case "contentPicker":
                    parameters.Add(new MacroNavigator.MacroParameter(
                        macroPropertyAlias,
                        contentNavigator.CloneWithNewRoot(contentId), // null if not found - will be reported as empty
                        0));
                    break;

                case "contentSubs":
                    parameters.Add(new MacroNavigator.MacroParameter(
                        macroPropertyAlias,
                        contentNavigator.CloneWithNewRoot(contentId), // null if not found - will be reported as empty
                        1));
                    break;

                case "contentAll":
                    parameters.Add(new MacroNavigator.MacroParameter(macroPropertyAlias, contentNavigator.Clone()));
                    break;

                case "contentRandom":
                    var nav = contentNavigator.Clone();
                    if (nav.MoveToId(contentId))
                    {
                        var descendantIterator = nav.Select("./* [@isDoc]");
                        if (descendantIterator.MoveNext())
                        {
                            // not empty - and won't change
                            var descendantCount = descendantIterator.Count;

                            int index;
                            var r = library.GetRandom();
                            lock (r)
                            {
                                index = r.Next(descendantCount);
                            }

                            while (index > 0 && descendantIterator.MoveNext())
                                index--;

                            var node = descendantIterator.Current.UnderlyingObject as INavigableContent;
                            if (node != null)
                            {
                                nav = contentNavigator.CloneWithNewRoot(node.Id.ToString(CultureInfo.InvariantCulture));
                                parameters.Add(new MacroNavigator.MacroParameter(macroPropertyAlias, nav, 0));
                            }
                            else
                                throw new InvalidOperationException("Iterator contains non-INavigableContent elements.");
                        }
                        else
                            LogHelper.Warn<XsltMacroEngine>("Error adding random node - parent (" + macroPropertyValue + ") doesn't have children!");
                    }
                    else
                        LogHelper.Warn<XsltMacroEngine>("Error adding random node - parent (" + macroPropertyValue + ") doesn't exists!");
                    break;

                case "mediaCurrent":
                    parameters.Add(new MacroNavigator.MacroParameter(
                        macroPropertyAlias,
                        mediaNavigator.CloneWithNewRoot(contentId), // null if not found - will be reported as empty
                        0));
                    break;

                default:
                    parameters.Add(new MacroNavigator.MacroParameter(macroPropertyAlias, HttpUtility.HtmlDecode(macroPropertyValue)));
                    break;
            }
        }

        // gets the result of the xslt transform - Navigator mode
        private static string XsltTransform(IXPathNavigable macroNavigator, XslCompiledTransform xslt,
            XPathNavigator contentNavigator, IDictionary<string, object> parameters = null)
        {
            TextWriter tw = new StringWriter();

            XsltArgumentList xslArgs;
            using (DisposableTimer.DebugDuration<macro>("Adding XSLT Extensions"))
            {
                xslArgs = GetXsltArgumentListWithExtensions();
                var lib = new library();
                xslArgs.AddExtensionObject("urn:umbraco.library", lib);
            }

            // add parameters
            if (parameters == null || parameters.ContainsKey("currentPage") == false)
            {
                // note: "PageId" is a legacy stuff that might be != from what's in current PublishedContentRequest
                var currentPageId = UmbracoContext.Current.PageId;
                var current = contentNavigator.Clone().Select("//* [@id=" + currentPageId + "]");
                xslArgs.AddParam("currentPage", string.Empty, current);
            }
            if (parameters != null)
            {
                foreach (var parameter in parameters)
                    xslArgs.AddParam(parameter.Key, string.Empty, parameter.Value);
            }

            // transform
            using (DisposableTimer.DebugDuration<macro>("Executing XSLT transform"))
            {
                xslt.Transform(macroNavigator, xslArgs, tw);
            }
            return TemplateUtilities.ResolveUrlsFromTextString(tw.ToString());
        }

        #endregion

        #region Manage transforms

        private static XslCompiledTransform GetCachedXsltTransform(string filename)
        {
            //TODO: SD: Do we really need to cache this??
            var filepath = IOHelper.MapPath(SystemDirectories.Xslt.EnsureEndsWith('/') + filename);
            return ApplicationContext.Current.ApplicationCache.GetCacheItem(
                CacheKeys.MacroXsltCacheKey + filename,
                CacheItemPriority.Default,
                new CacheDependency(filepath),
                () =>
                {
                    using (var xslReader = new XmlTextReader(filepath))
                    {
                        return GetXsltTransform(xslReader, GlobalSettings.DebugMode);
                    }
                });
        }

        public static XslCompiledTransform GetXsltTransform(XmlTextReader xslReader, bool debugMode)
        {
            var transform = new XslCompiledTransform(debugMode);
            var xslResolver = new XmlUrlResolver
            {
                Credentials = CredentialCache.DefaultCredentials
            };

            xslReader.EntityHandling = EntityHandling.ExpandEntities;

            try
            {
                transform.Load(xslReader, XsltSettings, xslResolver);
            }
            finally
            {
                xslReader.Close();
            }

            return transform;
        }

        #endregion

        #region Manage extensions

        /*
        private static readonly string XsltExtensionsConfig =
            IOHelper.MapPath(SystemDirectories.Config + "/xsltExtensions.config");

        private static readonly Func<CacheDependency> XsltExtensionsDependency =
            () => new CacheDependency(XsltExtensionsConfig);
        */

        // creates and return an Xslt argument list with all Xslt extensions.
        public static XsltArgumentList GetXsltArgumentListWithExtensions()
        {
            var xslArgs = new XsltArgumentList();

            foreach (var extension in GetXsltExtensions())
            {
                var extensionNamespace = "urn:" + extension.Key;
                xslArgs.AddExtensionObject(extensionNamespace, extension.Value);
                LogHelper.Info<XsltMacroEngine>(string.Format("Extension added: {0}, {1}",
                    extensionNamespace, extension.Value.GetType().Name));
            }

            return xslArgs;
        }

        /*
        // gets the collection of all XSLT extensions for macros
        // ie predefined, configured in the config file, and marked with the attribute
        public static Dictionary<string, object> GetCachedXsltExtensions()
        {
            // We could cache the extensions in a static variable but then the cache
            // would not be refreshed when the .config file is modified. An application
            // restart would be required. Better use the cache and add a dependency.

            // SD: The only reason the above statement might be true is because the xslt extension .config file is not a 
            // real config file!! if it was, we wouldn't have this issue. Having these in a static variable would be preferred!
            //  If you modify a config file, the app restarts and thus all static variables are reset.
            //  Having this stuff in cache just adds to the gigantic amount of cache data and will cause more cache turnover to happen.

            return ApplicationContext.Current.ApplicationCache.GetCacheItem(
                "UmbracoXsltExtensions",
                CacheItemPriority.NotRemovable, // NH 4.7.1, Changing to NotRemovable
                null, // no refresh action
                XsltExtensionsDependency(), // depends on the .config file
                TimeSpan.FromDays(1), // expires in 1 day (?)
                GetXsltExtensions);
        }
        */

        // actually gets the collection of all XSLT extensions for macros
        // ie predefined, configured in the config file, and marked with the attribute
        public static Dictionary<string, object> GetXsltExtensions()
        {
            return XsltExtensionsResolver.Current.XsltExtensions
                .ToDictionary(x => x.Namespace, x => x.ExtensionObject);

            /*
            // initialize the collection
            // there is no "predefined" extensions anymore
            var extensions = new Dictionary<string, object>();

            // Load the XSLT extensions configuration
            var xsltExt = new XmlDocument();
            xsltExt.Load(XsltExtensionsConfig);

            // get the configured types
            var extensionsNode = xsltExt.SelectSingleNode("/XsltExtensions");
            if (extensionsNode != null)
                foreach (var attributes in extensionsNode.Cast<XmlNode>()
                    .Where(x => x.NodeType == XmlNodeType.Element)
                    .Select(x => x.Attributes))
                {
                    Debug.Assert(attributes["assembly"] != null, "Extension attribute 'assembly' not specified.");
                    Debug.Assert(attributes["type"] != null, "Extension attribute 'type' not specified.");
                    Debug.Assert(attributes["alias"] != null, "Extension attribute 'alias' not specified.");

                    // load the extension assembly
                    var extensionFile = IOHelper.MapPath(string.Format("{0}/{1}.dll",
                        SystemDirectories.Bin, attributes["assembly"].Value));

                    Assembly extensionAssembly;
                    try
                    {
                        extensionAssembly = Assembly.LoadFrom(extensionFile);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception(
                            String.Format("Could not load assembly {0} for XSLT extension {1}. Please check config/xsltExtensions.config.",
                                extensionFile, attributes["alias"].Value), ex);
                    }

                    // load the extension type
                    var extensionType = extensionAssembly.GetType(attributes["type"].Value);
                    if (extensionType == null)
                        throw new Exception(
                            String.Format("Could not load type {0} ({1}) for XSLT extension {2}. Please check config/xsltExtensions.config.",
                                attributes["type"].Value, extensionFile, attributes["alias"].Value));

                    // create an instance and add it to the extensions list
                    extensions.Add(attributes["alias"].Value, Activator.CreateInstance(extensionType));
                }

            // get types marked with XsltExtension attribute
            var foundExtensions = PluginManager.Current.ResolveXsltExtensions();
            foreach (var xsltType in foundExtensions)
            {
                var attributes = xsltType.GetCustomAttributes<XsltExtensionAttribute>(true);
                var xsltTypeName = xsltType.FullName;
                foreach (var ns in attributes
                    .Select(attribute => string.IsNullOrEmpty(attribute.Namespace) ? attribute.Namespace : xsltTypeName))
                {
                    extensions.Add(ns, Activator.CreateInstance(xsltType));
                }
            }

            return extensions;
            */
        }

        #endregion
    }
}
