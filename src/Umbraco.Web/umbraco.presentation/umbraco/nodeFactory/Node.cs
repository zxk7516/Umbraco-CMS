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
using umbraco.cms.businesslogic;
using umbraco.cms.businesslogic.propertytype;
using umbraco.interfaces;
using Umbraco.Core;
using Umbraco.Web;
using Umbraco.Web.PublishedCache;

namespace umbraco.NodeFactory
{
	/// <summary>
	/// Summary description for Node.
	/// </summary>

	[Serializable]
	[XmlType(Namespace = "http://umbraco.org/webservices/")]
	public class Node : INode
	{
		private bool _initialized;

	    private readonly XPathNavigator _nodeNav;

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
		private string _urlName;
		private string _path;
		private DateTime _createDate;
		private DateTime _updateDate;
		private Guid _version;
        private int _sortOrder;
        private int _level;

        #region Constructors

        public Node()
        {
            var preview = UmbracoContext.Current != null && UmbracoContext.Current.InPreviewMode;
            var nav = ContentCache.GetXPathNavigator(preview);
            if (nav.MoveToId(HttpContext.Current.Items["pageID"].ToString())) // fixme - Items["pageID"]
                _nodeNav = nav.Clone(); // each node has its own clone
            // else it remains null

            InitializeStructure();
            Initialize();

            //_pageXmlNode = ((IHasXmlNode)library.GetXmlNodeCurrent().Current).GetNode();
        }

