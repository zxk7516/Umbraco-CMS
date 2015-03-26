using System;

namespace Umbraco.Tests.TestHelpers
{
    // indicates the facade service behavior during tests
    // EnableRepositoryEvents: indicates whether the service should handle repository events
    //  XmlStore: if false, will not subscribe to repository event
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class FacadeServiceBehaviorAttribute : Attribute
    {
        public bool EnableRepositoryEvents { get; set; }
    }
}