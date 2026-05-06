using Utilities.Exceptions.Common;
using Microsoft.Extensions.Logging;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.Contracts;
using Nethereum.Util;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using SLT.Services._BlockChain._MultiCallService;
using SLT.Services._BlockChain.DTOs.Settings;
using SLT.Services._Price.DTOs.Settings;
using System.Numerics;
using static Utilities.Constants.RegisterMode;
using SLT.Services._BlockChain.DTOs.Updates;

namespace SLT.Services._BlockChain
{
    public class BlockChainService : IBlockChainService, ISingletonDependency
    {
        private const string ContractAbi = TokenForwardSaleAbi.Value;
        private const string ERC20Abi = TokenForwardSaleAbi.ERC20Abi;
        private readonly BlockChainSettings _settings;
        private readonly ILogger<BlockChainService> _logger;
        private readonly IMultiCallService _multicallService;
        private readonly AvailableTokensSettings _availableTokenData;
        private readonly Web3 _web3;
        private readonly Account _account;
        private readonly Contract _contract;

        public BlockChainService(BlockChainSettings settings,
            ILogger<BlockChainService> logger,
            IMultiCallService multiCallService,
            AvailableTokensSettings availableTokenData)
        {
            _settings = settings;
            _logger = logger;
            _multicallService = multiCallService;
            _availableTokenData = availableTokenData;
            if (string.IsNullOrEmpty(_settings.PrivateKey))
                throw new InvalidOperationException("Blockchain private key is not configured.");

            _account = new Account(_settings.PrivateKey, _settings.ChainId);
            _web3 = new Web3(_account, _settings.RpcUrl2);
            _web3.TransactionManager.UseLegacyAsDefault = true;
        }


        ///// <summary>
        ///// Executes the <c>createQuickInvoice</c> function on the blockchain contract.
        ///// Converts inputs properly, waits for transaction receipt, and returns
        ///// transaction hash if successful; otherwise logs and returns null.
        ///// </summary>
        //public async Task<string> CreateQuickInvoiceAsync(
        //    string id,
        //    string tokenAddress,
        //    decimal usdtAmount,
        //    string ownerAddress)
        //{
        //    if (string.IsNullOrEmpty(id))
        //        throw new BadRequestException("Invoice ID is null or empty.");

        //    if (string.IsNullOrEmpty(tokenAddress))
        //        throw new BadRequestException("Token address is null or empty.");

        //    if (usdtAmount <= 0)
        //        throw new BadRequestException("USDT amount must be greater than zero.");

        //    try
        //    {
        //        var contract = _web3.Eth.GetContract(ContractAbi, _settings.ContractAddress);
        //        var function = contract.GetFunction("createQuickInvoice");

        //        var invoiceIdBytes = HexToByteArray32(id);
        //        var amountInWei = ConvertToWei(usdtAmount, 18);

        //        var gasPrice = await GetOptimalGasPriceAsync();
        //        var gas = new Nethereum.Hex.HexTypes.HexBigInteger(
        //            _settings.GetDefaultGasLimit());

        //        var receipt = await function.SendTransactionAndWaitForReceiptAsync(
        //            from: _account.Address,
        //            gas: gas,
        //            gasPrice: new Nethereum.Hex.HexTypes.HexBigInteger(gasPrice),
        //            value: new Nethereum.Hex.HexTypes.HexBigInteger(0),
        //            functionInput: new object[]
        //            {
        //        invoiceIdBytes,
        //        ownerAddress,
        //        tokenAddress,
        //        amountInWei
        //            }
        //        );

        //        if (receipt.Status.Value == 1)
        //        {
        //            _logger.LogInformation(
        //                "CreateQuickInvoice successful. TxHash: {TxHash}",
        //                receipt.TransactionHash);

        //            return receipt.TransactionHash;
        //        }
        //        else
        //        {
        //            _logger.LogError(
        //                "CreateQuickInvoice failed (reverted). TxHash: {TxHash}",
        //                receipt.TransactionHash);

        //            return null;
        //        }
        //    }
        //    catch (SmartContractRevertException revertEx)
        //    {
        //        _logger.LogError(
        //            revertEx,
        //            "Contract revert error during createQuickInvoice: {Message}",
        //            revertEx.Message);

