using AuctionService.DTOs;
using AuctionService.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuctionService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuctionsController(AuctionDbContext context) : ControllerBase
{
    private readonly AuctionDbContext _context = context;

    [HttpGet]
    public async Task<ActionResult<List<AuctionDto>>> GetAllAuctions()
    {
        var auctions = await _context.Auctions.Include(x => x.Item).OrderBy(x => x.Item.Make).ToListAsync();
        return auctions.ToDtoList();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<AuctionDto>> GetAuctionById(Guid id)
    {
        var auction = await _context.Auctions.Include(x => x.Item).FirstOrDefaultAsync(x => x.Id == id);

        if (auction == null) return NotFound();

        return auction.ToDto();
    }

    [HttpPost]
    public async Task<ActionResult<AuctionDto>> CreateAuction(CreateAuctionDto auctionDto)
    {
        var auction = auctionDto.ToEntity();
        auction.Seller = "test";
        _context.Auctions.Add(auction);
        var result = await _context.SaveChangesAsync() > 0;
        if (!result) return BadRequest("Could not save changes to the DB");

        return CreatedAtAction(nameof(GetAuctionById), new { id = auction.Id }, auction.ToDto());
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<AuctionDto>> UpdateAuction(Guid id, UpdateAuctionDto auctionDto)
    {
        var auction = await _context.Auctions.Include(x => x.Item).FirstOrDefaultAsync(x => x.Id == id);
        if (auction == null) return NotFound();
        auction.Item.Make = auctionDto.Make ?? auction.Item.Make;
        auction.Item.Model = auctionDto.Model ?? auction.Item.Model;
        auction.Item.Color = auctionDto.Color ?? auction.Item.Color;
        auction.Item.Year = auctionDto.Year ?? auction.Item.Year;
        auction.Item.Mileage = auctionDto.Mileage ?? auction.Item.Mileage;
        var result = await _context.SaveChangesAsync() > 0;

        if (!result) return BadRequest("Could not save changes to the DB");

        return Ok();
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteAuction(Guid id)
    {
        var auction = await _context.Auctions.FindAsync(id);
        if (auction == null) return NotFound();

        _context.Auctions.Remove(auction);

        var result = await _context.SaveChangesAsync() > 0;
        if (!result) return BadRequest("Could not save changes to the DB");
        return Ok();
    }

}

