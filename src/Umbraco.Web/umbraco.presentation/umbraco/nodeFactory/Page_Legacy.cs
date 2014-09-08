using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Web;
using System.Xml;
using System.Xml.Serialization;
using System.Xml.XPath;
using Umbraco.Core.Configuration;
using Umbraco.Web.PublishedCache;
using Umbraco.Web.Templates;
using umbraco.cms.businesslogic;
using umbraco.cms.businesslogic.propertytype;

namespace umbraco.presentation.nodeFactory
{
    /// <summary>
    /// Summary description for Node.
    /// </summary>

    [Serializable]
    [XmlType(Namespace = "http://umbraco.org/webservices/")]
    [Obsolete("This class is obsolete; use class umbraco.NodeFactory.Node instead", false)]
    public class Node
    {
        private readonly XPathNavigator _nodeNav;
        private bool _initialized;

        private readonly Nodes _children = new Nodes();
        private Node _parent;
        private readonly Properties _properties = new Properties();

        private int _id;
        private int _template;
        private string _name;
        private string _nodeTypeAlias;
        private string _writerName;
        private string _creatorName;
        private int _writerId;
        private int _creatorId;

        private string _path;
        private DateTime _createDate;
        private DateTime _updateDate;
        private Guid _version;
        private int _sortOrder;

        private readonly Hashtable _aliasToNames = new Hashtable();

        #region Constructors

        public Node()
        {
            var nav = ContentCache.CreateNavigator(); // safe (no need to clone)
            if (nav.MoveToId(HttpContext.Current.Items["pageID"].ToString())) // fixme - Items["pageID"]
                _nodeNav = nav;
            // else it remains null

            InitializeStructure();
            Initialize();
        }

        internal Node(XPathNavigator nav, bool doNotInitialize = false)
        {
            _nodeNav = nav.Clone();  // assume garbage-in, clone
            InitializeStructure();
            if (doNotInitialize == false)
                Initialize();
        }

        public Node(XmlNode xmlNode)
            : this(xmlNode.CreateNavigator())
        { }

        public Node(XmlNode xmlNode, bool doNotInitialize)
            : this(xmlNode.CreateNavigator(), doNotInitialize)
        { }

        /// <summary>
        /// Special constructor for by-passing published vs. preview xml to use
        /// when updating the SiteMapProvider
        /// </summary>
        /// <param name="id"></param>
        /// <param name="forcePublishedXml"></param>
        public Node(int id, bool forcePublishedXml)
            : this(ContentCache.CreateNavigator(false), id, forcePublishedXml == false)
        {
            if (forcePublishedXml == false)
                throw new ArgumentException("Use Node(int NodeId) if not forcing published xml");
        }

        public Node(int id)
            : this(ContentCache.CreateNavigator(), id, false)
        { }

        private Node(XPathNavigator nav, int id, bool fail)
        {
            if (fail) return;

            // only invoked by one of the two ctors above
            // so nav is ContentCache.GetXPathNavigator which is safe (no need to clone)

            if (id == -1)
            {
                _nodeNav = nav;
                _nodeNav.MoveToRoot();
                _nodeNav.MoveToChild(XPathNodeType.Element);
            }
            else
            {
                if (nav.MoveToId(id.ToString()))
                    _nodeNav = nav;
                // else it remains null
            }

            InitializeStructure();
            Initialize();
        }


        #endregion

        #region ContentCache

        private static IPublishedContentCache ContentCache
        {
            // gets the "current" one - what is "current" is managed by the service
            get { return PublishedCachesServiceResolver.Current.Service.GetPublishedCaches().ContentCache; }
        }

        #endregion

        #region Parent & Children

        public Nodes Children
        {
            get
            {
                if (_initialized == false)
                    Initialize();
                return _children;
            }
        }

        public Node Parent
        {
            get
            {
                if (_initialized == false)
                    Initialize();
                return _parent;
            }
        }

