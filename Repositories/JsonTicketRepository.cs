using System.Text.Json;
using Ticketify.Models;

namespace Ticketify.Repositories;

public class JsonTicketRepository : ITicketRepository
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true
    };

    public JsonTicketRepository(IWebHostEnvironment env)
    {
        var dataDir = Path.Combine(env.ContentRootPath, "App_Data");
        _filePath = Path.Combine(dataDir, "tickets.json");
    }

    private async Task<List<Ticket>> ReadAllAsync()
    {
        if (!File.Exists(_filePath))
            return new List<Ticket>();

        var json = await File.ReadAllTextAsync(_filePath);
        if (string.IsNullOrWhiteSpace(json))
            return new List<Ticket>();

        return JsonSerializer.Deserialize<List<Ticket>>(json, _options) ?? new List<Ticket>();
    }

    private async Task WriteAllAsync(List<Ticket> tickets)
    {
        var json = JsonSerializer.Serialize(tickets, _options);
        await File.WriteAllTextAsync(_filePath, json);
    }

    public async Task<IEnumerable<Ticket>> GetAllAsync()
    {
        await _lock.WaitAsync();
        try
        {
            var tickets = await ReadAllAsync();
            return tickets.OrderByDescending(t => t.CreatedAt);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IEnumerable<Ticket>> GetByUserIdAsync(int userId)
    {
        await _lock.WaitAsync();
        try
        {
            var tickets = await ReadAllAsync();
            return tickets.Where(t => t.UserId == userId).OrderByDescending(t => t.CreatedAt);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Ticket?> GetByIdAsync(int id)
    {
        await _lock.WaitAsync();
        try
        {
            var tickets = await ReadAllAsync();
            return tickets.FirstOrDefault(t => t.Id == id);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Ticket> CreateAsync(Ticket ticket)
    {
        await _lock.WaitAsync();
        try
        {
            var tickets = await ReadAllAsync();
            ticket.Id = tickets.Count > 0 ? tickets.Max(t => t.Id) + 1 : 1;
            ticket.CreatedAt = DateTime.UtcNow;
            ticket.Status = "Open";
            tickets.Add(ticket);
            await WriteAllAsync(tickets);
            return ticket;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Ticket?> UpdateAsync(Ticket updatedTicket)
    {
        await _lock.WaitAsync();
        try
        {
            var tickets = await ReadAllAsync();
            var index = tickets.FindIndex(t => t.Id == updatedTicket.Id);
            if (index < 0) return null;

            tickets[index] = updatedTicket;
            await WriteAllAsync(tickets);
            return updatedTicket;
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
            var tickets = await ReadAllAsync();
            var ticket = tickets.FirstOrDefault(t => t.Id == id);
            if (ticket == null) return false;

            tickets.Remove(ticket);
            await WriteAllAsync(tickets);
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }
}
