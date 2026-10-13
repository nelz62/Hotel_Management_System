
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using csboilerplate.Helpers;
using System.Security.Claims;

namespace csboilerplate.Controllers;

[ApiController]
[Route("api/usermanagement")]
[Authorize(Roles = "Admin")]
public class UserManagementController : ControllerBase
{
    private readonly DataContext _context;

    public UserManagementController(DataContext context)
    {
        _context = context;
    }

    // GET: api/usermanagement
    [HttpGet]
    public async Task<IActionResult> GetAllUsers()
    {
        var users = new List<object>();

        await using var connection = _context.CreateConnection();
        await connection.OpenAsync();

        const string sql = @"
            SELECT id, first_name, last_name, email, role
            FROM accounts
            ORDER BY id";

        await using var command = new MySqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            users.Add(new
            {
                id = reader.GetInt32("id"),
                firstName = reader.GetString("first_name"),
                lastName = reader.GetString("last_name"),
                email = reader.GetString("email"),
                role = reader.GetString("role")
            });
        }

        return Ok(users);
    }

    // GET: api/usermanagement/{id}
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetUserById(int id)
    {
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync();

        const string sql = @"
            SELECT id, first_name, last_name, email, role
            FROM accounts
            WHERE id = @id";

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);

        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            return NotFound(new { message = "Account not found." });

        return Ok(new
        {
            id = reader.GetInt32("id"),
            firstName = reader.GetString("first_name"),
            lastName = reader.GetString("last_name"),
            email = reader.GetString("email"),
            role = reader.GetString("role")
        });
    }

    // PUT: api/usermanagement/{id}
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateUser(
        int id, [FromBody] UpdateUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName) ||
            string.IsNullOrWhiteSpace(request.LastName) ||
            string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new
            {
                message = "First name, last name, and email are required."
            });
        }

        await using var connection = _context.CreateConnection();
        await connection.OpenAsync();

        const string sql = @"
            UPDATE accounts
            SET first_name = @firstName,
                last_name = @lastName,
                email = @email
            WHERE id = @id";

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@firstName", request.FirstName.Trim());
        command.Parameters.AddWithValue("@lastName", request.LastName.Trim());
        command.Parameters.AddWithValue("@email", request.Email.Trim());
        command.Parameters.AddWithValue("@id", id);

        try
        {
            int affected = await command.ExecuteNonQueryAsync();

            if (affected == 0)
            {
                // Check whether the account exists, since unchanged
                // values can also produce zero affected rows.
                const string checkSql =
                    "SELECT COUNT(*) FROM accounts WHERE id = @id";

                await using var check = new MySqlCommand(checkSql, connection);
                check.Parameters.AddWithValue("@id", id);

                var exists = Convert.ToInt32(await check.ExecuteScalarAsync());

                if (exists == 0)
                    return NotFound(new { message = "Account not found." });
            }
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            return Conflict(new { message = "That email is already in use." });
        }

        return Ok(new { message = "Account updated successfully." });
    }

    // PUT: api/usermanagement/{id}/role
    [HttpPut("{id:int}/role")]
    public async Task<IActionResult> UpdateUserRole(
        int id, [FromBody] UpdateRoleRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Role) ||
            !(request.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase) ||
              request.Role.Equals("User", StringComparison.OrdinalIgnoreCase)))
        {
            return BadRequest(new
            {
                message = "Role must be either Admin or User."
            });
        }

        // Prevent an Admin from removing their own Admin role.
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (int.TryParse(currentUserId, out int loggedInId) &&
            loggedInId == id &&
            request.Role.Equals("User", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message = "You cannot remove your own Admin role."
            });
        }

        await using var connection = _context.CreateConnection();
        await connection.OpenAsync();

        const string sql =
            "UPDATE accounts SET role = @role WHERE id = @id";

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue(
            "@role",
            request.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase)
                ? "Admin"
                : "User");
        command.Parameters.AddWithValue("@id", id);

        int affected = await command.ExecuteNonQueryAsync();

        if (affected == 0)
        {
            const string checkSql =
                "SELECT COUNT(*) FROM accounts WHERE id = @id";

            await using var check = new MySqlCommand(checkSql, connection);
            check.Parameters.AddWithValue("@id", id);

            if (Convert.ToInt32(await check.ExecuteScalarAsync()) == 0)
                return NotFound(new { message = "Account not found." });
        }

        return Ok(new
        {
            message = "Account role updated. The affected account must log in again to receive a token with the new role."
        });
    }

    // DELETE: api/usermanagement/{id}
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (int.TryParse(currentUserId, out int loggedInId) &&
            loggedInId == id)
        {
            return BadRequest(new
            {
                message = "You cannot delete your own account while logged in."
            });
        }

        await using var connection = _context.CreateConnection();
        await connection.OpenAsync();

        const string sql = "DELETE FROM accounts WHERE id = @id";

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);

        try
        {
            int affected = await command.ExecuteNonQueryAsync();

            if (affected == 0)
                return NotFound(new { message = "Account not found." });
        }
        catch (MySqlException ex) when (ex.Number == 1451)
        {
            return Conflict(new
            {
                message = "This account is referenced by other records and cannot be deleted."
            });
        }

        return Ok(new { message = "Account deleted successfully." });
    }
}

public class UpdateUserRequest
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
}

public class UpdateRoleRequest
{
    public string Role { get; set; } = "";
}
