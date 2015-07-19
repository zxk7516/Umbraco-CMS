using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Caching;
using System.Web.Hosting;
using System.Web.UI;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Configuration;
using Umbraco.Core.Events;
using Umbraco.Core.IO;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using umbraco.interfaces;
using Umbraco.Web;
using Umbraco.Web.Macros;
using Umbraco.Web.Models;
using umbraco.BusinessLogic;
using umbraco.cms.businesslogic.macro;
using Umbraco.Web.Security;
using Umbraco.Web.umbraco.presentation;
using File = System.IO.File;
using MacroErrorEventArgs = Umbraco.Core.Events.MacroErrorEventArgs;
using Macro = umbraco.cms.businesslogic.macro.Macro;
//using MacroTypes = Umbraco.Core.Models.MacroTypes;
using MacroTypes = umbraco.cms.businesslogic.macro.MacroTypes;

// ReSharper disable once CheckNamespace
namespace umbraco
{
    /// <summary>
    /// Summary description for macro.
    /// </summary>
// ReSharper disable once InconsistentNaming
    public class macro
    {
        #region Instance fields

        // the list of exceptions thrown while executing this macro
        public IList<Exception> Exceptions = new List<Exception>();

        // I have no idea what that is - obsolete it, and make sure we only
        // instanciate the StringBuilder if we do want it
        [Obsolete("Why would someone use this?", false)]
        public String MacroContent
        {
            set { (_content ?? (_content = new StringBuilder())).Append(value); }
            get { return _content == null ? string.Empty : _content.ToString(); }
        }
        private StringBuilder _content;

        #endregion

        #region Model

        // there's little point separating "macro" from "MacroModel" and things
        // should really be organized differently - but that would break compatibility,
        // so for the time being, let's keep it as it is, but obsolete a few things.

        // the macro model for this macro
        public MacroModel Model { get; private set; }

        // we should get rid of these and access them through the model
        // keep them here for backward compatibility - though they should
        // not be used anywhere anymore in the Core

        [Obsolete("Use Model.CacheByMember")]
        public bool CacheByPersonalization
        {
            get { return Model.CacheByMember; }
        }

        [Obsolete("Use Model.CacheByPage")]
        public bool CacheByPage
        {
            get { return Model.CacheByPage; }
        }

        [Obsolete("Use Model.RenderInEditor")]
        public bool DontRenderInEditor
        {
            get { return Model.RenderInEditor == false; }
        }

        [Obsolete("Use Model.CacheDuration")]
        public int RefreshRate
        {
            get { return Model.CacheDuration; }
        }

        [Obsolete("Use Model.Alias")]
        public String Alias
        {
            get { return Model.Alias; }
        }

        [Obsolete("Use Model.Name")]
        public String Name
        {
            get { return Model.Name; }
        }

        [Obsolete("Use Model.Xslt")]
        public String XsltFile
        {
            get { return Model.Xslt; }
        }

        [Obsolete("Use Model.ScriptName")]
        public String ScriptFile
        {
            get { return Model.ScriptName; }
        }

        [Obsolete("Use Model.TypeName")]
        public String ScriptType
        {
            get { return Model.TypeName; }
        }

        [Obsolete("Use Model.TypeName")]
        public String ScriptAssembly
        {
            get { return Model.TypeName; }
        }

        [Obsolete("Use Model.MacroType")]
        public int MacroType
        {
            get { return (int)Model.MacroType; }
        }

        #endregion

        #region GetMacro, constructors, ToString

        // initializes an empty macro
        [Obsolete("Should be private.", false)]
        public macro()
        {
            Model = new MacroModel();
        }

        // initializes a macro with a Macro
        [Obsolete("Should be removed.", false)]
        public macro(Macro macro)
        {
            Model = new MacroModel(macro);
        }

        // initializes a macro with a Macro
        private macro(IMacro macro)
        {
            Model = new MacroModel(macro);
        }

        // initializes a macro identified by its alias
        [Obsolete("Use GetMacro().", false)]
        public macro(string alias)
        {
            var macro = ApplicationContext.Current.Services.MacroService.GetByAlias(alias);
            Model = new MacroModel(macro);
        }

        // initializes a macro identified by its id
        [Obsolete("Use GetMacro().", false)]
        public macro(int id)
        {
            var macro = ApplicationContext.Current.Services.MacroService.GetById(id);
            Model = new MacroModel(macro);
        }

        // gets an anonymous macro
        public static macro GetMacro()
        {
// ReSharper disable once CSharpWarnings::CS0618
            return new macro();
        }

        // gets a new macro identified by its alias
        public static macro GetMacro(string alias)
        {
            var macro = ApplicationContext.Current.Services.MacroService.GetByAlias(alias);
            return macro == null ? null : new macro(macro);
        }

