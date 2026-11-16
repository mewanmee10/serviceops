
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceOps.Api.Authorization;
using ServiceOps.Api.DTOs.Users;
using ServiceOps.Api.Services.Interfaces;

namespace ServiceOps.Api.Controllers;

[Authorize(Policy="permission:users.manage")]
[ApiController]
[TypeFilter(typeof(AccountApiExceptionFilter))]
[Route("api/users")]
[Authorize(Roles = ServiceOpsRoles.Administrator)]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet("roles")]
    public async Task<IActionResult> GetRoles() => Ok(await _userService.GetRolesAsync());

    private int ActorUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // GET /api/users
    [HttpGet]
    public async Task<IActionResult> GetAllUsers()
    {
        var users = await _userService.GetAllUsersAsync();
        return Ok(users);
    }

    // GET /api/users/{id}
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetUserById(int id)
    {
        var user = await _userService.GetUserByIdAsync(id);

        if (user == null)
            return NotFound(new { message = "User not found." });

        return Ok(user);
    }

    // POST /api/users
    [HttpPost]
    public async Task<IActionResult> CreateUser(
        [FromBody] CreateUserRequest request)
    {
        try
        {
            var user = await _userService.CreateUserAsync(request);

            return CreatedAtAction(
                nameof(GetUserById),
                new { id = user.UserId },
                user);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    // PUT /api/users/{id}
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateUser(
        int id,
        [FromBody] UpdateUserRequest request)
    {
        try
        {
            var updated = await _userService.UpdateUserAsync(id, request, ActorUserId);

            if (!updated)
                return NotFound(new { message = "User not found." });

            return Ok(new { message = "User updated successfully." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    // Removal means reversible deactivation. History/profile identities remain intact.
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> RemoveUser(int id)
    {
        try
        {
            var updated = await _userService.UpdateUserStatusAsync(id,
                new UpdateUserStatusRequest { Status = "Inactive" }, ActorUserId);
            return updated ? Ok(new { message = "User deactivated; historical records retained." })
                : NotFound(new { message = "User not found." });
        }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    // PATCH /api/users/{id}/status
    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> UpdateUserStatus(
        int id,
        [FromBody] UpdateUserStatusRequest request)
    {
        try
        {
            var updated = await _userService.UpdateUserStatusAsync(
                id, request, ActorUserId);

            if (!updated)
                return NotFound(new { message = "User not found." });

            return Ok(new { message = "User status updated successfully." });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }
}
