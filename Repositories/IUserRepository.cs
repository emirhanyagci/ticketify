using Ticketify.Models;

namespace Ticketify.Repositories;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByIdAsync(int id);
    Task<User> CreateAsync(User user);
    Task<IEnumerable<User>> GetAllAsync();
}
