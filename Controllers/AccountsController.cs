
using BCryptNet = BCrypt.Net.BCrypt;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MySqlConnector;
using csboilerplate.Helpers;
using csboilerplate.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using csboilerplate.Entities;

namespace csboilerplate.Controllers
{
    [ApiController]
    [Route("api/accounts")]
    public class AccountsController : ControllerBase
    {
        private readonly DataContext _dataContext;
        private readonly IConfiguration _configuration;

        public AccountsController(
            DataContext dataContext,
            IConfiguration configuration)
        {
            _dataContext = dataContext;
            _configuration = configuration;
        }

        // REGISTER
        [HttpPost("register")]
        public async Task<IActionResult> Register(
            [FromBody] RegisterRequest request)
        {
            await using MySqlConnection connection =
                _dataContext.CreateConnection();

            await connection.OpenAsync();

            const string checkSql =
                "SELECT COUNT(*) FROM accounts WHERE email = @Email";

            await using (var checkCommand =
                new MySqlCommand(checkSql, connection))
            {
                checkCommand.Parameters.AddWithValue(
                    "@Email", request.Email.Trim());

                int count = Convert.ToInt32(
                    await checkCommand.ExecuteScalarAsync());

                if (count > 0)
                {
                    return Conflict(new
                    {
                        message = "That email is already registered."
                    });
                }
            }

            string passwordHash =
                BCryptNet.HashPassword(request.Password);

            const string insertSql = @"
                INSERT INTO accounts
                    (first_name, last_name, email, password_hash, role)
                VALUES
                    (@FirstName, @LastName, @Email, @PasswordHash, 'User')";

            await using var insertCommand =
                new MySqlCommand(insertSql, connection);

            insertCommand.Parameters.AddWithValue(
                "@FirstName", request.FirstName.Trim());

            insertCommand.Parameters.AddWithValue(
                "@LastName", request.LastName.Trim());

            insertCommand.Parameters.AddWithValue(
                "@Email", request.Email.Trim());

            insertCommand.Parameters.AddWithValue(
                "@PasswordHash", passwordHash);

            await insertCommand.ExecuteNonQueryAsync();

            return StatusCode(201, new
            {
                message = "Account registered successfully."
            });
        }

        // LOGIN
        [HttpPost("login")]
        
        public async Task<IActionResult> Login(
            [FromBody] LoginRequest request)
            
        {
            await using MySqlConnection connection =
                _dataContext.CreateConnection();

            await connection.OpenAsync();

            const string sql = @"
                SELECT id, first_name, last_name, email,
                       password_hash, role, is_verified
                FROM accounts
                WHERE email = @Email
                LIMIT 1";

            await using var command =
                new MySqlCommand(sql, connection);

            command.Parameters.AddWithValue(
                "@Email", request.Email.Trim());

            int id;
            string firstName;
            string lastName;
            string email;
            string passwordHash;
            string role;
            bool isVerified;

            await using (var reader =
                await command.ExecuteReaderAsync())
            {
                if (!await reader.ReadAsync())
                {
                    return Unauthorized(new
                    {
                        message = "Invalid email or password."
                    });
                }

                id = reader.GetInt32("id");
                firstName = reader.GetString("first_name");
                lastName = reader.GetString("last_name");
                email = reader.GetString("email");
                passwordHash = reader.GetString("password_hash");
                role = reader.GetString("role");
                isVerified = reader.GetBoolean("is_verified");
            }

            if (!BCryptNet.Verify(
                request.Password, passwordHash))
            {
                return Unauthorized(new
                {
                    message = "Invalid email or password."
                });
            }

            

            // GENERATE JWT TOKEN
            string? jwtKey = _configuration["Jwt:Key"];
            string? issuer = _configuration["Jwt:Issuer"];
            string? audience = _configuration["Jwt:Audience"];

            if (string.IsNullOrWhiteSpace(jwtKey) ||
                string.IsNullOrWhiteSpace(issuer) ||
                string.IsNullOrWhiteSpace(audience))
            {
                return StatusCode(500, new
                {
                    message = "JWT configuration is missing."
                });
            }

            var claims = new[]
            {
                new Claim(
                    ClaimTypes.NameIdentifier, id.ToString()),

                new Claim(ClaimTypes.Name, email),
                new Claim(ClaimTypes.Email, email),
                new Claim(ClaimTypes.Role, role),
                
                
            };

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey));

            var credentials = new SigningCredentials(
                key, SecurityAlgorithms.HmacSha256);

            int expiryMinutes = int.TryParse(
                _configuration["Jwt:ExpiryMinutes"],
                out int minutes) && minutes > 0
                    ? minutes
                    : 30;

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
                signingCredentials: credentials);

            string tokenString =
                new JwtSecurityTokenHandler().WriteToken(token);

            return Ok(new
            {
                message = "Login successful!",
                token = tokenString,
                account = new
                {
                    id,
                    firstName,
                    lastName,
                    email,
                    role,
                    isVerified
                }
            });
        }

        // PROTECTED PROFILE
        [Authorize]
        [HttpGet("profile")]
        public IActionResult GetProfile()
        {
            return Ok(new
            {
                message = "You are authenticated!",
                userId = User.FindFirst(
                    ClaimTypes.NameIdentifier)?.Value,
                email = User.FindFirst(
                    ClaimTypes.Email)?.Value,
                role = User.FindFirst(
                    ClaimTypes.Role)?.Value
            });
        }

        [Authorize(Roles = "Admin")]
[HttpGet("admin")]
public IActionResult AdminDashboard()
{
    return Ok(new
    {
        message = "Welcome to the Admin Dashboard!",
        role = User.FindFirst(ClaimTypes.Role)?.Value
    });
}
    }
}
