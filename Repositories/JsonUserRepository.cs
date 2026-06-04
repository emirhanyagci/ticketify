using System.Text.Json;
using Ticketify.Models;

namespace Ticketify.Repositories;

public class JsonUserRepository : IUserRepository
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true
    };

    public JsonUserRepository(IWebHostEnvironment env)
    {
        var dataDir = Path.Combine(env.ContentRootPath, "App_Data");
        _filePath = Path.Combine(dataDir, "users.json");
    }

    private async Task<List<User>> ReadAllAsync()
    {
        if (!File.Exists(_filePath))
            return new List<User>();

        var json = await File.ReadAllTextAsync(_filePath);
        if (string.IsNullOrWhiteSpace(json))
            return new List<User>();

        return JsonSerializer.Deserialize<List<User>>(json, _options) ?? new List<User>();
    }

    private async Task WriteAllAsync(List<User> users)
    {
        var json = JsonSerializer.Serialize(users, _options);
        await File.WriteAllTextAsync(_filePath, json);
    }

    public async Task<IEnumerable<User>> GetAllAsync()
    {
        await _lock.WaitAsync();
        try
        {
            return await ReadAllAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        await _lock.WaitAsync();
        try
        {
            var users = await ReadAllAsync();
            return users.FirstOrDefault(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<User?> GetByIdAsync(int id)
    {
        await _lock.WaitAsync();
        try
        {
            var users = await ReadAllAsync();
            return users.FirstOrDefault(u => u.Id == id);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IEnumerable<User>> GetByRoleAsync(string role)
    {
        await _lock.WaitAsync();
        try
        {
            var users = await ReadAllAsync();
            return users.Where(u => u.Role.Equals(role, StringComparison.OrdinalIgnoreCase))
                        .OrderBy(u => u.FullName);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IEnumerable<User>> GetByDepartmentAsync(string department)
    {
        await _lock.WaitAsync();
        try
        {
            var users = await ReadAllAsync();
            return users.Where(u => u.Role == "Employee" &&
                                    u.Department != null &&
                                    u.Department.Equals(department, StringComparison.OrdinalIgnoreCase))
                        .OrderBy(u => u.FullName);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<User> CreateAsync(User user)
    {
        await _lock.WaitAsync();
        try
        {
            var users = await ReadAllAsync();
            user.Id = users.Count > 0 ? users.Max(u => u.Id) + 1 : 1;
            user.CreatedAt = DateTime.UtcNow;
            users.Add(user);
            await WriteAllAsync(users);
            return user;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<User?> UpdateAsync(User updatedUser)
    {
        await _lock.WaitAsync();
        try
        {
            var users = await ReadAllAsync();
            var index = users.FindIndex(u => u.Id == updatedUser.Id);
            if (index < 0) return null;

            users[index] = updatedUser;
            await WriteAllAsync(users);
            return updatedUser;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> DeleteAsync(int id)
    {
        await _lock.WaitAsync();
        try
        {
            var users = await ReadAllAsync();
            var user = users.FirstOrDefault(u => u.Id == id);
            if (user == null) return false;

            users.Remove(user);
            await WriteAllAsync(users);
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }
}
