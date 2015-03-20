using System;

namespace Umbraco.Tests.TestHelpers
{
    // indicates the facade service behavior during tests
    // WithEvents: indicates whether the service should handle events
    //  XmlStore: if false, will not subscribe to any event
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class FacadeServiceBehaviorAttribute : Attribute
    {
        public bool WithEvents { get; set; }
    }
}