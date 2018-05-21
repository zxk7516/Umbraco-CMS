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
            => PropertyCacheLevel.None;

        public override object ConvertIntermediateToObject(IPublishedElement owner, PublishedPropertyType propertyType, PropertyCacheLevel referenceCacheLevel, object inter, bool preview)
        {
            if (inter == null) return null;
            var sourceString = inter.ToString();

            if (sourceString.DetectIsJson())
            {
                try
                {
                    sourceString = @"{
     'rows': [
        {
            'alias': 'fullwidth',
            'settings': {
                'type': 'umb://document-type/740d3802-ffe6-446d-8f03-288e22ba3e17',
                'values': {
                    'classNames': 'fancy row',
                    'bgColor': '000000'
                }
            },
            'cells': [
                {
                    'settings': {
                        'type': 'umb://document-type/740d3802-ffe6-446d-8f03-288e22ba3e17',
                        'values': {
                            'classNames': 'fancy row',
                            'bgColor': '000000'
                        }
                    },
                    'items': [
                        {
                            'type': 'umb://document-type/7efe7d7c-a627-4367-bf03-aa64b4d4ec8f',
                            'values': {
                                'headline': 'Welcome to the fantastic site'
                            }
                        }
                    ]
                }
            ]
        },
        {
            'alias': 'twocol',
            'settings': {
                'type': 'umb://document-type/740d3802-ffe6-446d-8f03-288e22ba3e17',
                'values': {
                    'classNames': 'fancy row',
                    'bgColor': '000000'
                }
            },
            'cells': [
                {
                    'settings': {
                        'type': 'umb://document-type/740d3802-ffe6-446d-8f03-288e22ba3e17',
                        'values': {
                            'classNames': 'fancy row',
                            'bgColor': '000000'
                        }
                    },
                    'items': [
                        {
                            'type': 'umb://document-type/7efe7d7c-a627-4367-bf03-aa64b4d4ec8f',
                            'values': {
                                'headline': 'Welcome to the fantastic site'
                            }
                        }
                    ]
                },
                {
                    'settings': {
                        'type': 'umb://document-type/740d3802-ffe6-446d-8f03-288e22ba3e17',
                        'values': {
                            'classNames': 'fancy row',
                            'bgColor': '000000'
                        }
                    },
                    'items': [
                        {
                            'type': 'umb://document-type/d57b379d-d993-47b0-b05f-1ab69acd83df',
                            'values': {
                                'rte': 'Some rich text'
                            }
                        }
                    ]
                }
            ]
        }
    ]
}";

                    var configString = @"{
   'columns': 12,
    'rows': [
        {
            'alias': 'fullwidth',
            'name': 'Full Width',
            'settingsType': 'umb://document-type/740d3802-ffe6-446d-8f03-288e22ba3e17',
            'cells': [
                {
                    'colspan': 12,
                    'settingsType': 'umb://document-type/740d3802-ffe6-446d-8f03-288e22ba3e17',
                    'allowAll': false,
                    'allowed': [
                        'umb://document-type/7efe7d7c-a627-4367-bf03-aa64b4d4ec8f',
                        'umb://document-type/d57b379d-d993-47b0-b05f-1ab69acd83df'
                    ]
                }
            ]
        },
        {
            'alias': 'twocol',
            'name': 'Two Column',
            'settingsType': 'umb://document-type/740d3802-ffe6-446d-8f03-288e22ba3e17',
            'cells': [
                {
                    'colspan': 6,
                    'settingsType': 'umb://document-type/740d3802-ffe6-446d-8f03-288e22ba3e17',
                    'allowAll': false,
                    'allowed': [
                        'umb://document-type/7efe7d7c-a627-4367-bf03-aa64b4d4ec8f',
                        'umb://document-type/d57b379d-d993-47b0-b05f-1ab69acd83df'
                    ]
                },
                {
                    'colspan': 6,
                    'settingsType': 'umb://document-type/740d3802-ffe6-446d-8f03-288e22ba3e17',
                    'allowAll': false,
                    'allowed': [
                        'umb://document-type/7efe7d7c-a627-4367-bf03-aa64b4d4ec8f',
                        'umb://document-type/d57b379d-d993-47b0-b05f-1ab69acd83df'
                    ]
                }
            ]
        }
    ]
}";

                    var obj = JsonConvert.DeserializeObject<JObject>(sourceString);

                    var dataType = _dataTypeService.GetDataType(propertyType.DataType.Id);
                    var config = dataType.ConfigurationAs<Grid2Configuration>().Items;

                    // TEMP
                    config = JsonConvert.DeserializeObject<JObject>(configString);

                    var value = new Grid2Value();
                    value.Columns = config["columns"].ToObject<int>();
                    
                    var rowConfigs = GetArray(config, "rows")
                        .ToDictionary(x => x["alias"].ToString(), x => x.ToObject<JObject>());

                    var rows = new List<Grid2Row>();
                    var rawRows = GetArray(obj, "rows");
                    for (var r = 0; r < rawRows.Count; r++)
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
                        for(var c = 0; c < rawCells.Count; c++)
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

            // TOFIX each editor needs to be given a unique GUID key
            if (!propertyValues.TryGetValue("key", out var keyo)
                || !Guid.TryParse(keyo.ToString(), out var key))
                key = Guid.NewGuid();

            IPublishedElement element = new PublishedElement(publishedContentType, key, propertyValues, preview, referenceCacheLevel, _publishedSnapshotAccessor);
            element = PublishedModelFactory.CreateModel(element);
            return element;
        }
    }
}
