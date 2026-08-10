using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DigitalRegistry.Application.Common.Security;
using DigitalRegistry.Domain.Entities;
using DigitalRegistry.Domain.Enums;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DigitalRegistry.Infrastructure.Identity;

/// <summary>
/// Issues HMAC-SHA256 signed JWTs for users and for anonymous table sessions.
/// </summary>
internal sealed class JwtTokenGenerator(IOptions<JwtSettings> settingsAccessor) : IJwtTokenGenerator
{
    private readonly JwtSettings _settings = settingsAccessor.Value;

    public (string Token, DateTime ExpiresAtUtc) GenerateForUser(ApplicationUser user)
    {
        var roleName = user.Role.ToString();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            // Both the framework role claim (so [Authorize(Roles = ...)] and policies work) and our
            // own, so ICurrentUserService can read the role without depending on claim mapping.
            new(ClaimTypes.Role, roleName),
            new(DigitalRegistryClaimTypes.Role, roleName)
        };

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Email, user.Email));
            claims.Add(new Claim(ClaimTypes.Email, user.Email));
        }

        return CreateToken(claims, _settings.AccessTokenLifetimeMinutes);
    }

    public (string Token, DateTime ExpiresAtUtc) GenerateForTableSession(Guid tableId, int tableNumber)
    {
        var roleName = UserRole.Guest.ToString();

        // No NameIdentifier and no user id: the session is anonymous by design. Everything it is
        // allowed to do is derived from the table claim plus the guest role.
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, $"table:{tableId}"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, roleName),
            new(DigitalRegistryClaimTypes.Role, roleName),
            new(DigitalRegistryClaimTypes.TableId, tableId.ToString()),
            new(DigitalRegistryClaimTypes.TableNumber, tableNumber.ToString())
        };

        return CreateToken(claims, _settings.TableSessionLifetimeMinutes);
    }

    private (string Token, DateTime ExpiresAtUtc) CreateToken(IEnumerable<Claim> claims, int lifetimeMinutes)
    {
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var issuedAtUtc = DateTime.UtcNow;
        var expiresAtUtc = issuedAtUtc.AddMinutes(lifetimeMinutes);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: issuedAtUtc,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc);
    }
}
