namespace FlowCare.Api.Services
{
    public class PasswordHasher : IPasswordHasher
    {
        public string Hash(string password) =>
        BCrypt.Net.BCrypt.HashPassword(password);

        public bool Verify(string password, string passwordHash) =>
            BCrypt.Net.BCrypt.Verify(password, passwordHash);
       

       
    }
    public interface IPasswordHasher
    {
        string Hash(string password);
        bool Verify(string password, string passwordHash);
    }
}
