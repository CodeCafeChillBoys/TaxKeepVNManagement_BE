using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;

namespace TaxKeepVN.Infrastructure.Services
{
    public class JwtTokenService : IJwtTokenService
    {
        private readonly IConfiguration _configuration;

        public JwtTokenService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string GenerateToken(User user)
        {
            var jwtSection = _configuration.GetSection("Jwt");

            var key = jwtSection["Key"]
                ?? throw new InvalidOperationException("JWT Key chưa được cấu hình trong appsettings.json.");

            var issuer = jwtSection["Issuer"] ?? "TaxKeepVN";
            var audience = jwtSection["Audience"] ?? "TaxKeepVN";
            var expiryMinutes = int.Parse(jwtSection["ExpiryMinutes"] ?? "1440");

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("userId", user.UserId.ToString()),
                new Claim("citizenId", user.CitizenId),
                new Claim("fullName", user.FullName),
                new Claim("email", user.Email),
                new Claim(ClaimTypes.Role, user.UserRole),
            };

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