        // gets a new macro identified by its id
        public static macro GetMacro(int id)
        {
            var macro = ApplicationContext.Current.Services.MacroService.GetById(id);
            return macro == null ? null : new macro(macro);
        }

        // tostring
        public override string ToString()
        {
            return Model.Name;
        }

        #endregion

        #region MacroContent cache

        // gets this macro content cache identifier
        string GetContentCacheIdentifier(MacroModel model, int pageId)
        {
            var id = new StringBuilder();

            var alias = string.IsNullOrEmpty(model.ScriptCode)
                ? model.Alias
                : Macro.GenerateCacheKeyFromCode(model.ScriptCode);
            id.AppendFormat("{0}-", alias);

            if (Model.CacheByPage)
                id.AppendFormat("{0}-", pageId);

            if (model.CacheByMember)
            {
                // cannot use CurrentLoginStatus, need the actual member, so we can use its ID
                var member = (new MembershipHelper(UmbracoContext.Current)).GetCurrentMember();
                id.AppendFormat("m{0}-", member == null ? 0 : member.Id);
            }

            foreach (var value in model.Properties.Select(prop => prop.Value))
                id.AppendFormat("{0}-", value.Length <= 255 ? value : value.Substring(0, 255));

            return id.ToString();
        }

        // gets this macro content from the cache
        // ensure that it is appropriate to use the cache
        internal static MacroContent GetMacroContentFromCache(MacroModel model)
        {
            // only if cache is enabled
            if (UmbracoContext.Current.InPreviewMode || model.CacheDuration <= 0) return null;

            var cache = ApplicationContext.Current.ApplicationCache.RuntimeCache;
            var macroContent = cache.GetCacheItem<MacroContent>(CacheKeys.MacroContentCacheKey + model.CacheIdentifier);

            if (macroContent == null) return null;

            LogHelper.Debug<macro>("Macro content loaded from cache \"{0}\".", () => model.CacheIdentifier);

            // ensure that the source has not changed
            // fixme - does not handle dependencies (never has)
            var macroSource = GetMacroFile(model); // null if macro is not file-based
            if (macroSource != null)
            {
                if (macroSource.Exists == false)
                {
                    LogHelper.Debug<macro>("Macro source does not exist anymore, ignore cache.");
                    return null;
                }

                if (macroContent.Date < macroSource.LastWriteTime)
                {
                    LogHelper.Debug<macro>("Macro source has changed, ignore cache.");
                    return null;
                }
            }

            // fixme - legacy - what's the point? (let's keep it)
            if (macroContent.Control != null)
                macroContent.Control.ID = macroContent.ControlId;

            return macroContent;
        }

        // stores macro content into the cache
        void AddMacroContentToCache(MacroModel model, MacroContent macroContent)
        {
            // only if cache is enabled
            if (UmbracoContext.Current.InPreviewMode || model.CacheDuration <= 0) return;

            // just make sure...
            if (macroContent == null) return;

            // do not cache if it should cache by member and there's not member 
            if (Model.CacheByMember && (new MembershipHelper(UmbracoContext.Current)).IsLoggedIn() == false) return;

            // scripts and xslt can be cached as strings but not controls
            // as page events (Page_Load) wouldn't be hit -- yet caching
            // controls is a bad idea, it can lead to plenty of issues ?!
            // eg with IDs...

            // fixme - legacy - what's the point? (let's keep it)
            if (macroContent.Control != null)
                macroContent.ControlId = macroContent.Control.ID;

            // remember when we cache the content
            macroContent.Date = DateTime.Now;

            var cache = ApplicationContext.Current.ApplicationCache.RuntimeCache;
            cache.InsertCacheItem(
                CacheKeys.MacroContentCacheKey + model.CacheIdentifier,
                () => macroContent,
                new TimeSpan(0, 0, model.CacheDuration),
                priority: CacheItemPriority.NotRemovable
                );

            LogHelper.Debug<macro>("Macro content saved to cache \"{0}\".", () => model.CacheIdentifier);
        }

        /// <summary>
        /// Gets the macro source file name.
        /// </summary>
        /// <remarks>Or null if the macro is not file-based.</remarks>
        internal static string GetMacroFileName(MacroModel model)
        {
            string filename;

            switch (model.MacroType)
            {
                case MacroTypes.XSLT:
                    filename = SystemDirectories.Xslt.EnsureEndsWith('/') + model.Xslt;
                    break;
                case MacroTypes.Python:
                case MacroTypes.Script:
                    filename = SystemDirectories.MacroScripts.EnsureEndsWith('/') + model.ScriptName;
                    break;
                case MacroTypes.PartialView:
                    filename = model.ScriptName; //partial views are saved with their full virtual path
                    break;
                case MacroTypes.UserControl:
                    filename = model.TypeName; //user controls are saved with their full virtual path
                    break;
                //case MacroTypes.CustomControl:
                //case MacroTypes.Unknown:
                default:
                    // not file-based
                    filename = null;
                    break;
            }

            return filename;
        }

