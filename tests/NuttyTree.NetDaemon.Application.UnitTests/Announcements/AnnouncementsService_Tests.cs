using System;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Bogus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;
using NuttyTree.NetDaemon.Application.Announcements;
using NuttyTree.NetDaemon.Application.Announcements.Models;
using NuttyTree.NetDaemon.Infrastructure.HomeAssistant;
using NuttyTree.NetDaemon.Infrastructure.RateLimiting;
using Xunit;

namespace NuttyTree.NetDaemon.Application.UnitTests.Announcements;

public class AnnouncementsService_Tests
{
    private readonly Faker faker = new ();

    private string testMessage;

    private string testPerson;

    private Mock<IHaContext> haContext;

    private AnnouncementsService announcementsService;

    [Fact]
    public async Task SendAnnouncement_AnnouncementIsSent()
    {
        // Arrange
        Arrange();

        // Act
        await announcementsService.SendAnnouncementAsync(testMessage);

        // Assert
        VerifyAnnouncementSent();
    }

    [Fact]
    public async Task SendAnnouncement_NotDayMode_AnnouncementIsNotSent()
    {
        // Arrange
        Arrange(houseModeIsDay: false);

        // Act
        await announcementsService.SendAnnouncementAsync(testMessage);

        // Assert
        VerifyAnnouncementSent(Times.Never);
    }

    [Fact]
    public async Task SendAnnouncement_MelissaIsInBed_AnnouncementIsNotSent()
    {
        // Arrange
        Arrange(melissaIsNotInBed: false);

        // Act
        await announcementsService.SendAnnouncementAsync(testMessage);

        // Assert
        VerifyAnnouncementSent(Times.Never);
    }

    [Fact]
    public async Task SendAnnouncement_WithPersonThatIsHome_AnnouncementIsSent()
    {
        // Arrange
        Arrange();

        // Act
        await announcementsService.SendAnnouncementAsync(testMessage, person: testPerson);

        // Assert
        VerifyAnnouncementSent();
    }

    [Fact]
    public async Task SendAnnouncement_WithPersonThatIsNotHome_AnnouncementIsNotSent()
    {
        // Arrange
        Arrange(testPersonIsHome: false);

        // Act
        await announcementsService.SendAnnouncementAsync(testMessage, person: testPerson);

        // Assert
        VerifyAnnouncementSent(Times.Never);
    }

    [Fact]
    public async Task SendAnnouncement_AnnouncementsAreDisabled_AnnouncementIsNotSent()
    {
        // Arrange
        Arrange(announcementsAreEnabled: false);

        // Act
        await announcementsService.SendAnnouncementAsync(testMessage);

        // Assert
        VerifyAnnouncementSent(Times.Never);
    }

    [Fact]
    public async Task SendCriticalAnnouncement_AnnouncementsAreDisabled_AnnouncementIsSent()
    {
        // Arrange
        Arrange(announcementsAreEnabled: false);

        // Act
        await announcementsService.SendAnnouncementAsync(testMessage, AnnouncementType.General, AnnouncementPriority.Critical);

        // Assert
        VerifyAnnouncementSent();
    }

    [Fact]
    public async Task SendWarningAnnouncement_AnnouncementsAreDisabled_AnnouncementIsNotSent()
    {
        // Arrange
        Arrange(announcementsAreEnabled: false);

        // Act
#pragma warning disable CA1849 // Call async methods when in an async method
        announcementsService.SendAnnouncement(testMessage, AnnouncementType.General, AnnouncementPriority.Warning);
#pragma warning restore CA1849 // Call async methods when in an async method

        // Assert
        await Task.Delay(500);
        VerifyAnnouncementSent(Times.Never);
    }

    [Fact]
    public async Task SendWarningAnnouncement_AnnouncementsAreDisabled_AnnouncementIsNotSentUntilAnnouncementsAreEnabled()
    {
        // Arrange
        Arrange(announcementsAreEnabled: false);

        // Act
#pragma warning disable CA1849 // Call async methods when in an async method
        announcementsService.SendAnnouncement(testMessage, AnnouncementType.General, AnnouncementPriority.Warning);
#pragma warning restore CA1849 // Call async methods when in an async method

        // Assert
        await Task.Delay(500);
        VerifyAnnouncementSent(Times.Never);
        announcementsService.EnableAnnouncements();
        await Task.Delay(500);
        VerifyAnnouncementSent();
    }

    private void Arrange(
        bool houseModeIsDay = true,
        bool melissaIsNotInBed = true,
        bool testPersonIsHome = true,
        bool announcementsAreEnabled = true)
    {
        testMessage = faker.Random.AlphaNumeric(20);
        testPerson = faker.Random.AlphaNumeric(20);

        var stateAllChangeSubject = new Subject<StateChange>();

        var eventsSubject = new Subject<Event>();

        haContext = new Mock<IHaContext>();
        haContext.Setup(x => x.StateAllChanges())
            .Returns(stateAllChangeSubject);
        haContext.Setup(x => x.Events)
            .Returns(eventsSubject);

        var testPersonState = new EntityState();
        var testPersonStateProperty = typeof(EntityState).GetProperty(nameof(EntityState.State));
        testPersonStateProperty.SetValue(testPersonState, testPersonIsHome ? "home" : faker.Random.AlphaNumeric(10));
        haContext.Setup(x => x.GetState($"person.{testPerson.ToLowerInvariant()}"))
            .Returns(testPersonState);

        var entities = new Entities(haContext.Object);
        haContext.Setup(x => x.GetState(entities.InputSelect.HouseMode.EntityId))
            .Returns(new EntityState { State = houseModeIsDay ? "Day" : faker.Random.AlphaNumeric(5) });
        haContext.Setup(x => x.GetState(entities.BinarySensor.MelissaIsInBed.EntityId))
            .Returns(new EntityState { State = melissaIsNotInBed ? "off" : "on" });

        var haServices = new Services(haContext.Object);

        var services = new ServiceCollection();
        services.AddSingleton(haContext.Object);
        services.AddSingleton<IEntities>(entities);
        services.AddSingleton<IServices>(haServices);

        var serviceScope = new Mock<IServiceScope>();
        serviceScope.Setup(x => x.ServiceProvider)
            .Returns(services.BuildServiceProvider());

        var serviceScopeFactory = new Mock<IServiceScopeFactory>();
        serviceScopeFactory.Setup(x => x.CreateScope())
            .Returns(serviceScope.Object);

        var hostApplicationLifetime = new Mock<IHostApplicationLifetime>();

        announcementsService = new AnnouncementsService(hostApplicationLifetime.Object, Mock.Of<IRateLimiter<AnnouncementsService>>(), Mock.Of<ILogger<AnnouncementsService>>());
        announcementsService.Initialize(entities, haContext.Object, haServices);

        if (!announcementsAreEnabled)
        {
            announcementsService.DisableAnnouncements(1);
        }
    }

    private void VerifyAnnouncementSent(Func<Times> times = null)
        => haContext.Verify(
            x => x.CallService(
                "notify",
                "alexa_media_devices_inside",
                null,
                It.Is<NotifyAlexaMediaDevicesInsideParameters>(p =>
                    (p.Data.ToString().Contains("type = reminder") && p.Message.EndsWith($", {testMessage}"))
                    || (!p.Data.ToString().Contains("type = reminder") && p.Message == $"{testMessage}"))),
            times ?? Times.Once);
}
