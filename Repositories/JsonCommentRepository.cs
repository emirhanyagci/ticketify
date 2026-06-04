using System.Text.Json;
using Ticketify.Models;

namespace Ticketify.Repositories;

public class JsonCommentRepository : ICommentRepository
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true
    };

    public JsonCommentRepository(IWebHostEnvironment env)
    {
        var dataDir = Path.Combine(env.ContentRootPath, "App_Data");
        _filePath = Path.Combine(dataDir, "comments.json");
    }

    private async Task<List<Comment>> ReadAllAsync()
    {
        if (!File.Exists(_filePath))
            return new List<Comment>();

        var json = await File.ReadAllTextAsync(_filePath);
        if (string.IsNullOrWhiteSpace(json))
            return new List<Comment>();

        return JsonSerializer.Deserialize<List<Comment>>(json, _options) ?? new List<Comment>();
    }

    private async Task WriteAllAsync(List<Comment> comments)
    {
        var json = JsonSerializer.Serialize(comments, _options);
        await File.WriteAllTextAsync(_filePath, json);
    }

    public async Task<IEnumerable<Comment>> GetByTicketIdAsync(int ticketId)
    {
        await _lock.WaitAsync();
        try
        {
            var comments = await ReadAllAsync();
            return comments
                .Where(c => c.TicketId == ticketId)
                .OrderBy(c => c.CreatedAt);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Comment> CreateAsync(Comment comment)
    {
        await _lock.WaitAsync();
        try
        {
            var comments = await ReadAllAsync();
            comment.Id = comments.Count > 0 ? comments.Max(c => c.Id) + 1 : 1;
            comment.CreatedAt = DateTime.UtcNow;
            comments.Add(comment);
            await WriteAllAsync(comments);
            return comment;
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
            var comments = await ReadAllAsync();
            var comment = comments.FirstOrDefault(c => c.Id == id);
            if (comment == null) return false;

            comments.Remove(comment);
            await WriteAllAsync(comments);
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }
}
