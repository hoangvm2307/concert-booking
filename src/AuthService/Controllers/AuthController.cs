using Microsoft.AspNetCore.Mvc;
using AuthService.Models;
using AuthService.Models.DTOs;
using AuthService.Services;
using AuthService.Utils;

namespace AuthService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserService _userService;
        private readonly JwtTokenGenerator _jwtTokenGenerator;

        public AuthController(UserService userService, JwtTokenGenerator jwtTokenGenerator)
        {
            _userService = userService;
            _jwtTokenGenerator = jwtTokenGenerator;
        }

        [HttpPost("register")]
        [ProducesResponseType(typeof(AuthSuccessResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Register([FromBody] RegisterRequestDto registerRequest)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ErrorResponseDto("Validation failed", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()));
            }

            var existingUserByUsername = await _userService.GetByUsernameAsync(registerRequest.Username);

            if (existingUserByUsername != null) return BadRequest(new ErrorResponseDto("Username already exists."));

            var existingUserByEmail = await _userService.GetByEmailAsync(registerRequest.Email);

            if (existingUserByEmail != null) return BadRequest(new ErrorResponseDto("Email already registered."));

            var hashedPassword = PasswordHasher.HashPassword(registerRequest.Password);

            var newUser = new User
            {
                Username = registerRequest.Username,
                Email = registerRequest.Email,
                PasswordHash = hashedPassword,
                RegisteredAt = DateTime.UtcNow
            };

            await _userService.CreateAsync(newUser);

            if (string.IsNullOrEmpty(newUser.Id))
            {
                var createdUser = await _userService.GetByUsernameAsync(newUser.Username);
                if (createdUser == null || string.IsNullOrEmpty(createdUser.Id))
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponseDto("Failed to create user or retrieve ID."));
                }
                newUser = createdUser;
            }

            var (token, expiration) = _jwtTokenGenerator.GenerateToken(newUser);

            return Ok(new AuthSuccessResponseDto
            {
                UserId = newUser.Id!,
                Username = newUser.Username,
                Email = newUser.Email,
                Token = token,
                Expiration = expiration
            });
        }

        [HttpPost("login")]
        [ProducesResponseType(typeof(AuthSuccessResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto loginRequest)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ErrorResponseDto("Validation failed", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()));
            }

            var user = await _userService.GetByUsernameAsync(loginRequest.Username);

            if (user == null || !PasswordHasher.VerifyPassword(loginRequest.Password, user.PasswordHash))
            {
                return BadRequest(new ErrorResponseDto("Invalid username or password."));
            }

            if (string.IsNullOrEmpty(user.Id))
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponseDto("User data is corrupted. Missing ID."));
            }

            var (token, expiration) = _jwtTokenGenerator.GenerateToken(user);

            return Ok(new AuthSuccessResponseDto
            {
                UserId = user.Id,
                Username = user.Username,
                Email = user.Email,
                Token = token,
                Expiration = expiration
            });
        }
    }
}