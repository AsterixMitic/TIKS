using NppCore.Models;

namespace NppCore.Services.Features.Player;

public interface IPlayerService
{
    Task<PlayerEntity> CreateAsync(string username, string email, string? avatarUrl = null);
    Task<PlayerEntity?> GetByIdAsync(Guid playerId);
    Task<PlayerEntity?> GetByEmailAsync(string email);
    Task<PlayerEntity?> GetByUsernameAsync(string username);
    Task<PlayerEntity?> UpdateAsync(Guid playerId, string? username, string? avatarUrl);
    Task<bool> DeleteAsync(Guid playerId);
}
