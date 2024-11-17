using System;
using AuctionService.Data;
using AuctionService.DTO;
using AuctionService.Entities;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Contracts;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuctionService.Controllers;

[ApiController]
[Route("api/auctions")]
public class AuctionsController:ControllerBase
{
    private readonly AuctionDbContext _context;
    private readonly IMapper _mapper;
    private readonly IPublishEndpoint _publishEndpoint;

    public AuctionsController(AuctionDbContext context, IMapper mapper, 
            IPublishEndpoint publishEndpoint)
    {
        _context = context;
        _mapper = mapper;
        _publishEndpoint = publishEndpoint;
    }

    [HttpGet]
    public async Task<ActionResult<List<AuctionDto>>> GetAllAuctions(string date)
    {
        var query = _context.Auctions.OrderBy(x => x.Item.Make).AsQueryable();

        // Check if date is provided and valid
        if (!string.IsNullOrEmpty(date))
        {
            if (DateTime.TryParse(date, out DateTime parsedDate))
            {
                query = query.Where(x => x.UpdatedAt.CompareTo(parsedDate.ToUniversalTime()) > 0);
            }
            else
            {
                return BadRequest(new { error = "Invalid date format. Please provide a valid date." });
            }
        }

        var auctions = await query.ProjectTo<AuctionDto>(_mapper.ConfigurationProvider).ToListAsync();
        return Ok(auctions);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<AuctionDto>> GetAuctionById(Guid id)
    {
        var auction = await _context.Auctions
        .Include(x=>x.Item)
        .FirstOrDefaultAsync(x=>x.Id == id);
        if (auction == null)
        {
            return NotFound();
        }
        return _mapper.Map<AuctionDto>(auction);
    }

    [HttpPost]
    public async Task<ActionResult<AuctionDto>> CreateAuction(CreateAuctionDto auctionDto)
    {
        var auction = _mapper.Map<Auction>(auctionDto);
        // Todo: add current user as seller // Authentication Part
        auction.Seller = "test";

        _context.Auctions.Add(auction);

        var newAuction = _mapper.Map<AuctionDto>(auction);
        
        await _publishEndpoint.Publish(_mapper.Map<AuctionCreated>(newAuction));

        var result = await _context.SaveChangesAsync() > 0;// if nothing saved our db it sends 0

        if(!result) return BadRequest("Could not save changes to the Db");
        
        return CreatedAtAction(nameof(GetAuctionById), 
            new{auction.Id}, newAuction);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> UpdateAuction(Guid id, UpdateAuctionDto updateAuctionDto)
    {
        var auction = await _context.Auctions.Include(x=>x.Item)
        .FirstOrDefaultAsync(x=>x.Id==id);
        if(auction == null) return NotFound();
        //Todo: (Identity Wee need) check seller == username
        auction.Item.Make = updateAuctionDto.Make??auction.Item.Make;
        //!!!!!  if updateAucctionDto.Make is not Null then acution.item.make = updateAuctionDto else auction.Item.Make= auction.Item.make
        auction.Item.Model = updateAuctionDto.Model??auction.Item.Model;
        auction.Item.Color = updateAuctionDto.Color??auction.Item.Color;
        auction.Item.Mileage = updateAuctionDto.Mileage??auction.Item.Mileage;
        auction.Item.Year = updateAuctionDto.Year??auction.Item.Year;
        await _publishEndpoint.Publish(_mapper.Map<AuctionUpdated>(auction));
        var result = await _context.SaveChangesAsync()>0;
        if(result) return Ok();
        return BadRequest("Problem saving changes");
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteAuction(Guid id)
    {
        var auction = await _context.Auctions.FindAsync(id);
        if(auction == null) return NotFound();
        //Todo: check seller== username
        _context.Auctions.Remove(auction);
        await _publishEndpoint.Publish<AuctionDeleted>(new {Id = auction.Id.ToString()});
        var result = await _context.SaveChangesAsync()>0;
        if (!result) return BadRequest("Could not update Db");
        return Ok();
    }
}
