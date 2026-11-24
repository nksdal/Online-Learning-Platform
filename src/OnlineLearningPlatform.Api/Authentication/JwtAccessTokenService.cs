using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using OnlineLearningPlatform.Application.Abstractions;

namespace OnlineLearningPlatform.Api.Authentication;

public sealed class JwtAccessTokenService(IOptions<JwtOptions> options, TimeProvider timeProvider) : IAccessTokenService
{
    public IssuedAccessToken Create(
        Guid userId,
        string email,
        IReadOnlyCollection<string> roles,
        bool mustChangePassword = false,
        string? securityStamp = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentNullException.ThrowIfNull(roles);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var jwtOptions = options.Value;
        var expiresAtUtc = now.AddMinutes(jwtOptions.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(JwtRegisteredClaimNames.Iat, new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
        };
        claims.AddRange(roles.Select(role => new Claim("role", role)));
        if (mustChangePassword)
        {
            claims.Add(new Claim("must_change_password", "true"));
        }
        if (!string.IsNullOrWhiteSpace(securityStamp))
        {
            claims.Add(new Claim("security_stamp", securityStamp));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(jwtOptions.Issuer, jwtOptions.Audience, claims, now, expiresAtUtc, credentials);
        var token = new JwtSecurityTokenHandler().WriteToken(jwt);

        return new IssuedAccessToken(token, expiresAtUtc);
    }
}
