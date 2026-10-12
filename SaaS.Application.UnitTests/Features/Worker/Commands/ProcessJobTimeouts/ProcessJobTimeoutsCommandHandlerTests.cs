using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Common.Models;
using SaaS.Application.Common.Settings;
using SaaS.Application.Features.Worker.Commands.ProcessJobTimeouts;
using SaaS.Application.UnitTests.Common;
using SaaS.Domain.Entities;
using SaaS.Domain.Enums;
using SaaS.Domain.Extensions;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SaaS.Application.UnitTests.Features.Worker.Commands.ProcessJobTimeouts
{
    public class ProcessJobTimeoutsCommandHandlerTests
    {
        private readonly Mock<IAppNotificationService> _notificationServiceMock;
        private readonly Mock<INetworkClient> _networkClientMock;
        private readonly Mock<ILogger<ProcessJobTimeoutsCommandHandler>> _loggerMock;
        private readonly Mock<IOptions<JobWatchdogOptions>> _optionsMock;
        private readonly Mock<IJobStalenessStrategy> _stalenessStrategyMock;
        private readonly JobWatchdogOptions _jobWatchdogOptions;

        public ProcessJobTimeoutsCommandHandlerTests()
        {
            _notificationServiceMock = new Mock<IAppNotificationService>();
            _networkClientMock = new Mock<INetworkClient>();
            _loggerMock = new Mock<ILogger<ProcessJobTimeoutsCommandHandler>>();
            _optionsMock = new Mock<IOptions<JobWatchdogOptions>>();
            _stalenessStrategyMock = new Mock<IJobStalenessStrategy>();

            _jobWatchdogOptions = new JobWatchdogOptions
            {
                TimeoutThresholdMinutes = 15,
                LivenessCheckTimeoutSeconds = 10
            };
            _optionsMock.Setup(o => o.Value).Returns(_jobWatchdogOptions);

            _stalenessStrategyMock.Setup(s => s.JobType).Returns(JobType.MESSAGING);
            _stalenessStrategyMock.Setup(s => s.ExtractLeadIds(It.IsAny<Job>(), It.IsAny<ILogger>()))
                .Returns(new List<long>());
        }

        private MockAppDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<MockAppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new MockAppDbContext(options);
        }

        private ProcessJobTimeoutsCommandHandler CreateHandler(MockAppDbContext dbContext)
        {
            return new ProcessJobTimeoutsCommandHandler(
                dbContext,
                _notificationServiceMock.Object,
                _networkClientMock.Object,
                _loggerMock.Object,
                _optionsMock.Object,
                new List<IJobStalenessStrategy> { _stalenessStrategyMock.Object }
            );
        }

        [Fact]
        public async Task Should_Return_Success_When_No_Processing_Jobs_Exist()
        {
            // Arrange
            using var dbContext = CreateInMemoryDbContext();
            var handler = CreateHandler(dbContext);

            // Act
            var result = await handler.Handle(new ProcessJobTimeoutsCommand(), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            _stalenessStrategyMock.Verify(s => s.GetLastActivity(It.IsAny<Job>(), It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<Dictionary<long, DateTime>>()), Times.Never);
        }

        [Fact]
        public async Task Should_Not_Fail_Job_When_Job_Is_Not_Stale()
        {
            // Arrange
            using var dbContext = CreateInMemoryDbContext();
            var job = new Job
            {
                Type = JobType.MESSAGING.ToDbString(),
                Status = JobStatus.PROCESSING.ToDbString(),
                CreatedAt = DateTime.UtcNow
            };
            await dbContext.Jobs.AddAsync(job);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            // Job is very recent
            _stalenessStrategyMock.Setup(s => s.GetLastActivity(It.IsAny<Job>(), It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<Dictionary<long, DateTime>>()))
                .Returns(DateTime.UtcNow.AddMinutes(-5));

            var handler = CreateHandler(dbContext);

            // Act
            var result = await handler.Handle(new ProcessJobTimeoutsCommand(), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            
            var dbJob = await dbContext.Jobs.FindAsync(job.Id);
            dbJob!.Status.Should().Be(JobStatus.PROCESSING.ToDbString()); // Status unchanged
            
            _networkClientMock.Verify(n => n.GetAsync(It.IsAny<string>(), It.IsAny<ExternalSystem>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Should_Not_Fail_Job_When_Job_Is_Stale_But_Worker_Is_Still_Processing()
        {
            // Arrange
            using var dbContext = CreateInMemoryDbContext();
            var job = new Job
            {
                Type = JobType.MESSAGING.ToDbString(),
                Status = JobStatus.PROCESSING.ToDbString(),
                CreatedAt = DateTime.UtcNow
            };
            await dbContext.Jobs.AddAsync(job);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            // Job looks stale
            _stalenessStrategyMock.Setup(s => s.GetLastActivity(It.IsAny<Job>(), It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<Dictionary<long, DateTime>>()))
                .Returns(DateTime.UtcNow.AddMinutes(-20));

            // Worker responds that it's still running
            var workerResponse = NetworkResult.Ok(200, "{ \"status\": \"processing\" }");
            _networkClientMock.Setup(n => n.GetAsync($"jobs/{job.Id}/status", ExternalSystem.NodeWorker, It.IsAny<CancellationToken>()))
                .ReturnsAsync(workerResponse);

            var handler = CreateHandler(dbContext);

            // Act
            var result = await handler.Handle(new ProcessJobTimeoutsCommand(), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();

            var dbJob = await dbContext.Jobs.FindAsync(job.Id);
            dbJob!.Status.Should().Be(JobStatus.PROCESSING.ToDbString()); // Status unchanged

            _notificationServiceMock.Verify(n => n.NotifyJobFailedAsync(It.IsAny<Guid>(), It.IsAny<long>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Should_Fail_Job_When_Job_Is_Stale_And_Worker_Is_Unreachable()
        {
            // Arrange
            using var dbContext = CreateInMemoryDbContext();
            var job = new Job
            {
                Type = JobType.MESSAGING.ToDbString(),
                Status = JobStatus.PROCESSING.ToDbString(),
                CreatedAt = DateTime.UtcNow,
                PayloadJson = "{}"
            };
            await dbContext.Jobs.AddAsync(job);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            // Job looks stale
            _stalenessStrategyMock.Setup(s => s.GetLastActivity(It.IsAny<Job>(), It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<Dictionary<long, DateTime>>()))
                .Returns(DateTime.UtcNow.AddMinutes(-20));

            // Worker responds with an error (crashed)
            var workerResponse = NetworkResult.Fail(500, "The server was down");
            _networkClientMock.Setup(n => n.GetAsync($"jobs/{job.Id}/status", ExternalSystem.NodeWorker, It.IsAny<CancellationToken>()))
                .ReturnsAsync(workerResponse);

            var handler = CreateHandler(dbContext);

            // Act
            var result = await handler.Handle(new ProcessJobTimeoutsCommand(), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();

            var dbJob = await dbContext.Jobs.FindAsync(job.Id);
            dbJob!.Status.Should().Be(JobStatus.FAILED.ToDbString()); // Status updated to FAILED

            _notificationServiceMock.Verify(n => n.NotifyJobFailedAsync(job.UserId, job.Id, It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task Should_Unlock_Connected_Account_When_Failing_Stale_Job()
        {
            // Arrange
            using var dbContext = CreateInMemoryDbContext();
            
            var account = new ConnectedAccount
            {
                Status = AccountStatus.BUSY.ToDbString(),
                DisplayName = "Test Account",
                Platform = "Facebook"
            };
            await dbContext.ConnectedAccounts.AddAsync(account);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            var job = new Job
            {
                Type = JobType.MESSAGING.ToDbString(),
                Status = JobStatus.PROCESSING.ToDbString(),
                CreatedAt = DateTime.UtcNow,
                PayloadJson = $"{{\"accountId\": {account.Id}}}"
            };
            await dbContext.Jobs.AddAsync(job);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            // Job looks stale
            _stalenessStrategyMock.Setup(s => s.GetLastActivity(It.IsAny<Job>(), It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<Dictionary<long, DateTime>>()))
                .Returns(DateTime.UtcNow.AddMinutes(-20));

            // Worker is unreachable
            _networkClientMock.Setup(n => n.GetAsync($"jobs/{job.Id}/status", ExternalSystem.NodeWorker, It.IsAny<CancellationToken>()))
                .ReturnsAsync((NetworkResult)null);

            var handler = CreateHandler(dbContext);

            // Act
            var result = await handler.Handle(new ProcessJobTimeoutsCommand(), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();

            var dbJob = await dbContext.Jobs.FindAsync(job.Id);
            dbJob!.Status.Should().Be(JobStatus.FAILED.ToDbString()); 

            var dbAccount = await dbContext.ConnectedAccounts.FindAsync(account.Id);
            dbAccount!.Status.Should().Be(AccountStatus.ACTIVE.ToDbString()); // Account is unlocked
        }
    }
}
