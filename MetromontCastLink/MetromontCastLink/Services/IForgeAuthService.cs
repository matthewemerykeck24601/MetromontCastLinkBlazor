// MetromontCastLink/MetromontCastLink/Services/IForgeAuthService.cs
using System.Threading.Tasks;

namespace MetromontCastLink.Services
{
    public interface IForgeAuthService
    {
        Task<string?> Get2LeggedTokenAsync();
        Task<string?> Get3LeggedTokenAsync(string authorizationCode);
        Task<string?> RefreshTokenAsync(string refreshToken);
    }
}