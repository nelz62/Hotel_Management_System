
using MySqlConnector;

namespace csboilerplate.Helpers
{
    public class DataContext
    {
        private readonly IConfiguration _configuration;

        public DataContext(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public MySqlConnection CreateConnection()
        {
            string? connectionString =
                _configuration.GetConnectionString("DefaultConnection");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "The database connection string is missing.");
            }

            return new MySqlConnection(connectionString);
        }
    }
}