        //        return null;
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(
        //            ex,
        //            "Unexpected error during createQuickInvoice.");

        //        return null;
        //    }
        //}


        ///// <summary>
        ///// Executes the <c>createOrderedInvoices</c> function on the blockchain contract.
        ///// Registers multiple invoices in a single transaction.
        ///// Returns transaction hash if successful; otherwise logs and returns null.
        ///// </summary>
        //public async Task<string> CreateMultipleInvoicesAsync(
        //    List<CreateMultipleInvoicesUpdate> invoices, string ownerAddress)
        //{
        //    if (invoices == null || invoices.Count == 0)
        //        throw new BadRequestException("Invoices list is empty.");

        //    try
        //    {
        //        var ids = new List<byte[]>();
        //        var tokens = new List<string>();
        //        var usdAmounts = new List<BigInteger>();
        //        var unlockTimes = new List<BigInteger>();

        //        foreach (var invoice in invoices)
        //        {
        //            if (string.IsNullOrEmpty(invoice.Id))
        //                throw new BadRequestException("Invoice ID is null or empty.");

        //            if (string.IsNullOrEmpty(invoice.TokenAddress))
        //                throw new BadRequestException("Token address is null or empty.");

        //            if (invoice.USDTAmount <= 0)
        //                throw new BadRequestException("USDT amount must be greater than zero.");

        //            ids.Add(HexToByteArray32(invoice.Id));
        //            tokens.Add(invoice.TokenAddress);
        //            usdAmounts.Add(ConvertToWei(invoice.USDTAmount, 18));

        //            unlockTimes.Add(
        //                new BigInteger(
        //                    new DateTimeOffset(invoice.UnLockTime).ToUnixTimeSeconds()
        //                )
        //            );
        //        }

        //        var contract = _web3.Eth.GetContract(ContractAbi, _settings.ContractAddress);
        //        var function = contract.GetFunction("createOrderedInvoices");

        //        var gasPrice = await GetOptimalGasPriceAsync();
        //        var gas = new Nethereum.Hex.HexTypes.HexBigInteger(
        //            _settings.GetDefaultGasLimit());

        //        var receipt = await function.SendTransactionAndWaitForReceiptAsync(
        //            from: _account.Address,
        //            gas: gas,
        //            gasPrice: new Nethereum.Hex.HexTypes.HexBigInteger(gasPrice),
        //            value: new Nethereum.Hex.HexTypes.HexBigInteger(0),
        //            functionInput: new object[]
        //            {
        //        ownerAddress,
        //        ids.ToArray(),
        //        tokens.ToArray(),
        //        usdAmounts.ToArray(),
        //        unlockTimes.ToArray()
        //            }
        //        );

        //        if (receipt.Status.Value == 1)
        //        {
        //            _logger.LogInformation(
        //                "CreateMultipleInvoices successful. TxHash: {TxHash}",
        //                receipt.TransactionHash);

        //            return receipt.TransactionHash;
        //        }
        //        else
        //        {
        //            _logger.LogError(
        //                "CreateMultipleInvoices failed (reverted). TxHash: {TxHash}",
        //                receipt.TransactionHash);

        //            return null;
        //        }
        //    }
        //    catch (SmartContractRevertException revertEx)
        //    {
        //        _logger.LogError(
        //            revertEx,
        //            "Contract revert error during createOrderedInvoices: {Message}",
        //            revertEx.Message);

        //        return null;
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(
        //            ex,
        //            "Unexpected error during createOrderedInvoices.");

        //        return null;
        //    }
        //}


        /// <summary>
        /// use for delete multiple invoices 
        /// </summary>
        /// <param name="invoicesId"></param>
        /// <returns></returns>
        /// <exception cref="BadRequestException"></exception>
        public async Task<string> DeleteMultipleInvoicesAsync(List<string> invoicesId)
        {
            if (invoicesId == null || invoicesId.Count == 0)
                throw new BadRequestException("Invoice IDs list is empty.");

            try
            {
                var ids = new List<byte[]>();

                foreach (var id in invoicesId)
                {
                    if (string.IsNullOrEmpty(id))
                        throw new BadRequestException("Invoice ID is null or empty.");

                    ids.Add(HexToByteArray32(id));
                }

                var contract = _web3.Eth.GetContract(ContractAbi, _settings.ContractAddress);
                var function = contract.GetFunction("deleteBatchInvoice");

                var gasPrice = await GetOptimalGasPriceAsync();
                var gas = new Nethereum.Hex.HexTypes.HexBigInteger(
                    _settings.GetDefaultGasLimit());

                var receipt = await function.SendTransactionAndWaitForReceiptAsync(
                    from: _account.Address,
                    gas: gas,
                    gasPrice: new Nethereum.Hex.HexTypes.HexBigInteger(gasPrice),
                    value: new Nethereum.Hex.HexTypes.HexBigInteger(0),
                    functionInput: new object[]
                    {
                        ids.ToArray()
                    }
                );

                if (receipt.Status.Value == 1)
                {
                    _logger.LogInformation(
                        "DeleteMultipleInvoices successful. TxHash: {TxHash}",
                        receipt.TransactionHash);

                    return receipt.TransactionHash;
                }
                else
                {
                    _logger.LogError(
                        "DeleteMultipleInvoices failed (reverted). TxHash: {TxHash}",
                        receipt.TransactionHash);

                    return null;
                }
            }
            catch (SmartContractRevertException revertEx)
            {
                _logger.LogError(
                    revertEx,
                    "Contract revert error during deleteBatchInvoice: {Message}",
                    revertEx.Message);

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Unexpected error during deleteBatchInvoice.");

                return null;
            }
        }


        /// <summary>
        /// use for delete single invoice
        /// </summary>
        /// <param name="invoiceId"></param>
        /// <returns></returns>
        /// <exception cref="BadRequestException"></exception>
        public async Task<string> DeleteSingleInvoiceAsync(string invoiceId)
        {
            if (string.IsNullOrEmpty(invoiceId))
                throw new BadRequestException("Invoice ID is null or empty.");

            try
            {
                var id = HexToByteArray32(invoiceId);

                var contract = _web3.Eth.GetContract(ContractAbi, _settings.ContractAddress);
                var function = contract.GetFunction("deleteInvoice");

                var gasPrice = await GetOptimalGasPriceAsync();
                var gas = new Nethereum.Hex.HexTypes.HexBigInteger(
                    _settings.GetDefaultGasLimit());

                var receipt = await function.SendTransactionAndWaitForReceiptAsync(
                    from: _account.Address,
                    gas: gas,
                    gasPrice: new Nethereum.Hex.HexTypes.HexBigInteger(gasPrice),
                    value: new Nethereum.Hex.HexTypes.HexBigInteger(0),
                    functionInput: new object[]
                    {
                id
                    }
                );

                if (receipt.Status.Value == 1)
                {
                    _logger.LogInformation(
                        "DeleteSingleInvoice successful. TxHash: {TxHash}",
                        receipt.TransactionHash);

                    return receipt.TransactionHash;
                }
                else
                {
                    _logger.LogError(
                        "DeleteSingleInvoice failed (reverted). TxHash: {TxHash}",
                        receipt.TransactionHash);

                    return null;
                }
            }
            catch (SmartContractRevertException revertEx)
            {
                _logger.LogError(
                    revertEx,
                    "Contract revert error during deleteInvoice: {Message}",
                    revertEx.Message);

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Unexpected error during deleteInvoice.");

                return null;
            }
        }


        private AvailableTokenData ValidateToken(string tokenName)
        {

            if (tokenName == null)
                throw new BadRequestException($"Unsupported token name! {tokenName}");

            var tokenData = _availableTokenData.FirstOrDefault(q => q.Name.Equals(tokenName, StringComparison.OrdinalIgnoreCase))
                ?? throw new BadRequestException($"Unsupported token name! {tokenName}");
            return tokenData;
        }


        #region Utility Methods (Unchanged)
        public BigInteger ConvertToWei(decimal amount, int decimals = 18)
        {
            if (amount < 0) throw new ArgumentException("Amount must be a positive number.");
            var factor = BigInteger.Pow(10, decimals);
            return (BigInteger)(amount * (decimal)factor);
        }

        public decimal ConvertFromWei(BigInteger weiAmount, int decimals = 18)
        {
            if (weiAmount < 0) throw new ArgumentException("Amount must be a positive integer.");
            var factor = (decimal)BigInteger.Pow(10, decimals);
            return (decimal)weiAmount / factor;
        }


        public static byte[] HexToByteArray32(string hex)
        {
            if (string.IsNullOrEmpty(hex))
                throw new ArgumentException("Hex string is null or empty");

            var bytes = Nethereum.Hex.HexConvertors.Extensions.HexByteConvertorExtensions.HexToByteArray(hex);

            if (bytes.Length > 32)
                throw new ArgumentException("Hex string is too long for bytes32");

            var padded = new byte[32];
            Array.Copy(bytes, 0, padded, 32 - bytes.Length, bytes.Length);

            return padded;
        }

        private async Task<BigInteger> GetOptimalGasPriceAsync()
        {
            try
            {
                var currentGasPrice = await _web3.Eth.GasPrice.SendRequestAsync();

                var suggestedGasPrice = (BigInteger)((decimal)currentGasPrice.Value * 1.2m);

                var minGasPrice = UnitConversion.Convert.ToWei(_settings.GetMinGasPriceGwei(), UnitConversion.EthUnit.Gwei);
                var maxGasPrice = UnitConversion.Convert.ToWei(_settings.GetMaxGasPriceGwei(), UnitConversion.EthUnit.Gwei);

                var optimalPrice = BigInteger.Min(BigInteger.Max(suggestedGasPrice, minGasPrice), maxGasPrice);

                _logger.LogInformation($"Using gas price: {UnitConversion.Convert.FromWei(optimalPrice, UnitConversion.EthUnit.Gwei)} Gwei");
                return optimalPrice;
            }
            catch
            {
                var defaultPrice = UnitConversion.Convert.ToWei(_settings.GetDefaultGasPriceGwei(), UnitConversion.EthUnit.Gwei);
                _logger.LogWarning($"Using DEFAULT gas price: {_settings.GetDefaultGasPriceGwei()} Gwei");
                return defaultPrice;
            }
        }





        #endregion
    }
}
