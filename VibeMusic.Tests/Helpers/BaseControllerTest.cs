using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using FluentAssertions;

namespace VibeMusic.Tests.Helpers;

/// <summary>
/// Abstract base class for controller unit tests.
/// Provides TempData mocking and common assertion helpers.
/// </summary>
/// <typeparam name="TController">The controller type under test.</typeparam>
public abstract class BaseControllerTest<TController> where TController : Controller
{
    protected Mock<ITempDataDictionary> TempDataMock { get; }
    protected Dictionary<string, object?> TempDataStore { get; }

    protected BaseControllerTest()
    {
        TempDataStore = new Dictionary<string, object?>();
        TempDataMock = new Mock<ITempDataDictionary>();

        // Capture writes to TempData
        TempDataMock
            .SetupSet(td => td[It.IsAny<string>()] = It.IsAny<object?>())
            .Callback<string, object?>((key, value) => TempDataStore[key] = value);

        // Return values from the store on reads
        TempDataMock
            .Setup(td => td[It.IsAny<string>()])
            .Returns<string>(key => TempDataStore.TryGetValue(key, out var val) ? val : null);

        // Support ContainsKey
        TempDataMock
            .Setup(td => td.ContainsKey(It.IsAny<string>()))
            .Returns<string>(key => TempDataStore.ContainsKey(key));
    }

    /// <summary>
    /// Assigns the TempData mock to the given controller instance.
    /// Call this after constructing the controller under test.
    /// </summary>
    protected void SetupTempData(TController controller)
    {
        controller.TempData = TempDataMock.Object;
    }

    /// <summary>
    /// Returns the value stored in TempData for the given key.
    /// </summary>
    protected object? GetTempData(string key)
    {
        return TempDataStore.TryGetValue(key, out var value) ? value : null;
    }

    /// <summary>
    /// Asserts that TempData["Success"] was set.
    /// Optionally verifies the exact message.
    /// </summary>
    protected void AssertSuccess(string? expectedMessage = null)
    {
        TempDataStore.Should().ContainKey("Success",
            because: "controller should set TempData[\"Success\"] on successful operations");

        if (expectedMessage != null)
        {
            TempDataStore["Success"].Should().Be(expectedMessage,
                because: $"success message should be \"{expectedMessage}\"");
        }
    }

    /// <summary>
    /// Asserts that TempData["Error"] was set.
    /// Optionally verifies the exact message.
    /// </summary>
    protected void AssertError(string? expectedMessage = null)
    {
        TempDataStore.Should().ContainKey("Error",
            because: "controller should set TempData[\"Error\"] on failed operations");

        if (expectedMessage != null)
        {
            TempDataStore["Error"].Should().Be(expectedMessage,
                because: $"error message should be \"{expectedMessage}\"");
        }
    }
}
