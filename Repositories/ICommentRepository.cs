using Ticketify.Models;

namespace Ticketify.Repositories;

public interface ICommentRepository
{
    Task<IEnumerable<Comment>> GetByTicketIdAsync(int ticketId);
    Task<Comment> CreateAsync(Comment comment);
    Task<bool> DeleteAsync(int id);
}
