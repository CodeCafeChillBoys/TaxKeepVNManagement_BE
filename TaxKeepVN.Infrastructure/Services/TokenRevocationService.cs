using System;
using System.Linq;
using System.Threading.Tasks;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.IRepositories;

namespace TaxKeepVN.Infrastructure.Services
{
    public class TokenRevocationService : ITokenRevocationService
    {
        private readonly IUnitOfWork _unitOfWork;

        public TokenRevocationService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task RevokeAsync(string jti, DateTimeOffset expiresAt)
        {
            var revokedToken = new RevokedToken
            {
                Id = Guid.NewGuid(),
                Jti = jti,
                ExpiresAt = expiresAt,
                RevokedAt = DateTimeOffset.UtcNow
            };

            await _unitOfWork.Repository<RevokedToken>().AddAsync(revokedToken);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task<bool> IsRevokedAsync(string jti)
        {
            var matches = await _unitOfWork.Repository<RevokedToken>()
                .FindAsync(t => t.Jti == jti && t.ExpiresAt > DateTimeOffset.UtcNow);

            return matches.Any();
        }
    }
}