        public DataTable ChildrenAsTable()
        {
            if (Children.Count > 0)
            {
                DataTable dt = generateDataTable(Children[0]);

                string firstNodeTypeAlias = Children[0].NodeTypeAlias;

                foreach (Node n in Children)
                {
                    if (n.NodeTypeAlias == firstNodeTypeAlias)
                    {
                        DataRow dr = dt.NewRow();
                        populateRow(ref dr, n, getPropertyHeaders(n));
                        dt.Rows.Add(dr);
                    }
                }
                return dt;
            }
            else
                return new DataTable();
        }

        public DataTable ChildrenAsTable(string nodeTypeAliasFilter)
        {

            if (Children.Count > 0)
            {

                Node Firstnode = null;
                Boolean nodeFound = false;
                foreach (Node n in Children)
                {
                    if (n.NodeTypeAlias == nodeTypeAliasFilter && !nodeFound)
                    {
                        Firstnode = n;
                        nodeFound = true;
                        break;
                    }
                }

                if (nodeFound)
                {
                    DataTable dt = generateDataTable(Firstnode);

                    foreach (Node n in Children)
                    {
                        if (n.NodeTypeAlias == nodeTypeAliasFilter)
                        {
                            DataRow dr = dt.NewRow();
                            populateRow(ref dr, n, getPropertyHeaders(n));
                            dt.Rows.Add(dr);
                        }
                    }
                    return dt;
                }
                else
                {
                    return new DataTable();
                }
            }
            else
                return new DataTable();
        }

        private DataTable generateDataTable(Node SchemaNode)
        {
            DataTable NodeAsDataTable = new DataTable(SchemaNode.NodeTypeAlias);
            string[] defaultColumns = {
                                          "Id", "NodeName", "NodeTypeAlias", "CreateDate", "UpdateDate", "CreatorName",
                                          "WriterName", "Url"
                                      };
            foreach (string s in defaultColumns)
            {
                DataColumn dc = new DataColumn(s);
                NodeAsDataTable.Columns.Add(dc);
            }

            // add properties
            Hashtable propertyHeaders = getPropertyHeaders(SchemaNode);
            IDictionaryEnumerator ide = propertyHeaders.GetEnumerator();
            while (ide.MoveNext())
            {
                DataColumn dc = new DataColumn(ide.Value.ToString());
                NodeAsDataTable.Columns.Add(dc);
            }

            return NodeAsDataTable;
        }

        private Hashtable getPropertyHeaders(Node SchemaNode)
        {
            if (_aliasToNames.ContainsKey(SchemaNode.NodeTypeAlias))
                return (Hashtable)_aliasToNames[SchemaNode.NodeTypeAlias];
            else
            {
                ContentType ct = ContentType.GetByAlias(SchemaNode.NodeTypeAlias);
                Hashtable def = new Hashtable();
                foreach (PropertyType pt in ct.PropertyTypes)
                    def.Add(pt.Alias, pt.Name);
                System.Web.HttpContext.Current.Application.Lock(); // how nice :-(
                _aliasToNames.Add(SchemaNode.NodeTypeAlias, def);
                System.Web.HttpContext.Current.Application.UnLock();

                return def;
            }
        }

        private void populateRow(ref DataRow dr, Node n, Hashtable AliasesToNames)
        {
            dr["Id"] = n.Id;
            dr["NodeName"] = n.Name;
            dr["NodeTypeAlias"] = n.NodeTypeAlias;
            dr["CreateDate"] = n.CreateDate;
            dr["UpdateDate"] = n.UpdateDate;
            dr["CreatorName"] = n.CreatorName;
            dr["WriterName"] = n.WriterName;
            dr["Url"] = library.NiceUrl(n.Id);

            int counter = 8;
            foreach (Property p in n.Properties)
            {
                if (p.Value != null)
                {
                    dr[AliasesToNames[p.Alias].ToString()] = p.Value;
                    counter++;
                }
            }
        }

        #endregion

        #region Url

        public string Url
        {
            get { return Umbraco.Web.UmbracoContext.Current.UrlProvider.GetUrl(Id); }
        }

