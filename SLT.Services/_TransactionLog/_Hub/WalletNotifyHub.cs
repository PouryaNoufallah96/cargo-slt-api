using Microsoft.AspNetCore.SignalR;

namespace SLT.Services._TransactionLog._Hub
{
    public class WalletNotifyHub : Hub
    {
        public override async Task OnConnectedAsync()
        {

            await base.OnConnectedAsync();
        }

        public async Task RegisterWallet(string walletAddress)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, walletAddress);
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await base.OnDisconnectedAsync(exception);
        }

    }
}