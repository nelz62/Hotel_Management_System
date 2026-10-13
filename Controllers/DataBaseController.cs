
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using csboilerplate.Helpers;

namespace csboilerplate.Controllers
{
    [ApiController]
    [Route("api/database")]
    public class DatabaseTestController : ControllerBase
    {
        private readonly DataContext _dataContext;

        public DatabaseTestController(DataContext dataContext)
        {
            _dataContext = dataContext;
        }

        [HttpGet("test")]
        public async Task<IActionResult> TestConnection()
        {
            try
            {
                await using MySqlConnection connection =
                    _dataContext.CreateConnection();

                await connection.OpenAsync();

                await using MySqlCommand command =
                    new MySqlCommand("SELECT VERSION();", connection);

                object? version = await command.ExecuteScalarAsync();

                return Ok(new
                {
                    message = "MySQL connection successful!",
                    mysqlVersion = version?.ToString()
                });
            }
            catch (MySqlException)
            {
                return StatusCode(500, new
                {
                    message = "Could not connect to MySQL. Check your database settings."
                });
            }
        }
    }
}
