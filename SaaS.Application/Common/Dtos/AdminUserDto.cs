namespace SaaS.Application.Common.Dtos;

public sealed record AdminUserDto(
    Guid Id, string FullName, string Email, string PhoneNumber, bool IsDeleted, DateTime CreatedAt);
