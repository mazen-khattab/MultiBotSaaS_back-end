using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Moq;
using SaaS.Infrastructure.Hubs;
using SaaS.Infrastructure.Services;

namespace SaaS.Infrastructure.UnitTests.Services
{
    public class SignalRNotificationServiceTests
    {
        private readonly Mock<IHubContext<AppNotificationHub>> _mockHubContext;
        private readonly Mock<IHubClients> _mockHubClients;
        private readonly Mock<IClientProxy> _mockClientProxy;
        private readonly SignalRNotificationService _service;

        public SignalRNotificationServiceTests()
        {
            _mockHubContext = new Mock<IHubContext<AppNotificationHub>>();
            _mockHubClients = new Mock<IHubClients>();
            _mockClientProxy = new Mock<IClientProxy>();

            _mockHubContext.Setup(x => x.Clients).Returns(_mockHubClients.Object);
            _mockHubClients.Setup(x => x.User(It.IsAny<string>())).Returns(_mockClientProxy.Object);

            _service = new SignalRNotificationService(_mockHubContext.Object);
        }

        [Fact]
        public async Task NotifyScrapeCompletedAsync_ShouldSendScrapeCompletedEvent_ToTargetUser()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var scrapeId = 123;
            var leadsCount = 45;

            object?[]? capturedArgs = null;
            string? capturedMethod = null;

            _mockClientProxy
                .Setup(x => x.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), default))
                .Callback<string, object?[], CancellationToken>((method, args, _) =>
                {
                    capturedMethod = method;
                    capturedArgs = args;
                })
                .Returns(Task.CompletedTask);

            // Act
            await _service.NotifyScrapeCompletedAsync(userId, scrapeId, leadsCount);

            // Assert
            _mockHubClients.Verify(x => x.User(userId.ToString()), Times.Once);
            capturedMethod.Should().Be("ScrapeCompleted");
            capturedArgs.Should().NotBeNull().And.HaveCount(1);

            var payload = capturedArgs![0];
            payload.Should().NotBeNull();
            payload!.GetType().GetProperty("ScrapeId")!.GetValue(payload).Should().Be(scrapeId);
            payload.GetType().GetProperty("LeadsCount")!.GetValue(payload).Should().Be(leadsCount);
        }

        [Fact]
        public async Task NotifyLeadStatusUpdatedAsync_ShouldSendLeadStatusUpdatedEvent_ToTargetUser()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var leadId = 789L;
            var status = "Messaged";

            object?[]? capturedArgs = null;
            string? capturedMethod = null;

            _mockClientProxy
                .Setup(x => x.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), default))
                .Callback<string, object?[], CancellationToken>((method, args, _) =>
                {
                    capturedMethod = method;
                    capturedArgs = args;
                })
                .Returns(Task.CompletedTask);

            // Act
            await _service.NotifyLeadStatusUpdatedAsync(userId, leadId, status);

            // Assert
            _mockHubClients.Verify(x => x.User(userId.ToString()), Times.Once);
            capturedMethod.Should().Be("LeadStatusUpdated");
            capturedArgs.Should().NotBeNull().And.HaveCount(1);

            var payload = capturedArgs![0];
            payload.Should().NotBeNull();
            payload!.GetType().GetProperty("LeadId")!.GetValue(payload).Should().Be(leadId);
            payload.GetType().GetProperty("Status")!.GetValue(payload).Should().Be(status);
        }

        [Fact]
        public async Task NotifyJobFailedAsync_ShouldSendJobFailedEvent_ToTargetUser()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var jobId = 999L;
            var message = "Target account was blocked by rate limit";

            object?[]? capturedArgs = null;
            string? capturedMethod = null;

            _mockClientProxy
                .Setup(x => x.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), default))
                .Callback<string, object?[], CancellationToken>((method, args, _) =>
                {
                    capturedMethod = method;
                    capturedArgs = args;
                })
                .Returns(Task.CompletedTask);

            // Act
            await _service.NotifyJobFailedAsync(userId, jobId, message);

            // Assert
            _mockHubClients.Verify(x => x.User(userId.ToString()), Times.Once);
            capturedMethod.Should().Be("JobFailed");
            capturedArgs.Should().NotBeNull().And.HaveCount(1);

            var payload = capturedArgs![0];
            payload.Should().NotBeNull();
            payload!.GetType().GetProperty("JobId")!.GetValue(payload).Should().Be(jobId);
            payload.GetType().GetProperty("Message")!.GetValue(payload).Should().Be(message);
        }
    }
}