        /// <summary>
        /// Gets the macro source file.
        /// </summary>
        /// <remarks>Or null if the macro is not file-based.</remarks>
        internal static FileInfo GetMacroFile(MacroModel model)
        {
            var filename = GetMacroFileName(model);
            if (filename == null) return null;

            var mapped = HostingEnvironment.MapPath(filename);
            if (mapped == null) return null;

            var file = new FileInfo(mapped);
            if (file.Exists == false) return null;

            return file;
        }

        #endregion

        #region MacroModel properties

        // updates the model properties values according to the attributes
        public void UpdateMacroModelProperties(Hashtable attributes)
        {
            foreach (var prop in Model.Properties)
            {
                var key = prop.Key.ToLowerInvariant();
                prop.Value = attributes.ContainsKey(key) 
                    ? attributes[key].ToString()
                    : string.Empty;
            }
        }

        // generates the model properties according to the attributes
        public void GenerateMacroModelPropertiesFromAttributes(Hashtable attributes)
        {
            foreach (string key in attributes.Keys)
            {
                var prop = new MacroPropertyModel(key, attributes[key].ToString());
                Model.Properties.Add(prop);
            }
        }
        
        #endregion

        #region Render/Execute macro

        // first one is called by
        // - Umbraco.Web.UmbracoHelper.RenderMacro() when rendering a macro through the helper
        // - umbraco.presentation.templateControls.Macro.CreateChildControls() when rendering a <umbraco:Macro />
        // - umbraco.template.parseStringBuilder() when rendering macros in legacy templates (obsolete)
        // - umbraco.presentation.macroResultWrapper.Page_Load() when rendering a macro for RTE (?)
        //
        // second one is called by
        // - umbraco.presentation.templateControls.Macro.CreateChildControls() when rendering a <umbraco:Macro />
        // when the macro is inline code and has no predefined model => GenerateMacroModelProperties is reqd.
        //
        // "attributes" contains the macro parameters
        // "pageElements" contains the UmbracoPage.Elements (legacy)
        // "pageId" is the identified of the page being rendered

        // that one is here for backward compatibility but is not used anywhere in core
        [Obsolete("Use ExecuteMacro.", false)]
// ReSharper disable once InconsistentNaming
        public Control renderMacro(Hashtable attributes, Hashtable pageElements, int pageId)
        {
            // assume the macro has been initialized with a proper model so the model
            // properties have been generated properly and we just need to update their
            // value. don't parse them yet, though, as they will be parsed in 
            // ExecuteMacro(). After MacroRendering has triggered - which would mean
            // that pageElements may be changed in between. Dirty.

            UpdateMacroModelProperties(attributes);

            // make sure we return a control (backward compat.)
            var macroContent = ExecuteMacro(pageElements, pageId);
            return macroContent.GetAsControl();
        }

        // that one is here for backward compatibility but is not used anywhere in core
        [Obsolete("Use ExecuteMacro.", false)]
// ReSharper disable once InconsistentNaming
        public Control renderMacro(Hashtable pageElements, int pageId)
        {
            // assume the macro has been initialized with a proper model so model
            // properties have been generated properly, and their values have been
            // updated according to the attributes already.

            // make sure we return a control (backward compat.)
            var macroContent = ExecuteMacro(pageElements, pageId);
            return macroContent.GetAsControl();
        }

        // Still, this is ugly. The macro should have a Content property
        // referring to IPublishedContent we're rendering the macro against,
        // this is all soooo convoluted ;-(

        public MacroContent ExecuteMacro(Hashtable pageElements, int pageId, Hashtable attributes)
        {
            UpdateMacroModelProperties(attributes);
            return ExecuteMacro(pageElements, pageId);
        }

