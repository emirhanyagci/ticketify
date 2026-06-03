using Microsoft.AspNetCore.Identity;
using Ticketify.Models;

namespace Ticketify.Services;

public class AuthService
{
    private readonly PasswordHasher<User> _hasher = new PasswordHasher<User>();

    public string HashPassword(string password)
    {
        // Dummy user instance (PasswordHasher<T> doesn't actually use the user for hashing)
        return _hasher.HashPassword(new User(), password);
    }

    public bool VerifyPassword(User user, string password)
    {
        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        return result == PasswordVerificationResult.Success ||
               result == PasswordVerificationResult.SuccessRehashNeeded;
    }
}
