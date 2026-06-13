using Carsties.AuctionService.Entities;
using Microsoft.EntityFrameworkCore;

namespace Carsties.AuctionService.Data;

public class AuctionDbContext : DbContext
{
    public AuctionDbContext(DbContextOptions options) : base(options)
    {
    }

    public DbSet<Auction> Auctions { get; set; }
}
