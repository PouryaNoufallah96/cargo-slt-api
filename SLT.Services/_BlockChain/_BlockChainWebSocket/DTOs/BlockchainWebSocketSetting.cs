namespace SLT.Services._BlockChain._BlockChainWebSocket.DTOs
{
    public class BlockchainWebSocketSetting
    {
        public string WsUrl { get; set; }
        public string WsUrl2 { get; set; }
        public string ERC20WsUrl { get; set; }
        public string ContractAddress { get; set; }
        public string ERC20ContractAddress { get; set; }
        public int ReconnectInterval { get; set; } = 5;
        public int MaxReconnectAttempts { get; set; } = 30;
        public int HeartbeatInterval { get; set; } = 30;
        public int ConnectionTimeout { get; set; } = 10;
        public int SubscriptionTimeout { get; set; } = 30;
    }
}
