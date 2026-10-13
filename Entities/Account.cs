


namespace csboilerplate.Entities
{
    public class Account
    {
        public int Id { get; set; }

        public string FirstName { get; set; } = string.Empty;

        public string LastName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;

        public string Role { get; set; } = "User";

        public bool IsVerified { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