        public string NiceUrl
        {
            get { return Url; }
        }

        #endregion

        #region Builtin properties

        public int Id
        {
            get
            {
                if (_initialized == false)
                    Initialize();
                return _id;
            }
        }

        public int template
        {
            get
            {
                if (_initialized == false)
                    Initialize();
                return _template;
            }
        }

        public int SortOrder
        {
            get
            {
                if (_initialized == false)
                    Initialize();
                return _sortOrder;
            }
        }

        public string Name
        {
            get
            {
                if (_initialized == false)
                    Initialize();
                return _name;
            }
        }

        public string NodeTypeAlias
        {
            get
            {
                if (_initialized == false)
                    Initialize();
                return _nodeTypeAlias;
            }
        }

        public string WriterName
        {
            get
            {
                if (_initialized == false)
                    Initialize();
                return _writerName;
            }
        }

        public string CreatorName
        {
            get
            {
                if (_initialized == false)
                    Initialize();
                return _creatorName;
            }
        }

        public int WriterID
        {
            get
            {
                if (_initialized == false)
                    Initialize();
                return _writerId;
            }
        }

        public int CreatorID
        {
            get
            {
                if (_initialized == false)
                    Initialize();
                return _creatorId;
            }
        }


        public string Path
        {
            get
            {
                if (_initialized == false)
                    Initialize();
                return _path;
            }
        }

        public DateTime CreateDate
        {
            get
            {
                if (_initialized == false)
                    Initialize();
                return _createDate;
            }
        }

        public DateTime UpdateDate
        {
            get
            {
                if (_initialized == false)
                    Initialize();
                return _updateDate;
            }
        }

        public Guid Version
        {
            get
            {
                if (_initialized == false)
                    Initialize();
                return _version;
            }
        }

        #endregion

        #region User Properties

        public Properties Properties
        {
            get
            {
                if (_initialized == false)
                    Initialize();
                return _properties;
            }
        }

        public Property GetProperty(string Alias)
        {
            return Properties.Cast<Property>().FirstOrDefault(p => p.Alias == Alias);
        }

        #endregion

        #region Initialize

        private void InitializeStructure()
        {
            // Load parent if it exists and is a node

            if (_nodeNav == null) return; // fixme ?!
            var nav = _nodeNav.Clone(); // so it's not impacted by what we do below

            if (nav.MoveToParent()
                && nav.NodeType == XPathNodeType.Element
                && (nav.LocalName == "node" || nav.Clone().MoveToAttribute("isDoc", "")))
            {
                _parent = new Node(nav, true);
            }
        }

        // action should NOT move the navigator!
        internal static bool ReadAttribute(XPathNavigator nav, string name, Action<XPathNavigator> action)
        {
            if (nav.MoveToAttribute(name, "") == false)
                return false;

            action(nav);
            nav.MoveToParent();
            return true;
        }

