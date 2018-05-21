using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Umbraco.Core.Composing;
using Umbraco.Core.Logging;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Core.Models;
using System.Collections.Generic;
using Umbraco.Web.PublishedCache;
using Umbraco.Core.PropertyEditors;
using Umbraco.Core.PropertyEditors.ValueConverters;
using Umbraco.Core;
using Umbraco.Core.Services;

namespace Umbraco.Web.PropertyEditors.ValueConverters
{
    /// <summary>
    /// This ensures that the grid config is merged in with the front-end value
    /// </summary>
    [DefaultPropertyValueConverter(typeof(JsonValueConverter))] //this shadows the JsonValueConverter
    public partial class Grid2ValueConverter : PropertyValueConverterBase
    {
        private readonly IPublishedSnapshotAccessor _publishedSnapshotAccessor;
        private readonly IEntityService _entityService;
        private readonly IDataTypeService _dataTypeService;

        public Grid2ValueConverter(IPublishedSnapshotAccessor publishedSnapshotAccessor,
            IPublishedModelFactory publishedModelFactory,
            IEntityService entityService, IDataTypeService dataTypeService)
        {
            _publishedSnapshotAccessor = publishedSnapshotAccessor;
            _entityService = entityService;
            _dataTypeService = dataTypeService;

            PublishedModelFactory = publishedModelFactory;
        }

        protected IPublishedModelFactory PublishedModelFactory { get; }

        public override bool IsConverter(PublishedPropertyType propertyType)
            => propertyType.EditorAlias.InvariantEquals(Constants.PropertyEditors.Aliases.Grid2);

        public override Type GetPropertyValueType(PublishedPropertyType propertyType)
            => typeof (Grid2Value);

        public override PropertyCacheLevel GetPropertyCacheLevel(PublishedPropertyType propertyType)
            => PropertyCacheLevel.Element;

        public override object ConvertIntermediateToObject(IPublishedElement owner, PublishedPropertyType propertyType, PropertyCacheLevel referenceCacheLevel, object inter, bool preview)
        {
            if (inter == null) return null;
            var sourceString = inter.ToString();

            if (sourceString.DetectIsJson())
            {
                try
                {
                    var obj = JsonConvert.DeserializeObject<JObject>(sourceString);

                    var dataType = _dataTypeService.GetDataType(propertyType.DataType.Id);
                    var config = dataType.ConfigurationAs<Grid2Configuration>();

                    var value = new Grid2Value();
                    value.Columns = config.Items["columns"].ToObject<int>();
                    
                    var rowConfigs = GetArray(config.Items, "rows")
                        .ToDictionary(x => x["alias"].ToString(), x => x.ToObject<JObject>());

                    var rows = new List<Grid2Row>();
                    var rawRows = GetArray(obj, "rows");
                    for (var r = 0; r <= rawRows.Count; r++)
                    {
                        var rawRow = rawRows[r].ToObject<JObject>();
                        var rowConfig = rowConfigs[rawRow["alias"].ToString()];

                        var row = new Grid2Row();
                        row.Alias = rawRow["alias"].ToString();
                        row.Name = rowConfig["name"].ToString();

                        var rowSettingsValue = rawRow["settings"]?.ToObject<JObject>();
                        if (rowSettingsValue != null)
                        {
                            row.Settings = ConvertToElement(rowSettingsValue, referenceCacheLevel, preview);
                        }

                        var cells = new List<Grid2Cell>();
                        var cellConfigs = GetArray(rowConfig, "cells");
                        var rawCells = GetArray(rawRow, "cells");
                        for(var c = 0; c <= rawCells.Count; c++)
                        {
                            var rawCell = rawCells[c].ToObject<JObject>();
                            var cellConfig = cellConfigs[c].ToObject<JObject>();

                            var cell = new Grid2Cell();
                            cell.Colspan = cellConfig["colspan"].ToObject<int>();

                            var cellSettingsValue = rawCell["settings"]?.ToObject<JObject>();
                            if (cellSettingsValue != null)
                            {
                                cell.Settings = ConvertToElement(cellSettingsValue, referenceCacheLevel, preview);
                            }

                            var items = new List<IPublishedElement>();
                            var rawItems = GetArray(rawCell, "items");
                            foreach(var rawItem in rawItems.Cast<JObject>())
                            {
                                items.Add(ConvertToElement(rawItem, referenceCacheLevel, preview));    
                            }

                            cell.Items = items;
                            cells.Add(cell);
                        }

                        row.Cells = cells;
                        rows.Add(row);
                    }

                    value.Rows = rows;
                    return value;
                }
                catch (Exception ex)
                {
                    Current.Logger.Error<GridValueConverter>("Could not parse the string " + sourceString + " to a json object", ex);
                }
            }

            //it's not json, just return the string
            return sourceString;
        }

        private JArray GetArray(JObject obj, string propertyName)
        {
            JToken token;
            if (obj.TryGetValue(propertyName, out token))
            {
                var asArray = token as JArray;
                return asArray ?? new JArray();
            }
            return new JArray();
        }

        protected IPublishedElement ConvertToElement(JObject sourceObject, PropertyCacheLevel referenceCacheLevel, bool preview)
        {
            //var elementType = sourceObject["type"]?.ToObject<string>();
            //if (string.IsNullOrEmpty(elementType))
            //return null;

            if (!sourceObject.TryGetValue("type", out var elementType)
                || !Udi.TryParse(elementType.ToString(), out var elementTypeUdi))
                return null;

            var elementTypeId = _entityService.GetId(elementTypeUdi);
            if (!elementTypeId.Success)
                return null;

            var publishedContentType = _publishedSnapshotAccessor.PublishedSnapshot.Content.GetContentType(elementTypeId.Result);
            if (publishedContentType == null)
                return null;

            var propertyValues = sourceObject["values"].ToObject<Dictionary<string, object>>();

            if (!propertyValues.TryGetValue("key", out var keyo)
                || !Guid.TryParse(keyo.ToString(), out var key))
                key = Guid.Empty;

            IPublishedElement element = new PublishedElement(publishedContentType, key, propertyValues, preview, referenceCacheLevel, _publishedSnapshotAccessor);
            element = PublishedModelFactory.CreateModel(element);
            return element;
        }
    }
}