        public MacroContent ExecuteMacro(Hashtable pageElements, int pageId)
        {
            // trigger MacroRendering event so that the model can be manipulated before rendering
            OnMacroRendering(new MacroRenderingEventArgs(pageElements, pageId));

            var macroInfo = (Model.MacroType == MacroTypes.Script && Model.Name.IsNullOrWhiteSpace())
                                ? string.Format("Render Inline Macro, cache: {0}", Model.CacheDuration)
                                : string.Format("Render Macro: {0}, type: {1}, cache: {2}", Model.Name, Model.MacroType, Model.CacheDuration);

            using (DisposableTimer.DebugDuration<macro>(macroInfo, "Rendered Macro."))
            {
                // parse macro parameters ie replace the special [#key], [$key], etc. syntaxes
                foreach (var prop in Model.Properties)
                    prop.Value = helper.parseAttribute(pageElements, prop.Value);

                Model.CacheIdentifier = GetContentCacheIdentifier(Model, pageId);

                // get the macro from cache if it is there
                var macroContent = GetMacroContentFromCache(Model);

                // macroContent.IsEmpty may be true, meaning the macro produces no output,
                // but still can be cached because its execution did not trigger any error.
                // so we need to actually render, only if macroContent is null
                if (macroContent == null)
                {
                    // this will take care of errors
                    // it may throw, if we actually want to throw, so better not
                    // catch anything here and let the exception be thrown
                    var attempt = ExecuteMacroOfType(Model);

                    // by convention ExecuteMacroByType must either throw or return a result
                    // just check to avoid internal errors
                    macroContent = attempt.Result;
                    if (macroContent == null)
                        throw new Exception("Internal error, ExecuteMacroByType returned no content.");

                    // add to cache if render is successful
                    // content may be empty but that's not an issue
                    if (attempt.Success)
                    {
                        // write to cache (if appropriate)
                        AddMacroContentToCache(Model, macroContent);
                    }
                }

                return macroContent;
            }
        }

        /// <summary>
        /// Executes a macro of a given type.
        /// </summary>
        private Attempt<MacroContent> ExecuteMacroWithErrorWrapper(string msgIn, string msgOut, Func<MacroContent> getMacroContent)
        {
            using (DisposableTimer.DebugDuration<macro>(msgIn, msgOut))
            {
                try
                {
                    return Attempt.Succeed(getMacroContent());
                }
                catch (Exception e)
                {
                    Exceptions.Add(e);

                    var errorMessage = "Failed " + msgIn;
                    LogHelper.WarnWithException<macro>(errorMessage, true, e);

                    var macroErrorEventArgs = new MacroErrorEventArgs
                    {
                        Name = Model.Name,
                        Alias = Model.Alias,
                        ItemKey = Model.ScriptName,
                        Exception = e,
                        Behaviour = UmbracoConfig.For.UmbracoSettings().Content.MacroErrorBehaviour
                    };

                    OnError(macroErrorEventArgs);

                    switch (macroErrorEventArgs.Behaviour)
                    {
                        case MacroErrorBehaviour.Inline:
                            // do not throw, eat the exception, display the trace error message
                            return Attempt.Fail(new MacroContent { Text = errorMessage }, e);
                        case MacroErrorBehaviour.Silent:
                            // do not throw, eat the exception, do not display anything
                            return Attempt.Fail(new MacroContent { Text = string.Empty }, e);
                        case MacroErrorBehaviour.Content:
                            // do not throw, eat the exception, display the custom content
                            return Attempt.Fail(new MacroContent { Text = macroErrorEventArgs.Html ?? string.Empty }, e);
                        //case MacroErrorBehaviour.Throw:
                        default:
                            // see http://issues.umbraco.org/issue/U4-497 at the end
                            // throw the original exception
                            throw;
                    }
                }
            }
        }