	    internal Node(XPathNavigator nav, bool doNotInitialize = false)
	    {
	        _nodeNav = nav.Clone(); // each node has its own clone
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
        {
            if (forcePublishedXml == false)
                throw new ArgumentException("Use Node(int NodeId) if not forcing published xml");

            const bool preview = false; // force published

            if (id == -1)
            {
                _nodeNav = UmbracoContext.Current.ContentCache.GetXPathNavigator(preview); // no need to clone
                _nodeNav.MoveToRoot();
                _nodeNav.MoveToChild(XPathNodeType.Element);
            }
            else
            {
                var nav = UmbracoContext.Current.ContentCache.GetXPathNavigator(preview);
                if (nav.MoveToId(id.ToString()))
                    _nodeNav = nav.Clone(); // each node has its own clone
                // else it remains null
            }

            //_pageXmlNode = id != -1 
            //    ? content.Instance.XmlContent.GetElementById(id.ToString()) 
            //    : content.Instance.XmlContent.DocumentElement;

            InitializeStructure();
            Initialize();
        }

        public Node(int id)
        {
            if (id == -1)
            {
                var preview = UmbracoContext.Current != null && UmbracoContext.Current.InPreviewMode;
                _nodeNav = ContentCache.GetXPathNavigator(preview); // no need to clone
                _nodeNav.MoveToRoot();
                _nodeNav.MoveToChild(XPathNodeType.Element);
            }
            else
            {
                var nav = UmbracoContext.Current.ContentCache.GetXPathNavigator();
                if (nav.MoveToId(id.ToString()))
                    _nodeNav = nav.Clone(); // each node has its own clone
                // else it remains null

                //_pageXmlNode = ((IHasXmlNode)library.GetXmlNodeById(id.ToString()).Current).GetNode();
            }

            InitializeStructure();
            Initialize();
        }
        
        #endregion

        #region ContentCache

	    private IPublishedContentCache ContentCache
	    {
            get { return PublishedCachesResolver.Current.Caches.ContentCache; }
	    }

        #endregion

        #region Parent & Children

        public INode Parent
		{
			get
			{
				if (_initialized == false)
					Initialize();
				return _parent;
			}
		}

        public Nodes Children
        {
            get
            {
                if (_initialized == false)
                    Initialize();
                return _children;
            }
        }

        public List<INode> ChildrenAsList
        {
            get { return Children.Cast<INode>().ToList(); }
        }

        public DataTable ChildrenAsTable()
        {
            return GenerateDataTable(this);
        }

        public DataTable ChildrenAsTable(string nodeTypeAliasFilter)
        {
            return GenerateDataTable(this, nodeTypeAliasFilter);
        }

        #endregion

        #region Url

        public string Url
        {
            get { return UmbracoContext.Current.UrlProvider.GetUrl(Id); }
        }

        public string NiceUrl
        {
            get { return Url; }
        }

        #endregion

        #region Builtin Properties

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

		public string UrlName
		{
			get
			{
				if (_initialized == false)
					Initialize();
				return _urlName;
			}
		}

		public int Level
		{
			get
			{
				if (_initialized == false)
					Initialize();
				return _level;
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

        public List<IProperty> PropertiesAsList
		{
			get { return Properties.Cast<IProperty>().ToList(); }
		}


		public IProperty GetProperty(string alias)
		{
		    return Properties.Cast<Property>().FirstOrDefault(p => p.Alias == alias);
		}

	    public IProperty GetProperty(string alias, out bool propertyExists)
	    {
	        var property = GetProperty(alias);
	        propertyExists = property != null;
	        return property;
		}

        #endregion

        #region Initialize

        private void InitializeStructure()
		{
			// Load parent if it exists and is a node

            if (_nodeNav == null) return; // fixme ?!
            var nav = _nodeNav.Clone();

            if (nav.MoveToParent() 
                && nav.NodeType == XPathNodeType.Element
                && (nav.LocalName == "node" || nav.Clone().MoveToAttribute("isDoc", "")))
            {
                _parent = new Node(nav, true);
            }

            //if (_pageXmlNode != null && _pageXmlNode.SelectSingleNode("..") != null)
            //{
            //    XmlNode parent = _pageXmlNode.SelectSingleNode("..");
            //    if (parent != null && (parent.Name == "node" || (parent.Attributes != null && parent.Attributes.GetNamedItem("isDoc") != null)))
            //        _parent = new Node(parent, true);
            //}
		}

	    private static bool ReadAttribute(XPathNavigator nav, string name, Action<XPathNavigator> action)
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
            var nav = _nodeNav.Clone();

            _initialized = true;

		    _id = int.Parse(nav.GetAttribute("id", ""));
		    ReadAttribute(nav, "template", n => _template = n.ValueAsInt);
            ReadAttribute(nav, "sortOrder", n => _sortOrder = n.ValueAsInt);
            ReadAttribute(nav, "nodeName", n => _name = n.Value);
            ReadAttribute(nav, "writerName", n => _writerName = n.Value);
            ReadAttribute(nav, "urlName", n => _urlName = n.Value);
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
            ReadAttribute(nav, "level", n => _level = n.ValueAsInt);


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

            //if (_pageXmlNode != null)
            //{
            //    _initialized = true;
            //    if (_pageXmlNode.Attributes != null)
            //    {
            //        _id = int.Parse(_pageXmlNode.Attributes.GetNamedItem("id").Value);
            //        if (_pageXmlNode.Attributes.GetNamedItem("template") != null)
            //            _template = int.Parse(_pageXmlNode.Attributes.GetNamedItem("template").Value);
            //        if (_pageXmlNode.Attributes.GetNamedItem("sortOrder") != null)
            //            _sortOrder = int.Parse(_pageXmlNode.Attributes.GetNamedItem("sortOrder").Value);
            //        if (_pageXmlNode.Attributes.GetNamedItem("nodeName") != null)
            //            _name = _pageXmlNode.Attributes.GetNamedItem("nodeName").Value;
            //        if (_pageXmlNode.Attributes.GetNamedItem("writerName") != null)
            //            _writerName = _pageXmlNode.Attributes.GetNamedItem("writerName").Value;
            //        if (_pageXmlNode.Attributes.GetNamedItem("urlName") != null)
            //            _urlName = _pageXmlNode.Attributes.GetNamedItem("urlName").Value;
            //        // Creatorname is new in 2.1, so published xml might not have it!
            //        try
            //        {
            //            _creatorName = _pageXmlNode.Attributes.GetNamedItem("creatorName").Value;
            //        }
            //        catch
            //        {
            //            _creatorName = _writerName;
            //        }

            //        //Added the actual userID, as a user cannot be looked up via full name only... 
            //        if (_pageXmlNode.Attributes.GetNamedItem("creatorID") != null)
            //            _creatorId = int.Parse(_pageXmlNode.Attributes.GetNamedItem("creatorID").Value);
            //        if (_pageXmlNode.Attributes.GetNamedItem("writerID") != null)
            //            _writerId = int.Parse(_pageXmlNode.Attributes.GetNamedItem("writerID").Value);

            //        if (UmbracoConfig.For.UmbracoSettings().Content.UseLegacyXmlSchema)
            //        {
            //            if (_pageXmlNode.Attributes.GetNamedItem("nodeTypeAlias") != null)
            //                _nodeTypeAlias = _pageXmlNode.Attributes.GetNamedItem("nodeTypeAlias").Value;
            //        }
            //        else
            //        {
            //            _nodeTypeAlias = _pageXmlNode.Name;
            //        }

            //        if (_pageXmlNode.Attributes.GetNamedItem("path") != null)
            //            _path = _pageXmlNode.Attributes.GetNamedItem("path").Value;
            //        if (_pageXmlNode.Attributes.GetNamedItem("version") != null)
            //            _version = new Guid(_pageXmlNode.Attributes.GetNamedItem("version").Value);
            //        if (_pageXmlNode.Attributes.GetNamedItem("createDate") != null)
            //            _createDate = DateTime.Parse(_pageXmlNode.Attributes.GetNamedItem("createDate").Value);
            //        if (_pageXmlNode.Attributes.GetNamedItem("updateDate") != null)
            //            _updateDate = DateTime.Parse(_pageXmlNode.Attributes.GetNamedItem("updateDate").Value);
            //        if (_pageXmlNode.Attributes.GetNamedItem("level") != null)
            //            _level = int.Parse(_pageXmlNode.Attributes.GetNamedItem("level").Value);

            //    }

            //    // load data
            //    string dataXPath = UmbracoConfig.For.UmbracoSettings().Content.UseLegacyXmlSchema ? "data" : "* [not(@isDoc)]";
            //    foreach (XmlNode n in _pageXmlNode.SelectNodes(dataXPath))
            //        _properties.Add(new Property(n));

            //    // load children
            //    string childXPath = UmbracoConfig.For.UmbracoSettings().Content.UseLegacyXmlSchema ? "node" : "* [@isDoc]";
            //    XPathNavigator nav = _pageXmlNode.CreateNavigator();
            //    XPathExpression expr = nav.Compile(childXPath);
            //    expr.AddSort("@sortOrder", XmlSortOrder.Ascending, XmlCaseOrder.None, "", XmlDataType.Number);
            //    XPathNodeIterator iterator = nav.Select(expr);
            //    while (iterator.MoveNext())
            //    {
            //        _children.Add(
            //            new Node(((IHasXmlNode)iterator.Current).GetNode(), true)
            //            );
            //    }
            //}
            ////            else
            ////                throw new ArgumentNullException("Node xml source is null");
		}

        #endregion

        #region Static

        public static Node GetCurrent()
		{
			var id = getCurrentNodeId();
			return new Node(id);
		}

// ReSharper disable once InconsistentNaming
		public static int getCurrentNodeId()
		{
            if (UmbracoContext.Current.PublishedContentRequest.HasPublishedContent == false)
                throw new InvalidOperationException("There is no current content.");
		    return UmbracoContext.Current.PublishedContentRequest.PublishedContent.Id;

            // note: was previously based on HttpContext.Current.Items["pageID"]
            // but... that should not make a difference, should it?
            // fixme - conclusion?
        }

        public static Node GetNodeByXpath(string xpath)
        {
            // fixme - new cache does not have IHasXmlNode!!
            XPathNodeIterator itNode = library.GetXmlNodeByXPath(xpath);
            XmlNode nodeXmlNode = null;
            if (itNode.MoveNext())
            {
                nodeXmlNode = ((IHasXmlNode)itNode.Current).GetNode();
            }
            if (nodeXmlNode != null)
            {
                return new Node(nodeXmlNode);
            }
            return null;
        }

        #endregion

        #region Stuff

        private DataTable GenerateDataTable(INode node, string nodeTypeAliasFilter = "")
        {
            var firstNode = nodeTypeAliasFilter.IsNullOrWhiteSpace()
                                ? node.ChildrenAsList.Any()
                                    ? node.ChildrenAsList[0]
                                    : null
                                : node.ChildrenAsList.FirstOrDefault(x => x.NodeTypeAlias == nodeTypeAliasFilter);
            if (firstNode == null)
                return new DataTable(); //no children found 

            //use new utility class to create table so that we don't have to maintain code in many places, just one
            var dt = Umbraco.Core.DataTableExtensions.GenerateDataTable(
                //pass in the alias of the first child node since this is the node type we're rendering headers for
                firstNode.NodeTypeAlias,
                //pass in the callback to extract the Dictionary<string, string> of column aliases to names
                alias =>
                {
                    var userFields = ContentType.GetAliasesAndNames(alias);
                    //ensure the standard fields are there
                    var allFields = new Dictionary<string, string>()
							{
								{"Id", "Id"},
								{"NodeName", "NodeName"},
								{"NodeTypeAlias", "NodeTypeAlias"},
								{"CreateDate", "CreateDate"},
								{"UpdateDate", "UpdateDate"},
								{"CreatorName", "CreatorName"},
								{"WriterName", "WriterName"},
								{"Url", "Url"}
							};
                    foreach (var f in userFields.Where(f => allFields.ContainsKey(f.Key) == false))
                    {
                        allFields.Add(f.Key, f.Value);
                    }
                    return allFields;
                },
                //pass in a callback to populate the datatable, yup its a bit ugly but it's already legacy and we just want to maintain code in one place.
                () =>
                {
                    //create all row data
                    var tableData = Umbraco.Core.DataTableExtensions.CreateTableData();
                    //loop through each child and create row data for it
                    foreach (Node n in Children)
                    {
                        if (nodeTypeAliasFilter.IsNullOrWhiteSpace() == false)
                        {
                            if (n.NodeTypeAlias != nodeTypeAliasFilter)
                                continue; //skip this one, it doesn't match the filter
                        }

                        var standardVals = new Dictionary<string, object>()
								{
									{"Id", n.Id},
									{"NodeName", n.Name},
									{"NodeTypeAlias", n.NodeTypeAlias},
									{"CreateDate", n.CreateDate},
									{"UpdateDate", n.UpdateDate},
									{"CreatorName", n.CreatorName},
									{"WriterName", n.WriterName},
									{"Url", library.NiceUrl(n.Id)}
								};
                        var userVals = new Dictionary<string, object>();
                        foreach (var p in from Property p in n.Properties where p.Value != null select p)
                        {
                            userVals[p.Alias] = p.Value;
                        }
                        //add the row data
                        Umbraco.Core.DataTableExtensions.AddRowData(tableData, standardVals, userVals);
                    }
                    return tableData;
                }
                );
            return dt;
        }

        #endregion
    }
}