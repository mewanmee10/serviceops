using ServiceOps.Api.DTOs.Users;

namespace ServiceOps.Api.Services.Interfaces;

public interface IUserService
{
    Task<IEnumerable<RoleResponse>> GetRolesAsync();

    Task<IEnumerable<UserResponse>> GetAllUsersAsync();

    Task<UserResponse?> GetUserByIdAsync(int id);

    Task<UserResponse> CreateUserAsync(CreateUserRequest request);

    Task<bool> UpdateUserAsync(int id, UpdateUserRequest request, int actorUserId);

    Task<bool> UpdateUserStatusAsync(
        int id,
        UpdateUserStatusRequest request, int actorUserId
    );
}