        /// <summary>
        /// Executes a macro.
        /// </summary>
        /// <remarks>Returns an attempt that is successful if the macro ran successfully. If the macro failed
        /// to run properly, the attempt fails, though it may contain a content. But for instance that content
        /// should not be cached. In that case the attempt may also contain an exception.</remarks>
        private Attempt<MacroContent> ExecuteMacroOfType(MacroModel model)
        {
            // ensure that we are running against a published node (ie available in XML)
            // that may not be the case if the macro is embedded in a RTE of an unpublished document

            if (UmbracoContext.Current.PublishedContentRequest == null
                || UmbracoContext.Current.PublishedContentRequest.HasPublishedContent == false)
                return Attempt.Fail(new MacroContent { Text = "[macro]" });

            //XmlNode node = null;
            //if (HttpContext.Current.Items["pageID"] != null)
            //    node = UmbracoContext.Current.GetXml().SelectSingleNode(string.Format("//* [@isDoc and @id={0}]", HttpContext.Current.Items["pageID"]));
            //if (node == null)
            //    return Attempt.Fail(new MacroContent { Text = "[macro]" });

            switch (model.MacroType)
            {                   
                case MacroTypes.PartialView:
                    return ExecuteMacroWithErrorWrapper(
                        string.Format("Executing PartialView: TypeName=\"{0}\", ScriptName=\"{1}\".", model.TypeName, model.ScriptName),
                        "Executed PartialView.",
                        () =>
                        {
                            var text = ExecutePartialView(model);
                            return new MacroContent { Text = text };
                        });

                case MacroTypes.Script:
                    return ExecuteMacroWithErrorWrapper(
                        "Executing Script: " + (string.IsNullOrWhiteSpace(model.ScriptCode)
                            ? "ScriptName=\"" + model.ScriptName + "\""
                            : "Inline, Language=\"" + model.ScriptLanguage + "\""),
                        "Executed Script.",
                        () =>
                        {
                            var text = ExecuteScript(model);
                            return new MacroContent { Text = text };
                        });

                case MacroTypes.XSLT:
                    return ExecuteMacroWithErrorWrapper(
                        string.Format("Executing Xslt: TypeName=\"{0}\", ScriptName=\"{1}\".", model.TypeName, model.Xslt),
                        "Executed Xslt.",
                        () =>
                        {
                            var text = ExecuteXslt(model);
                            return new MacroContent { Text = text };
                        });

                case MacroTypes.UserControl:
                    return ExecuteMacroWithErrorWrapper(
                        string.Format("Loading UserControl: TypeName=\"{0}\".", model.TypeName),
                        "Loaded UserControl.",
                        () =>
                        {
                            // add tilde for v4 defined macros
                            if (string.IsNullOrEmpty(model.TypeName) == false &&
                                model.TypeName.StartsWith("~") == false)
                                model.TypeName = "~/" + model.TypeName;

                            var control = LoadUserControl(model);
                            return new MacroContent { Control = control };
                        });

                case MacroTypes.CustomControl:
                    return ExecuteMacroWithErrorWrapper(
                        string.Format("Loading CustomControl: TypeName=\"{0}\", TypeAssembly=\"{1}\".", model.TypeName, model.TypeAssembly),
                        "Loaded CustomControl.",
                        () =>
                        {
                            var control = LoadCustomControl(model);
                            return new MacroContent { Control = control };
                        });

                default:
                    return ExecuteMacroWithErrorWrapper(
                        string.Format("Execute macro with unsupported type \"{0}\".", model.MacroType),
                        "Executed.",
                        () =>
                        {
                            throw new Exception("Unsupported macro type.");
                        });
            }
        }

        /// <summary>
        /// Occurs when a macro error is raised.
        /// </summary>
        public static event EventHandler<MacroErrorEventArgs> Error;

        /// <summary>
        /// Raises the <see cref="MacroErrorEventArgs"/> event.
        /// </summary>
        protected void OnError(MacroErrorEventArgs e)
        {
            if (Error != null)
                Error(this, e);
        }

        /// <summary>
        /// Occurs just before the macro is rendered.
        /// </summary>
        /// <remarks>Allows to modify the macro before it actually executes.</remarks>
        public static event TypedEventHandler<macro, MacroRenderingEventArgs> MacroRendering;

        /// <summary>
        /// Raises the <see cref="MacroRendering"/> event.
        /// </summary>
        protected void OnMacroRendering(MacroRenderingEventArgs e)
        {
            if (MacroRendering != null)
                MacroRendering(this, e);
        }

        #endregion

        #region Execute engines: PartialView, MacroScript, Xslt

        /// <summary>
        /// Renders a PartialView Macro.
        /// </summary>
        /// <returns>The text output of the macro execution.</returns>
        private static string ExecutePartialView(MacroModel macro)
        {
            var engine = MacroEngineFactory.GetEngine(PartialViewMacroEngine.EngineName);
            var text = engine.Execute(macro, GetCurrentNode());

            // if the macro engine supports success reporting and executing failed,
            // then report that failure so content is not cached
            var engineWithResultStatus = engine as IMacroEngineResultStatus;
            if (engineWithResultStatus != null && engineWithResultStatus.Success == false)
                throw engineWithResultStatus.ResultException ?? new Exception("PartialView macro engine reported an error.");

            return text;
        }

        /// <summary>
        /// Renders a Script Macro.
        /// </summary>
        /// <returns>The text output of the macro execution.</returns>
        public static string ExecuteScript(MacroModel macro)
        {
            string text;
            IMacroEngine engine;
            if (String.IsNullOrEmpty(macro.ScriptCode) == false)
            {
                engine = MacroEngineFactory.GetByExtension(macro.ScriptLanguage);
                text = engine.Execute(macro, GetCurrentNode());
            }
            else
            {
                var path = IOHelper.MapPath(SystemDirectories.MacroScripts + "/" + macro.ScriptName);
                engine = MacroEngineFactory.GetByFilename(path);
                text = engine.Execute(macro, GetCurrentNode());
            }

            // if the macro engine supports success reporting and executing failed,
            // then report that failure so content is not cached - just throw the exception
            var engineWithResultStatus = engine as IMacroEngineResultStatus;
            if (engineWithResultStatus != null && engineWithResultStatus.Success == false)
                throw engineWithResultStatus.ResultException ?? new Exception("Script macro engine reported an error.");

            return text;
        }

