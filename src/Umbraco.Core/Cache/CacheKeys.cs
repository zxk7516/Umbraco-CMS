using System;

namespace Umbraco.Core.Cache
{

    /// <summary>
    /// Constants storing cache keys used in caching
    /// </summary>
    public static class CacheKeys
    {
        public const string ApplicationTreeCacheKey = "ApplicationTreeCache";
        public const string ApplicationsCacheKey = "ApplicationCache";

        [Obsolete("This is no longer used and will be removed from the codebase in the future")]
        public const string UserTypeCacheKey = "UserTypeCache";

        public const string ContentItemCacheKey = "contentItem";

        public const string MediaCacheKey = "UL_GetMedia";

        public const string MacroXsltCacheKey = "macroXslt_"; // for macro XSLT transforms
        public const string MacroCacheKey = "UmbracoMacroCache"; // for Macro objects
        public const string MacroContentCacheKey = "macroContent_"; // for macro contents

        public const string MemberLibraryCacheKey = "UL_GetMember";
        public const string MemberBusinessLogicCacheKey = "MemberCacheItem_";
        
        public const string TemplateFrontEndCacheKey = "template";

        [Obsolete("This is no longer used and will be removed from the codebase in the future")]
        public const string TemplateBusinessLogicCacheKey = "UmbracoTemplateCache";

        public const string UserContextCacheKey = "UmbracoUserContext";
        public const string UserContextTimeoutCacheKey = "UmbracoUserContextTimeout";

        [Obsolete("This is no longer used and will be removed from the codebase in the future")]
        public const string UserCacheKey = "UmbracoUser";
        
        public const string UserPermissionsCacheKey = "UmbracoUserPermissions";

        public const string ContentTypeCacheKey = "UmbracoContentType";

        public const string ContentTypePropertiesCacheKey = "ContentType_PropertyTypes_Content:";
        
        public const string PropertyTypeCacheKey = "UmbracoPropertyTypeCache";

        [Obsolete("This is no longer used and will be removed from the codebase in the future")]
        public const string LanguageCacheKey = "UmbracoLanguageCache";

        [Obsolete("This is no longer used and will be removed from the codebase in the future")]
        public const string DomainCacheKey = "UmbracoDomainList";

        [Obsolete("This is no longer used and will be removed from the codebase in the future")]
        public const string StylesheetCacheKey = "UmbracoStylesheet";
        [Obsolete("This is no longer used and will be removed from the codebase in the future")]
        public const string StylesheetPropertyCacheKey = "UmbracoStylesheetProperty";

        public const string DataTypeCacheKey = "UmbracoDataTypeDefinition";
        public const string DataTypePreValuesCacheKey = "UmbracoPreVal";
    }
}