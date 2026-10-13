using FluentAssertions;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Mapper;
using SaaS.Domain.Entities;
using System;
using System.Collections.Generic;
using Xunit;

namespace SaaS.Application.UnitTests.Mapper
{
    public class TargetGroupMapperExtensionTests
    {
        [Fact]
        public void ToDto_ValidTargetGroup_MapsAllPropertiesCorrectly()
        {
            // Arrange
            var group = new TargetGroup
            {
                Id = 1,
                GroupName = "Designers Community",
                GroupUrl = "https://example.com/groups/designers",
                IsActive = true
            };

            // Act
            var dto = group.ToDto();

            // Assert
            dto.Should().NotBeNull();
            dto.Id.Should().Be(1);
            dto.GroupName.Should().Be("Designers Community");
            dto.GroupUrl.Should().Be("https://example.com/groups/designers");
            dto.IsActive.Should().BeTrue();
        }

        [Fact]
        public void ToDto_NullTargetGroup_ThrowsArgumentNullException()
        {
            // Arrange
            TargetGroup nullGroup = null!;

            // Act
            var act = () => nullGroup.ToDto();

            // Assert
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("targetGroup");
        }

        [Fact]
        public void ToDtoList_ValidGroups_ReturnsMappedList()
        {
            // Arrange
            var groups = new List<TargetGroup>
            {
                new TargetGroup { Id = 1, GroupName = "Group 1", GroupUrl = "https://g1.com", IsActive = true },
                new TargetGroup { Id = 2, GroupName = "Group 2", GroupUrl = "https://g2.com", IsActive = false }
            };

            // Act
            var dtos = groups.ToDtoList();

            // Assert
            dtos.Should().NotBeNull();
            dtos.Should().HaveCount(2);
            dtos[0].Id.Should().Be(1);
            dtos[0].GroupName.Should().Be("Group 1");
            dtos[1].Id.Should().Be(2);
            dtos[1].GroupName.Should().Be("Group 2");
        }

        [Fact]
        public void ToDtoList_EmptyCollection_ReturnsEmptyList()
        {
            // Arrange
            var groups = new List<TargetGroup>();

            // Act
            var dtos = groups.ToDtoList();

            // Assert
            dtos.Should().NotBeNull();
            dtos.Should().BeEmpty();
        }

        [Fact]
        public void ToDtoList_NullCollection_ThrowsArgumentNullException()
        {
            // Arrange
            IEnumerable<TargetGroup> nullGroups = null!;

            // Act
            var act = () => nullGroups.ToDtoList();

            // Assert
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("targetGroups");
        }

        [Fact]
        public void ToDetailsDto_ValidTargetGroup_MapsAllPropertiesCorrectly()
        {
            // Arrange
            var group = new TargetGroup
            {
                Id = 5,
                GroupName = "Developers Hub",
                GroupUrl = "https://example.com/groups/devs",
                ConfigJson = "{\"maxMembers\": 1000}",
                IsActive = true
            };

            // Act
            var detailsDto = group.ToDetailsDto(leadsCount: 150, scrapesCount: 12);

            // Assert
            detailsDto.Should().NotBeNull();
            detailsDto.Id.Should().Be(5);
            detailsDto.GroupName.Should().Be("Developers Hub");
            detailsDto.GroupUrl.Should().Be("https://example.com/groups/devs");
            detailsDto.ConfigJson.Should().Be("{\"maxMembers\": 1000}");
            detailsDto.IsActive.Should().BeTrue();
            detailsDto.RelatedLeadsCount.Should().Be(150);
            detailsDto.ScrapesCount.Should().Be(12);
        }

        [Fact]
        public void ToDetailsDto_NullTargetGroup_ThrowsArgumentNullException()
        {
            // Arrange
            TargetGroup nullGroup = null!;

            // Act
            var act = () => nullGroup.ToDetailsDto(0, 0);

            // Assert
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("targetGroup");
        }

        [Fact]
        public void FromDto_ValidDto_UpdatesTargetGroupProperties()
        {
            // Arrange
            var existingGroup = new TargetGroup
            {
                Id = 1,
                GroupName = "Old Name",
                GroupUrl = "https://old.url",
                IsActive = false,
                ConfigJson = "{\"setting\": true}",
                LastCursor = "cursor-123"
            };

            var updateDto = new UpdateGroupDto(
                GroupName: "Updated Name",
                GroupUrl: "https://new.url",
                ConfigJson: "{\"setting\": false}",
                IsActive: true,
                LastCursor: "new-cursor"
            );

            // Act
            var result = existingGroup.FromDto(updateDto);

            // Assert
            result.Should().BeSameAs(existingGroup);
            result.GroupName.Should().Be("Updated Name");
            result.GroupUrl.Should().Be("https://new.url");
            result.IsActive.Should().BeTrue();
            // Preserved properties according to FromDto implementation
            result.ConfigJson.Should().Be("{\"setting\": true}");
            result.LastCursor.Should().Be("cursor-123");
        }

        [Fact]
        public void FromDto_NullTargetGroup_ThrowsArgumentNullException()
        {
            // Arrange
            TargetGroup nullGroup = null!;
            var updateDto = new UpdateGroupDto("Name", "Url", "{}", true, null);

            // Act
            var act = () => nullGroup.FromDto(updateDto);

            // Assert
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("targetGroup");
        }

        [Fact]
        public void FromDto_NullUpdateDto_ThrowsArgumentNullException()
        {
            // Arrange
            var existingGroup = new TargetGroup { Id = 1 };
            UpdateGroupDto nullDto = null!;

            // Act
            var act = () => existingGroup.FromDto(nullDto);

            // Assert
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("groupDto");
        }
    }
}
