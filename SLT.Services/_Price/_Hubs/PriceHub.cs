using Microsoft.AspNetCore.SignalR;
using SLT.Services._Price.DTOs.Storages;

namespace SLT.Services._Price._Hubs
{
    public class PriceHub : Hub
    {
        private readonly PriceStorage _priceStorage;

        public PriceHub(PriceStorage priceStorage)
        {
            _priceStorage = priceStorage;
        }

        public override async Task OnConnectedAsync()
        {

            await Clients.Caller.SendAsync("NotifyPrice", _priceStorage);
            await base.OnConnectedAsync();
        }

        //public async Task InventoryNotify()
        //{
        //    await Clients.All.SendAsync("NotifyInventory", _priceStorage);
        //}

    }
}
