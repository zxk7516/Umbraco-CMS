using System;
using System.Diagnostics;
using NUnit.Framework;

// this class has NO NAMESPACE
// it applies to the whole assembly

[SetUpFixture]
// ReSharper disable once CheckNamespace
public class TestsSetup
{
    private Stopwatch _stopwatch;

    [SetUp]
    //[OneTimeSetUp] v3
    public void SetUp()
    {
        _stopwatch = Stopwatch.StartNew();
    }

    [TearDown]
    //[OneTimeTearDown] v3
    public void TearDown()
    {
        Console.WriteLine("TOTAL TESTS DURATION: {0}", _stopwatch.Elapsed);
    }
}