        /// <summary>
        /// Renders an Xslt Macro.
        /// </summary>
        /// <returns>The text output of the macro execution.</returns>
        public static string ExecuteXslt(MacroModel macro)
        {
            var engine = MacroEngineFactory.GetEngine(XsltMacroEngine.EngineName);
            var text = engine.Execute(macro, GetCurrentNode());

            // if the macro engine supports success reporting and executing failed,
            // then report that failure so content is not cached
            var engineWithResultStatus = engine as IMacroEngineResultStatus;
            if (engineWithResultStatus != null && engineWithResultStatus.Success == false)
                throw engineWithResultStatus.ResultException ?? new Exception("Xslt macro engine reported an error.");

            return text;
        }

        #endregion

        #region Execute (ie load) Custom|User Control

        // NOTE - these should move to a proper macro engine
        // but engines want to return a string, not a control!

        /// <summary>
        /// Renders a UserControl Macro.
        /// </summary>
        /// <remarks>Loads and returns the user control, which is not executed. Will be inserted in the page.</remarks>
        private static Control LoadUserControl(MacroModel model)
        {
            var filename = model.TypeName;

            // ensure the file exists
            var path = IOHelper.FindFile(filename);
            if (File.Exists(IOHelper.MapPath(path)) == false)
                throw new UmbracoException(string.Format("Failed to load control, file \"{0}\" does not exist.", path));

            // load the control
            var control = (UserControl)new UserControl().LoadControl(path);
            control.ID = string.IsNullOrEmpty(model.MacroControlIdentifier)
                ? GetControlUniqueId(filename)
                : model.MacroControlIdentifier;

            // initialize the control
            LogHelper.Info<macro>(string.Format("Loaded control \"{0}\" with ID \"{1}\".", filename, control.ID));
            SetControlCurrentNode(control);
            UpdateControlProperties(control, model);

            return control;
        }
        
        /// <summary>
        /// Renders a CustomControl Macro.
        /// </summary>
        /// <remarks>Loads and returns the custom control, which is not executed. Will be inserted in the page.</remarks>
        private static Control LoadCustomControl(MacroModel model)
        {
            var filename = model.TypeAssembly;
            var controlname = model.TypeName;

            // ensure the file exists
            var path = IOHelper.MapPath(SystemDirectories.Bin.EnsureEndsWith('/') + filename);
            if (File.Exists(path) == false)
                throw new Exception(string.Format("Failed to load control, file \"{0}\" does not exist.", path));

            // load the assembly
            var assembly = Assembly.LoadFrom(path);
            LogHelper.Info<macro>(string.Format("Loaded assembly \"{0}\" from file \"{1}\".", assembly.FullName, path));

            // get the type
            var type = assembly.GetType(controlname);
            if (type == null)
                throw new Exception(string.Format("Failed to get type \"{0}\" from assembly \"{1}\".", controlname, assembly.FullName));

            // instanciate
            var control = Activator.CreateInstance(type) as Control;
            if (control == null)
                throw new Exception(string.Format("Failed to create control \"{0}\" from assembly \"{1}\".", controlname, assembly.FullName));

            // fixme - don't we need to set the ID here too? (let's do it)
            control.ID = string.IsNullOrEmpty(model.MacroControlIdentifier)
                ? GetControlUniqueId(filename)
                : model.MacroControlIdentifier;

            // initialize the control
            LogHelper.Info<macro>(string.Format("Loaded control \"{0}\" with ID \"{1}\".", controlname, control.ID));
            SetControlCurrentNode(control);
            UpdateControlProperties(control, model);

            return control;
        }

        // sets the control CurrentNode|currentNode property
        private static void SetControlCurrentNode(Control control)
        {
            var node = GetCurrentNode();
            SetControlCurrentNode(control, "CurrentNode", node);
            SetControlCurrentNode(control, "currentNode", node);
        }

        // sets the control 'propertyName' property, of type INode
        private static void SetControlCurrentNode(Control control, string propertyName, INode node)
        {
            var currentNodeProperty = control.GetType().GetProperty(propertyName);
            if (currentNodeProperty != null && currentNodeProperty.CanWrite &&
                currentNodeProperty.PropertyType.IsAssignableFrom(typeof(INode)))
            {
                currentNodeProperty.SetValue(control, node, null);
            }
        }
        
