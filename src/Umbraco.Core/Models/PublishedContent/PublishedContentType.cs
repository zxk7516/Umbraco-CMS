using System;
using System.Collections.Generic;
using System.Linq;

namespace Umbraco.Core.Models.PublishedContent
{
    /// <summary>
    /// Represents an <see cref="IPublishedContent"/> type.
    /// </summary>
    /// <remarks>Instances of the <see cref="PublishedContentType"/> class are immutable, ie
    /// if the content type changes, then a new class needs to be created.</remarks>
    public class PublishedContentType
    {
        private readonly PublishedPropertyType[] _propertyTypes;

        // fast alias-to-index xref containing both the raw alias and its lowercase version
        private readonly Dictionary<string, int> _indexes = new Dictionary<string, int>();

        // internal so it can be used by PublishedNoCache which does _not_ want to cache anything and so will never
        // use the static cache getter PublishedContentType.GetPublishedContentType(alias) below - anything else
        // should use it.
        internal PublishedContentType(IContentType contentType)
            : this(PublishedItemType.Content, contentType)
        { }

        internal PublishedContentType(IMediaType mediaType)
            : this(PublishedItemType.Media, mediaType)
        { }

        internal PublishedContentType(IMemberType memberType)
            : this(PublishedItemType.Member, memberType)
        { }

        internal PublishedContentType(PublishedItemType itemType, IContentTypeComposition contentType)
        {
            Id = contentType.Id;
            Alias = contentType.Alias;
            ItemType = itemType;
            var propertyTypes = contentType.CompositionPropertyTypes
                .Select(x => new PublishedPropertyType(this, x));
            if (itemType == PublishedItemType.Member)
                propertyTypes = WithMemberProperties(propertyTypes, this);
            _propertyTypes = propertyTypes.ToArray();
            InitializeIndexes();
        }

        // internal so it can be used for unit tests
        internal PublishedContentType(int id, string alias, IEnumerable<PublishedPropertyType> propertyTypes)
            : this(id, alias, PublishedItemType.Content, propertyTypes)
        { }

        // internal so it can be used for unit tests
        internal PublishedContentType(int id, string alias, PublishedItemType itemType, IEnumerable<PublishedPropertyType> propertyTypes)
        {
            Id = id;
            Alias = alias;
            ItemType = itemType;
            if (itemType == PublishedItemType.Member)
                propertyTypes = WithMemberProperties(propertyTypes);
            _propertyTypes = propertyTypes.ToArray();
            foreach (var propertyType in _propertyTypes)
                propertyType.ContentType = this;
            InitializeIndexes();
        }

        // create detached content type - ie does not match anything in the DB
        internal PublishedContentType(string alias, IEnumerable<PublishedPropertyType> propertyTypes)
            : this (0, alias, propertyTypes)
        { }

        private void InitializeIndexes()
        {
            for (var i = 0; i < _propertyTypes.Length; i++)
            {
                var propertyType = _propertyTypes[i];
                _indexes[propertyType.PropertyTypeAlias] = i;
                _indexes[propertyType.PropertyTypeAlias.ToLowerInvariant()] = i;
            }
        }

        // NOTE: code below defines and add custom, built-in, Umbraco properties for members
        //  unless they are already user-defined in the content type, then they are skipped

        private const int TextboxDataTypeDefinitionId = -88;
        //private const int BooleanDataTypeDefinitionId = -49;
        //private const int DatetimeDataTypeDefinitionId = -36;

        private const string TextboxEditorAlias = Constants.PropertyEditors.TextboxAlias;
        //private const string BooleanEditorAlias = Constants.PropertyEditors.TrueFalseAlias;
        //private const string DatetimeEditorAlias = Constants.PropertyEditors.DateTimeAlias;

        static readonly Dictionary<string, Tuple<int, string>> BuiltinProperties = new Dictionary<string, Tuple<int, string>>
        {
            { "Email", Tuple.Create(TextboxDataTypeDefinitionId, TextboxEditorAlias) },
            { "Username", Tuple.Create(TextboxDataTypeDefinitionId, TextboxEditorAlias) },
            //{ "PasswordQuestion", Tuple.Create(TextboxDataTypeDefinitionId, TextboxEditorAlias) },
            //{ "Comments", Tuple.Create(TextboxDataTypeDefinitionId, TextboxEditorAlias) },
            //{ "IsApproved", Tuple.Create(BooleanDataTypeDefinitionId, BooleanEditorAlias) },
            //{ "IsLockedOut", Tuple.Create(BooleanDataTypeDefinitionId, BooleanEditorAlias) },
            //{ "LastLockoutDate", Tuple.Create(DatetimeDataTypeDefinitionId, DatetimeEditorAlias) },
            //{ "CreateDate", Tuple.Create(DatetimeDataTypeDefinitionId, DatetimeEditorAlias) },
            //{ "LastLoginDate", Tuple.Create(DatetimeDataTypeDefinitionId, DatetimeEditorAlias) },
            //{ "LastPasswordChangeDate", Tuple.Create(DatetimeDataTypeDefinitionId, DatetimeEditorAlias) },
        };

        private static IEnumerable<PublishedPropertyType> WithMemberProperties(IEnumerable<PublishedPropertyType> propertyTypes, 
            PublishedContentType contentType = null)
        {
            var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var propertyType in propertyTypes)
            {
                aliases.Add(propertyType.PropertyTypeAlias);
                yield return propertyType;
            }

            foreach (var kvp in BuiltinProperties.Where(kvp => aliases.Contains(kvp.Key) == false))
            {
                var propertyType = new PublishedPropertyType(kvp.Key, kvp.Value.Item1, kvp.Value.Item2, true);
                if (contentType != null) propertyType.ContentType = contentType;
                yield return propertyType;
            }
        }

        #region Content type

        public int Id { get; private set; }
        public string Alias { get; private set; }
        public PublishedItemType ItemType { get; private set; }

        #endregion

        #region Properties

        public IEnumerable<PublishedPropertyType> PropertyTypes
        {
            get { return _propertyTypes; }
        }

        // alias is case-insensitive
        // this is the ONLY place where we compare ALIASES!
        public int GetPropertyIndex(string alias)
        {
            int index;
            if (_indexes.TryGetValue(alias, out index)) return index; // fastest
            if (_indexes.TryGetValue(alias.ToLowerInvariant(), out index)) return index; // slower
            return -1;
        }

        // virtual for unit tests
        public virtual PublishedPropertyType GetPropertyType(string alias)
        {
            var index = GetPropertyIndex(alias);
            return GetPropertyType(index);
        }

        // virtual for unit tests
        public virtual PublishedPropertyType GetPropertyType(int index)
        {
            return index >= 0 && index < _propertyTypes.Length ? _propertyTypes[index] : null;
        }

        #endregion
    }
}
