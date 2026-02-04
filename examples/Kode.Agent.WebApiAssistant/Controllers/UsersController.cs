using Kode.Agent.WebApiAssistant.Models.Requests;
using Kode.Agent.WebApiAssistant.Models.Responses;
using Kode.Agent.WebApiAssistant.Services;
using Microsoft.AspNetCore.Mvc;

namespace Kode.Agent.WebApiAssistant.Controllers;

/// <summary>
/// 用户管理控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        IUserService userService,
        ILogger<UsersController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    /// <summary>
    /// 注册新用户
    /// </summary>
    /// <param name="request">注册请求</param>
    /// <returns>用户信息</returns>
    //[HttpPost("register")]
    //public async Task<ActionResult<UserResponse>> Register([FromBody] UserRegisterRequest request)
    //{
    //    if (string.IsNullOrWhiteSpace(request.Username))
    //    {
    //        return BadRequest(new { error = "Username is required" });
    //    }

    //    try
    //    {
    //        var user = await _userService.CreateUserAsync(
    //            request.Username,
    //            request.Email);
            
    //        return CreatedAtAction(
    //            nameof(GetProfile),
    //            new { userId = user.UserId },
    //            UserResponse.FromEntity(user));
    //    }
    //    catch (Exception ex)
    //    {
    //        _logger.LogError(ex, "Failed to register user: {Username}", request.Username);
    //        return StatusCode(500, new { error = "Failed to register user" });
    //    }
    //}

    /// <summary>
    /// 创建指定 userId 的用户
    /// </summary>
    [HttpPost("create")]
    public async Task<ActionResult<UserResponse>> CreateUser(
        [FromQuery] string userId,
        [FromBody] UserCreateRequest? request)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return BadRequest(new { error = "userId is required" });
        }

        try
        {
            var user = await _userService.GetOrCreateUserAsync(userId);
            return CreatedAtAction(
                nameof(GetProfile),
                new { userId = user.UserId },
                UserResponse.FromEntity(user));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create user: {UserId}", userId);
            return StatusCode(500, new { error = "Failed to create user" });
        }
    }

    /// <summary>
    /// 获取用户信息
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <returns>用户信息</returns>
    [HttpGet("profile")]
    public async Task<ActionResult<UserResponse>> GetProfile([FromQuery] string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return BadRequest(new { error = "userId is required" });
        }

        var user = await _userService.GetUserAsync(userId);
        if (user == null)
        {
            return NotFound(new { error = "User not found" });
        }

        return Ok(UserResponse.FromEntity(user));
    }

    /// <summary>
    /// 用户登出
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <returns>No Content</returns>
    [HttpPost("logout")]
    public async Task<ActionResult> Logout([FromQuery] string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return BadRequest(new { error = "userId is required" });
        }

        //await _userService.LogoutAsync(userId);
        return Ok(new { message = "Logged out successfully" });
    }
}
