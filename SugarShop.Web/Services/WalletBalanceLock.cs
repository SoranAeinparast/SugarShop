using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities;
using SugarShop.Infrastructure.Persistence.Sales;

namespace SugarShop.Web.Services;

public static class WalletBalanceLock
{
    public static async Task<Wallet> GetOrCreateForUpdateAsync(SugarShopSalesDbContext db, string userId)
    {
        if (db.Database.CurrentTransaction == null)
            throw new InvalidOperationException("Wallet balance updates require an active transaction.");

        var wallet = await db.Wallets
            .FromSqlInterpolated($"SELECT * FROM [Wallets] WITH (UPDLOCK, HOLDLOCK) WHERE [UserId] = {userId}")
            .SingleOrDefaultAsync();
        if (wallet != null) return wallet;

        wallet = new Wallet { UserId = userId, Balance = 0 };
        db.Wallets.Add(wallet);
        return wallet;
    }
}