        // set the control properties according to the model properties ie parameters
        internal static void UpdateControlProperties(Control control, MacroModel model)
        {
            var type = control.GetType();

            foreach (var modelProperty in model.Properties)
            {
                var controlProperty = type.GetProperty(modelProperty.Key);
                if (controlProperty == null)
                {
                    LogHelper.Warn<macro>(string.Format("Control property \"{0}\" doesn't exist or isn't accessible, skip.", modelProperty.Key));
                    continue;
                }

                var tryConvert = modelProperty.Value.TryConvertTo(controlProperty.PropertyType);
                if (tryConvert.Success)
                {
                    try
                    {
                        controlProperty.SetValue(control, tryConvert.Result, null);
                        LogHelper.Debug<macro>(string.Format("Set property \"{0}\" value \"{1}\".", modelProperty.Key, modelProperty.Value));
                    }
                    catch (Exception e)
                    {
                        LogHelper.WarnWithException<macro>(string.Format("Failed to set property \"{0}\" value \"{1}\".", modelProperty.Key, modelProperty.Value), e);
                    }
                }
                else
                {
                    LogHelper.Warn<macro>(string.Format("Failed to set property \"{0}\" value \"{1}\".", modelProperty.Key, modelProperty.Value));
                }
            }
        }

        #endregion

        #region Execute helpers

        private static INode GetCurrentNode()
        {       
            // get the current content request
            
            IPublishedContent content;
            if (UmbracoContext.Current.IsFrontEndUmbracoRequest)
            {
                var request = UmbracoContext.Current.PublishedContentRequest;
                content = (request == null || request.HasPublishedContent == false) ? null : request.PublishedContent;
            }
            else
            {
                var pageId = UmbracoContext.Current.PageId;
                content = pageId.HasValue ? UmbracoContext.Current.ContentCache.GetById(pageId.Value) : null;
            }
                    
            return content == null ? null : LegacyNodeHelper.ConvertToNode(content);
        }

        private static string GetControlUniqueId(string filename)
        {
            const string key = "MacroControlUniqueId";

            // will return zero as the first, uninitialized value
            var x = StateHelper.GetContextValue<int>(key);
            x += 1;
            StateHelper.SetContextValue(key, x);

            return string.Format("{0}_{1}", Path.GetFileNameWithoutExtension(filename), x);
        }

        #endregion

        #region RTE macros

        [Obsolete("Use RenderMacroStartTag.", false)]
// ReSharper disable once InconsistentNaming
        public static string renderMacroStartTag(Hashtable attributes, int pageId, Guid versionId)
        {
            return RenderMacroStartTag(attributes, pageId, versionId);
        }

        public static string RenderMacroStartTag(Hashtable attributes, int pageId, Guid versionId)
        {
            var div = "<div ";

            var ide = attributes.GetEnumerator();
            while (ide.MoveNext())
            {
                div += string.Format("umb_{0}=\"{1}\" ", ide.Key, EncodeMacroAttribute((ide.Value ?? string.Empty).ToString()));
            }

            div += "ismacro=\"true\" onresizestart=\"return false;\" umbVersionId=\"" + versionId +
                   "\" umbPageid=\"" +
                   pageId +
                   "\" title=\"This is rendered content from macro\" class=\"umbMacroHolder\"><!-- startUmbMacro -->";

            return div;
        }

        private static string EncodeMacroAttribute(string attributeContents)
        {
            // Replace linebreaks
            attributeContents = attributeContents.Replace("\n", "\\n").Replace("\r", "\\r");

            // Replace quotes
            attributeContents =
                attributeContents.Replace("\"", "&quot;");

            // Replace tag start/ends
            attributeContents =
                attributeContents.Replace("<", "&lt;").Replace(">", "&gt;");

            return attributeContents;
        }

        [Obsolete("Use RenderMacroEndTag.", false)]
// ReSharper disable once InconsistentNaming
        public static string renderMacroEndTag()
        {
            return RenderMacroEndTag();
        }

        public static string RenderMacroEndTag()
        {
            return "<!-- endUmbMacro --></div>";
        }

        private static readonly Regex HrefRegex = new Regex("href=\"([^\"]*)\"",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);

        public static string GetRenderedMacro(int macroId, page umbPage, Hashtable attributes, int pageId)
        {
            var macro = GetMacro(macroId);
            if (macro == null) return string.Empty;

            var macroContent = macro.ExecuteMacro(umbPage.Elements, pageId);

            // get as text, will render the control if any
            var text = macroContent.GetAsText();

            // remove hrefs
            text = HrefRegex.Replace(text, match => "href=\"javascript:void(0)\"");

            return text;
        }

