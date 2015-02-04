using System;

namespace Umbraco.Tests.TestHelpers
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class FacadeServiceBehaviorAttribute : Attribute
    {
        public bool WithEvents { get; set; }
    }
}