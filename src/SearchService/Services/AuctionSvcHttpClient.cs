using System;
using MongoDB.Entities;
using SearchService.Models;

namespace SearchService.Services;

public class AuctionSvcHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;

    public AuctionSvcHttpClient(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;
        _config = config;
    }

    public async Task<List<Item>> GetItemsForSearchDb()
    {
        // Get the last updated time for items
        var lastUpdated = await DB.Find<Item, string>()
            .Sort(x => x.Descending(x => x.UpdatedAt))
            .Project(x => x.UpdatedAt.ToString())
            .ExecuteFirstAsync();

        // Set a default date if lastUpdated is empty or null
        var dateParam = string.IsNullOrEmpty(lastUpdated) ? DateTime.MinValue.ToString("o") : lastUpdated;

        // Send the request to the AuctionService
        var response = await _httpClient.GetAsync(_config["AuctionServiceUrl"] + "/api/auctions?date=" + dateParam);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<List<Item>>();
        }

        throw new HttpRequestException($"Request failed with status code {response.StatusCode}");
    }
}
