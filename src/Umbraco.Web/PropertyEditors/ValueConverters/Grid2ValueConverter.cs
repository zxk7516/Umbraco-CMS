using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Umbraco.Core;
using Umbraco.Core.Composing;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Core.PropertyEditors;
using Umbraco.Core.PropertyEditors.ValueConverters;
using Umbraco.Core.Services;
using Umbraco.Web.PublishedCache;

namespace Umbraco.Web.PropertyEditors.ValueConverters
{
    public class Grid2ValueConverter : PropertyValueConverterBase
    {
        private readonly IPublishedSnapshotAccessor _publishedSnapshotAccessor;
        private readonly IEntityService _entityService;
        private readonly IDataTypeService _dataTypeService;
        private readonly IPublishedModelFactory _publishedModelFactory;

        public Grid2ValueConverter(
            IPublishedSnapshotAccessor publishedSnapshotAccessor,
            IPublishedModelFactory publishedModelFactory,
            IEntityService entityService,
            IDataTypeService dataTypeService)
        {
            _publishedSnapshotAccessor = publishedSnapshotAccessor;
            _publishedModelFactory = publishedModelFactory;
            _entityService = entityService;
            _dataTypeService = dataTypeService;
        }

        public override bool IsConverter(PublishedPropertyType propertyType)
            => propertyType.EditorAlias.InvariantEquals(Constants.PropertyEditors.Aliases.Grid2);

        public override Type GetPropertyValueType(PublishedPropertyType propertyType)
            => typeof(Grid2Value);

        public override PropertyCacheLevel GetPropertyCacheLevel(PublishedPropertyType propertyType)
            => PropertyCacheLevel.Element;

        public override object ConvertIntermediateToObject(IPublishedElement owner, PublishedPropertyType propertyType, PropertyCacheLevel referenceCacheLevel, object inter, bool preview)
        {
            if (inter == null)
                return default(Grid2Value);

            var source = inter.ToString();
            if (source.DetectIsJson() == false)
                return default(Grid2Value);

            try
            {
                var obj = JsonConvert.DeserializeObject<JObject>(source);

                var dataType = _dataTypeService.GetDataType(propertyType.DataType.Id);
                var config = dataType.ConfigurationAs<Grid2Configuration>().Items;

                var value = new Grid2Value();
                value.Columns = config["columns"].ToObject<int>();

                var layoutConfigs = GetArray(config, "layouts")
                    .ToDictionary(x => x["name"].ToString(), x => x.ToObject<JObject>());

                var rows = new List<Grid2Row>();
                var rawRows = GetArray(obj, "rows");
                for (var r = 0; r < rawRows.Count; r++)
                {
                    var rawRow = rawRows[r].ToObject<JObject>();
                    var rowConfig = layoutConfigs[rawRow["alias"].ToString()];

                    var row = new Grid2Row();
                    row.Alias = rawRow["alias"].ToString();
                    row.Name = rowConfig["name"].ToString();

                    var rowSettingsValue = rawRow["settings"]?.ToObject<JObject>();
                    if (rowSettingsValue != null)
                    {
                        row.Settings = ConvertToElement(rowSettingsValue, referenceCacheLevel, preview);
                    }

                    var cells = new List<Grid2Cell>();
                    var areaConfigs = GetArray(rowConfig, "areas");
                    var rawCells = GetArray(rawRow, "cells");
                    for (var c = 0; c < rawCells.Count; c++)
                    {
                        var rawCell = rawCells[c].ToObject<JObject>();
                        var cellConfig = areaConfigs[c].ToObject<JObject>();

                        var cell = new Grid2Cell();
                        cell.Colspan = cellConfig["grid"].ToObject<int>();

                        var cellSettingsValue = rawCell["settings"]?.ToObject<JObject>();
                        if (cellSettingsValue != null)
                        {
                            cell.Settings = ConvertToElement(cellSettingsValue, referenceCacheLevel, preview);
                        }

                        var items = new List<IPublishedElement>();
                        var rawItems = GetArray(rawCell, "items");
                        foreach (var rawItem in rawItems.Cast<JObject>())
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
                Current.Logger.Error<GridValueConverter>($"Could not parse the string '{source}' to a JSON object.", ex);
            }

            // if we get here, then the value wasn't able to be processed,
            // there's nothing we can do with it, so we return a default Grid value object.
            return default(Grid2Value);
        }

        private JArray GetArray(JObject obj, string propertyName)
        {
            if (obj.TryGetValue(propertyName, out JToken token))
            {
                var asArray = token as JArray;
                return asArray ?? new JArray();
            }

            return new JArray();
        }

        protected IPublishedElement ConvertToElement(JObject sourceObject, PropertyCacheLevel referenceCacheLevel, bool preview)
        {
            if (sourceObject.TryGetValue("type", out var elementType) == false || Udi.TryParse(elementType.ToString(), out var elementTypeUdi) == false)
                return null;

            var elementTypeId = _entityService.GetId(elementTypeUdi);
            if (elementTypeId.Success == false)
                return null;

            var publishedContentType = _publishedSnapshotAccessor.PublishedSnapshot.Content.GetContentType(elementTypeId.Result);
            if (publishedContentType == null)
                return null;

            var propertyValues = sourceObject["values"].ToObject<Dictionary<string, object>>();

            // TODO: Review how we can give each editor a unique GUID key
            if (propertyValues.TryGetValue("key", out var keyo) == false || Guid.TryParse(keyo.ToString(), out var key) == false)
                key = Guid.NewGuid();

            IPublishedElement element = new PublishedElement(publishedContentType, key, propertyValues, preview, referenceCacheLevel, _publishedSnapshotAccessor);
            return _publishedModelFactory.CreateModel(element);
        }
    }
}
