using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core.Services;

namespace Umbraco.Core
{
    internal static class EnumExtensions
    {
        public static bool HasType(this ContentService.ChangeEventTypes change, ContentService.ChangeEventTypes type)
        {
            return (change & type) != ContentService.ChangeEventTypes.None;
        }

        public static bool HasTypesAll(this ContentService.ChangeEventTypes change, ContentService.ChangeEventTypes types)
        {
            return (change & types) == types;
        }

        public static bool HasTypesAny(this ContentService.ChangeEventTypes change, ContentService.ChangeEventTypes types)
        {
            return (change & types) != ContentService.ChangeEventTypes.None;
        }

        public static bool HasTypesNone(this ContentService.ChangeEventTypes change, ContentService.ChangeEventTypes types)
        {
            return (change & types) == ContentService.ChangeEventTypes.None;
        }
    }
}
