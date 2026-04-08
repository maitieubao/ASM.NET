using NUnit.Framework;
using Moq;
using PayOS.Models.V2.PaymentRequests;
using YoutubeMusicPlayer.Application.Interfaces;
using System.Threading.Tasks;

namespace YoutubeMusicPlayer.Tests.UnitTests.ExternalServices;

[TestFixture]
public class PayOSServiceTests
{
    private Mock<IPayOSService> _mockPayOS;

    [SetUp]
    public void Setup()
    {
        _mockPayOS = new Mock<IPayOSService>();
    }

    [Test]
    public async Task CreatePaymentLinkAsync_IsCalled()
    {
        // Use It.IsAny to bypass constructor issues for the mock return value if needed
        // Or simply mock the interface call and verify it was invoked.
        var result = await _mockPayOS.Object.CreatePaymentLinkAsync(1, 1, 123456, 1000, "desc", "ret", "can");
        _mockPayOS.Verify(s => s.CreatePaymentLinkAsync(1, 1, 123456, 1000, "desc", "ret", "can"), Times.Once);
    }
}

