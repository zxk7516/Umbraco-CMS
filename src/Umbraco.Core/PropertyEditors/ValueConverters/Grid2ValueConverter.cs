using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Umbraco.Core.Configuration;
using Umbraco.Core.Composing;
using Umbraco.Core.IO;
using Umbraco.Core.Logging;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Core.Models;
using Umbraco.Core.Configuration.Grid;

namespace Umbraco.Core.PropertyEditors.ValueConverters
{
    /// <summary>
    /// This ensures that the grid config is merged in with the front-end value
    /// </summary>
    [DefaultPropertyValueConverter(typeof(JsonValueConverter))] //this shadows the JsonValueConverter
    public class Grid2ValueConverter : JsonValueConverter
    {
        public Grid2ValueConverter(PropertyEditorCollection propertyEditors)
            : base(propertyEditors)
        { }

        public override bool IsConverter(PublishedPropertyType propertyType) => propertyType.EditorAlias.InvariantEquals(Constants.PropertyEditors.Aliases.Grid2);

        public override Type GetPropertyValueType(PublishedPropertyType propertyType) => typeof(GridValue);

        public override PropertyCacheLevel GetPropertyCacheLevel(PublishedPropertyType propertyType) => PropertyCacheLevel.Element;

        // TODO: Not sure whether this should be  `ConvertSourceToIntermediate` or `ConvertIntermediateToObject` [LK:2018-05-20]
        public override object ConvertSourceToIntermediate(IPublishedElement owner, PublishedPropertyType propertyType, object source, bool preview)
        {
            if (source == null)
                return null;

            var sourceString = source.ToString();

            if (sourceString.DetectIsJson())
            {
                try
                {
                    var grid = JsonConvert.DeserializeObject<GridValue>(sourceString);

                    // so we have the grid json... we need to merge in the grid's configuration values with the values
                    // we've saved in the database so that when the front end gets this value, it is up-to-date.

                    //TODO: Change all singleton access to use ctor injection in v8!!!
                    //TODO: That would mean that property value converters would need to be request lifespan, hrm....
                    var gridConfig = UmbracoConfig.For.GridConfig(
                        Current.ProfilingLogger.Logger,
                        Current.ApplicationCache.RuntimeCache,
                        new DirectoryInfo(IOHelper.MapPath(SystemDirectories.AppPlugins)),
                        new DirectoryInfo(IOHelper.MapPath(SystemDirectories.Config)),
                        HttpContext.Current.IsDebuggingEnabled);

                    foreach (var section in grid.Sections)
                    {
                        foreach (var row in section.Rows)
                        {
                            foreach (var area in row.Areas)
                            {
                                foreach (var control in area.Controls)
                                {
                                    if (control.Editor != null)
                                    {
                                        var alias = control.Editor.Alias;
                                        if (alias.IsNullOrWhiteSpace() == false)
                                        {
                                            //find the alias in config
                                            // TODO: load up the editor config into a dictionary for faster lookup, rather than a FirstOrDefault per each iteration. [LK:2018-05-20]
                                            var found = gridConfig.EditorsConfig.Editors.FirstOrDefault(x => x.Alias == alias);
                                            if (found != null)
                                            {
                                                // TODO: There might be some fancy AutoMapper thing we could use here? [LK:2018-05-20]
                                                //add/replace the editor value with the one from config
                                                control.Editor.Alias = found.Alias;
                                                control.Editor.Name = found.Name;
                                                control.Editor.View = found.View;
                                                control.Editor.Render = found.Render;
                                                control.Editor.Icon = found.Icon;
                                                control.Editor.Config = found.Config;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    return grid;
                }
                catch (Exception ex)
                {
                    Current.Logger.Error<GridValueConverter>($"Could not parse the string {sourceString} to a JSON object", ex);
                }
            }

            //it's not json, just return the string
            return sourceString;
        }
    }
}