        public static string MacroContentByHttp(int pageId, Guid pageVersion, Hashtable attributes)
        {

            if (SystemUtilities.GetCurrentTrustLevel() != AspNetHostingPermissionLevel.Unrestricted)
            {
                return "<span style='color: red'>Cannot render macro content in the rich text editor when the application is running in a Partial Trust environment</span>";
            }
            
            var tempAlias = attributes["macroalias"] != null
                ? attributes["macroalias"].ToString()
                : attributes["macroAlias"].ToString();

            var macro = GetMacro(tempAlias);
            if (macro.Model.RenderInEditor == false)
                return ShowNoMacroContent(macro);

            var querystring = "umbPageId=" + pageId + "&umbVersionId=" + pageVersion;
            var ide = attributes.GetEnumerator();
            while (ide.MoveNext())
                querystring += "&umb_" + ide.Key + "=" + HttpContext.Current.Server.UrlEncode((ide.Value ?? string.Empty).ToString());

            // Create a new 'HttpWebRequest' Object to the mentioned URL.
            var protocol = GlobalSettings.UseSSL ? "https" : "http";
            var url = string.Format("{0}://{1}:{2}{3}/macroResultWrapper.aspx?{4}", protocol,
                HttpContext.Current.Request.ServerVariables["SERVER_NAME"],
                HttpContext.Current.Request.ServerVariables["SERVER_PORT"],
                IOHelper.ResolveUrl(SystemDirectories.Umbraco), querystring);

            var myHttpWebRequest = (HttpWebRequest)WebRequest.Create(url);

            // allows for validation of SSL conversations (to bypass SSL errors in debug mode!)
            ServicePointManager.ServerCertificateValidationCallback += ValidateRemoteCertificate;

            // propagate the user's context
            var inCookie = StateHelper.Cookies.UserContext.RequestCookie;
            var cookie = new Cookie(inCookie.Name, inCookie.Value, inCookie.Path,
                HttpContext.Current.Request.ServerVariables["SERVER_NAME"]);
            myHttpWebRequest.CookieContainer = new CookieContainer();
            myHttpWebRequest.CookieContainer.Add(cookie);

            // Assign the response object of 'HttpWebRequest' to a 'HttpWebResponse' variable.
            HttpWebResponse myHttpWebResponse = null;
            var text = string.Empty;
            try
            {
                myHttpWebResponse = (HttpWebResponse)myHttpWebRequest.GetResponse();
                if (myHttpWebResponse.StatusCode == HttpStatusCode.OK)
                {
                    var streamResponse = myHttpWebResponse.GetResponseStream();
                    if (streamResponse == null)
                        throw new Exception("Internal error, no response stream.");
                    var streamRead = new StreamReader(streamResponse);
                    var readBuff = new Char[256];
                    var count = streamRead.Read(readBuff, 0, 256);
                    while (count > 0)
                    {
                        var outputData = new String(readBuff, 0, count);
                        text += outputData;
                        count = streamRead.Read(readBuff, 0, 256);
                    }
                    // Close the Stream object.
                    streamResponse.Close();
                    streamRead.Close();

                    // Find the content of a form
                    const string grabStart = "<!-- grab start -->";
                    const string grabEnd = "<!-- grab end -->";

                    var grabStartPos = text.InvariantIndexOf(grabStart) + grabStart.Length;
                    var grabEndPos = text.InvariantIndexOf(grabEnd) - grabStartPos;
                    text = text.Substring(grabStartPos, grabEndPos);
                }
                else
                    text = ShowNoMacroContent(macro);

                // Release the HttpWebResponse Resource.
                myHttpWebResponse.Close();
            }
            catch (Exception)
            {
                text = ShowNoMacroContent(macro);
            }
            finally
            {
                // Release the HttpWebResponse Resource.
                if (myHttpWebResponse != null)
                    myHttpWebResponse.Close();
            }

            return text.Replace("\n", string.Empty).Replace("\r", string.Empty);
        }

        private static string ShowNoMacroContent(macro currentMacro)
        {
            return "<span style=\"color: green\"><strong>" + currentMacro.Model.Name +
                   "</strong><br />No macro content available for WYSIWYG editing</span>";
        }

        private static bool ValidateRemoteCertificate(
            object sender,
            X509Certificate certificate,
            X509Chain chain,
            SslPolicyErrors policyErrors
            )
        {
            // allow any old dodgy certificate in debug mode
            return GlobalSettings.DebugMode || policyErrors == SslPolicyErrors.None;
        }

        #endregion
    }

    /// <summary>
    /// Event arguments used for the MacroRendering event
    /// </summary>
    public class MacroRenderingEventArgs : EventArgs
    {
        public Hashtable PageElements { get; private set; }
        public int PageId { get; private set; }

        public MacroRenderingEventArgs(Hashtable pageElements, int pageId)
        {
            PageElements = pageElements;
            PageId = pageId;
        }
    }

}