        private void Initialize()
        {
            if (_nodeNav == null) return; // fixme ?!
            var nav = _nodeNav.Clone(); // so it's not impacted by what we do below

            _initialized = true;

            _id = int.Parse(nav.GetAttribute("id", ""));
            ReadAttribute(nav, "template", n => _template = n.ValueAsInt);
            ReadAttribute(nav, "sortOrder", n => _sortOrder = n.ValueAsInt);
            ReadAttribute(nav, "nodeName", n => _name = n.Value);
            ReadAttribute(nav, "writerName", n => _writerName = n.Value);
            //ReadAttribute(nav, "urlName", n => _urlName = n.Value);
            if (ReadAttribute(nav, "creatorName", n => _creatorName = n.Value) == false)
                _creatorName = _writerName;
            ReadAttribute(nav, "creatorID", n => _creatorId = n.ValueAsInt);
            ReadAttribute(nav, "writerID", n => _writerId = n.ValueAsInt);
            if (UmbracoConfig.For.UmbracoSettings().Content.UseLegacyXmlSchema)
                ReadAttribute(nav, "nodeTypeAlias", n => _nodeTypeAlias = n.Value);
            else
                _nodeTypeAlias = _nodeNav.LocalName;
            ReadAttribute(nav, "path", n => _path = n.Value);
            ReadAttribute(nav, "version", n => _version = new Guid(n.Value));
            ReadAttribute(nav, "createDate", n => _createDate = n.ValueAsDateTime);
            ReadAttribute(nav, "updateDate", n => _updateDate = n.ValueAsDateTime);
            //ReadAttribute(nav, "level", n => _level = n.ValueAsInt);

            // load data, children
            var children = nav.SelectChildren(XPathNodeType.Element);
            var legacy = UmbracoConfig.For.UmbracoSettings().Content.UseLegacyXmlSchema;
            var temp = new List<Node>();
            while (children.MoveNext())
            {
                var n = children.Current;
                var isNode = legacy ? n.LocalName == "node" : n.Clone().MoveToAttribute("isDoc", "");
                if (isNode)
                    temp.Add(new Node(n, true));
                else
                    _properties.Add(new Property(n));
            }
            foreach (var n in temp.OrderBy(x => x.SortOrder))
                _children.Add(n);
        }

        #endregion

        #region Static

        public static Node GetCurrent()
        {
            if (Umbraco.Web.UmbracoContext.Current.PublishedContentRequest.HasPublishedContent == false)
                throw new InvalidOperationException("There is no current content.");
            var id = Umbraco.Web.UmbracoContext.Current.PublishedContentRequest.PublishedContent.Id;
            return new Node(id);

            // note: was previously based on HttpContext.Current.Items["pageID"]
            // but... that should not make a difference, should it?
            // fixme - conclusion?
        }

        #endregion
    }

    [Obsolete("This class is obsolete; use class umbraco.NodeFactory.Nodes instead", false)]
    public class Nodes : CollectionBase
    {
        public virtual void Add(Node NewNode)
        {
            List.Add(NewNode);
        }

        public virtual Node this[int Index]
        {
            get { return (Node)List[Index]; }
        }
    }

    [Serializable]
    [XmlType(Namespace = "http://umbraco.org/webservices/")]
    [Obsolete("This class is obsolete; use class umbraco.NodeFactory.Property instead", false)]
    public class Property
    {
        private Guid _version;
        private string _alias;
        private string _value;

        public string Alias
        {
            get { return _alias; }
        }

        private string _parsedValue;

        public string Value
        {
            get { return _parsedValue ?? (_parsedValue = TemplateUtilities.ResolveUrlsFromTextString(_value)); }
        }

        public Guid Version
        {
            get { return _version; }
        }

        public Property()
        {

        }

        public Property(XPathNavigator nav)
        {
            if (nav == null)
                throw new ArgumentNullException("nav");

            Node.ReadAttribute(nav, "versionID", n => _version = new Guid(n.Value));
        }

        public Property(XmlNode PropertyXmlData)
        {
            if (PropertyXmlData != null)
            {
                // For backward compatibility with 2.x (the version attribute has been removed from 3.0 data nodes)
                if (PropertyXmlData.Attributes.GetNamedItem("versionID") != null)
                    _version = new Guid(PropertyXmlData.Attributes.GetNamedItem("versionID").Value);
                _alias = UmbracoConfig.For.UmbracoSettings().Content.UseLegacyXmlSchema ?
                    PropertyXmlData.Attributes.GetNamedItem("alias").Value :
                    PropertyXmlData.Name;
                _value = xmlHelper.GetNodeValue(PropertyXmlData);
            }
            else
                throw new ArgumentNullException("Property xml source is null");
        }
    }

    [Obsolete("This class is obsolete; use class umbraco.NodeFactory.Properties instead", false)]
    public class Properties : CollectionBase
    {
        public virtual void Add(Property NewProperty)
        {
            List.Add(NewProperty);
        }

        public virtual Property this[int Index]
        {
            get { return (Property)List[Index]; }
        }
    }


}
