using Microsoft.Extensions.Options;
using SLT.Services._BlockChain._BlockChainWebSocket.DTOs;
using SLT.Services._BlockChain.DTOs.Settings;
using SLT.Services._Price.DTOs.Settings;


namespace SLT.Api.Utilities.Configurations
{
    public static class ControllerServiceCollectionExtensions
    {
        public static void AddSettings(this IServiceCollection services, IConfiguration configuration)
        {

            services.RegisterSetting<AvailableTokensSettings>(configuration.GetSection(nameof(AvailableTokensSettings)));
            services.RegisterSetting<BlockchainWebSocketSetting>(configuration.GetSection(nameof(BlockchainWebSocketSetting)));
            services.RegisterSetting<BlockChainSettings>(configuration.GetSection(nameof(BlockChainSettings)));
        }

        private static void RegisterSetting<TSettings>(this IServiceCollection services, IConfigurationSection configuration)
           where TSettings : class, new()
        {
            services.Configure<TSettings>(configuration);
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<TSettings>>().Value);
        }

        private static void RegisterSetting<TSettings, TISettings>(this IServiceCollection services, IConfigurationSection configuration)
            where TISettings : class
            where TSettings : class, TISettings, new()
        {
            services.Configure<TSettings>(configuration);
            services.AddSingleton<TISettings>(sp => sp.GetRequiredService<IOptions<TSettings>>().Value);
        }
    }
}
 