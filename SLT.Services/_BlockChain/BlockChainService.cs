using Microsoft.Extensions.Logging;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.Model;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using SLT.Services._BlockChain._MultiCallService;
using SLT.Services._BlockChain._MultiCallService.DTOs;
using SLT.Services._BlockChain.DTOs.Settings;
using SLT.Services._BlockChain.DTOs.Updates;
using SLT.Services._Price.DTOs.Settings;
using System.Numerics;
using Utilities.Exceptions.Common;
using static Utilities.Constants.RegisterMode;

namespace SLT.Services._BlockChain
{
    public class BlockChainService : IBlockChainService, ISingletonDependency
    {
        private const string ContractAbi = TokenForwardSaleAbi.Value;
        private const string ERC20Abi = TokenForwardSaleAbi.ERC20Abi;
        private const string StakeAbi = TokenForwardSaleAbi.StakeAbi;
        private readonly BlockChainSettings _settings;
        private readonly ILogger<BlockChainService> _logger;
        private readonly IMultiCallService _multicallService;
        private readonly AvailableTokensSettings _availableTokenData;
        private readonly Web3 _bep20Web3;
        private readonly Web3 _erc20Web3;
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
            _bep20Web3 = new Web3(_account, _settings.RpcUrl2);
            _erc20Web3 = new Web3(_settings.ERC20RpcUrl);
            _bep20Web3.TransactionManager.UseLegacyAsDefault = true;
        }

        [FunctionOutput]
        public class GetInvoiceOutputDTO : IFunctionOutputDTO
        {
            [Parameter("tuple", "", 1)]
            public GetInvoiceTupleDTO Invoice { get; set; }
        }

        public class GetInvoiceTupleDTO
        {
            [Parameter("bytes32", "invoiceId", 1)]
            public byte[] InvoiceId { get; set; }

            [Parameter("address", "creator", 2)]
            public string Creator { get; set; }

            [Parameter("address", "payer", 3)]
            public string Payer { get; set; }

            [Parameter("address", "token", 4)]
            public string Token { get; set; }

            [Parameter("uint256", "usdAmount", 5)]
            public BigInteger UsdAmount { get; set; }

            [Parameter("uint256", "payAmount", 6)]
            public BigInteger PayAmount { get; set; }

            [Parameter("uint256", "unlockTime", 7)]
            public BigInteger UnlockTime { get; set; }

            [Parameter("uint256", "lockDuration", 8)]
            public BigInteger LockDuration { get; set; }

            [Parameter("address", "approver", 9)]
            public string Approver { get; set; }

            [Parameter("uint256", "lockedUntil", 10)]
            public BigInteger LockedUntil { get; set; }

            [Parameter("uint256", "stakedPayout", 11)]
            public BigInteger StakedPayout { get; set; }

            [Parameter("uint256", "profitClaimed", 12)]
            public BigInteger ProfitClaimed { get; set; }

            [Parameter("bool", "approved", 13)]
            public bool Approved { get; set; }

            [Parameter("bool", "settled", 14)]
            public bool Settled { get; set; }
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

                var contract = _bep20Web3.Eth.GetContract(ContractAbi, _settings.ContractAddress);
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
        /// use for delete multiple invoices 
        /// </summary>
        /// <param name="invoicesId"></param>
        /// <returns></returns>
        /// <exception cref="BadRequestException"></exception>
        public async Task<string> DeleteERC20MultipleInvoicesAsync(List<string> invoicesId)
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

                var contract = _bep20Web3.Eth.GetContract(ContractAbi, _settings.ERC20ContractAddress);
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




        public async Task<decimal> GetBEP20WalletAddressSingleTokenBalanceAsync(string walletAddress, string tokenName)
        {

            var token = ValidateToken(tokenName,"BEP20");
            if (token == null)
                throw new ArgumentException($"Token '{tokenName}' not found in available tokens.");

            var erc20Contract = _bep20Web3.Eth.GetContract(ERC20Abi, token.Address);
            var balanceOfFunction = erc20Contract.GetFunction("balanceOf");
            var callData = balanceOfFunction.GetData(walletAddress).HexToByteArray();

            var calls = new List<MulticallCall>
            {
                new MulticallCall
                {
                    Target = token.Address,
                    CallData = callData
                }
            };

            var returnDataList = await _multicallService.ExecuteBscCallsAsync(calls);

            if (returnDataList == null || returnDataList.Count == 0 || returnDataList[0] == null)
                return 0;

            try
            {
                var parameterDecoder = new ParameterDecoder();
                var parameters = parameterDecoder.DecodeDefaultData(
                    returnDataList[0],
                    new Parameter("uint256", "balance"));

                var rawBalance = (BigInteger)parameters[0].Result;

                var balance = UnitConversion.Convert.FromWei(rawBalance, token.PriceDecimalPlaces);

                return balance;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error decoding token balance for {token.Name}: {ex.Message}");
                throw new BaseException("An error happened When getting wallet balance!");
            }
        }

        public async Task<decimal> GetERC20WalletAddressSingleTokenBalanceAsync(string walletAddress, string tokenName)
        {


            var token = ValidateToken(tokenName, "ERC20");
            if (token == null)
                throw new BadRequestException($"Token '{tokenName}' not found in available tokens.");

            var code = await _erc20Web3.Eth.GetCode.SendRequestAsync(token.Address);

            var erc20Contract = _erc20Web3.Eth.GetContract(ERC20Abi, token.Address);
            var balanceOfFunction = erc20Contract.GetFunction("balanceOf");
            var callData = balanceOfFunction.GetData(walletAddress).HexToByteArray();

            var calls = new List<MulticallCall>
            {
                new MulticallCall
                {
                    Target = token.Address,
                    CallData = callData
                }
            };

            var returnDataList = await _multicallService.ExecuteEthereumCallsAsync(calls);

            if (returnDataList == null || returnDataList.Count == 0 || returnDataList[0] == null)
                return 0;

            try
            {
                var parameterDecoder = new ParameterDecoder();
                var parameters = parameterDecoder.DecodeDefaultData(
                    returnDataList[0],
                    new Parameter("uint256", "balance"));

                var rawBalance = (BigInteger)parameters[0].Result;

                var balance = UnitConversion.Convert.FromWei(rawBalance, token.PriceDecimalPlaces);

                return balance;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error decoding token balance for {token.Name}: {ex.Message}");
                throw new BaseException("An error happened When getting wallet balance!");
            }
        }



        /// <summary>
        /// Preview accrued profit for a deposit
        /// </summary>
        /// <param name="depositId">Deposit Id (bytes32 hex string)</param>
        /// <param name="network">Network type (BEP20 / ERC20)</param>
        /// <returns>Claimable profit amount</returns>
        /// <exception cref="BadRequestException"></exception>
        public async Task<BigInteger> PreviewAccruedProfitAsync(string depositId, string network)
        {
            if (string.IsNullOrWhiteSpace(depositId))
                throw new BadRequestException("Deposit ID is null or empty.");

            if (string.IsNullOrWhiteSpace(network))
                throw new BadRequestException("Network is null or empty.");

            try
            {
                Web3 web3;
                string contractAddress;

                switch (network.ToUpper())
                {
                    case "BEP20":
                        web3 = _bep20Web3;
                        contractAddress = _settings.BEP20StakeContractAddress;
                        break;

                    case "ERC20":
                        web3 = _erc20Web3;
                        contractAddress = _settings.ERC20StakeContractAddress;
                        break;

                    default:
                        throw new BadRequestException("Invalid network type.");
                }

                var contract = web3.Eth.GetContract(StakeAbi, contractAddress);

                var function = contract.GetFunction("previewAccruedProfit");

                var depositIdBytes = HexToByteArray32(depositId);

                var result = await function.CallAsync<BigInteger>(
                    depositIdBytes
                );

                return result;
            }
            catch (SmartContractRevertException revertEx)
            {
                _logger.LogError(
                    revertEx,
                    "Contract revert error during previewAccruedProfit: {Message}",
                    revertEx.Message);

                throw new BaseException("Blockchain contract reverted.");
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Unexpected error during previewAccruedProfit.");

                throw new BaseException("An error happened while previewing accrued profit.");
            }
        }

        public async Task<LockedInvoiceChainResult> GetLockedInvoiceAsync(string invoiceId, string network)
        {
            if (string.IsNullOrWhiteSpace(invoiceId))
                throw new BadRequestException("Invoice ID is null or empty.");

            if (string.IsNullOrWhiteSpace(network))
                throw new BadRequestException("Network is null or empty.");

            try
            {
                Web3 web3;
                string contractAddress;

                switch (network.ToUpper())
                {
                    case "BEP20":
                        web3 = _bep20Web3;
                        contractAddress = _settings.ContractAddress;
                        break;

                    case "ERC20":
                        web3 = _erc20Web3;
                        contractAddress = _settings.ERC20ContractAddress;
                        break;

                    default:
                        throw new BadRequestException("Invalid network type.");
                }

                var contract = web3.Eth.GetContract(ContractAbi, contractAddress);
                var function = contract.GetFunction("getInvoice");
                var invoiceIdBytes = HexToByteArray32(invoiceId);
                var result = await function.CallDeserializingToObjectAsync<GetInvoiceOutputDTO>(invoiceIdBytes);

                if (result?.Invoice == null)
                    throw new BaseException("Locked invoice was not returned by the contract.");

                return new LockedInvoiceChainResult
                {
                    InvoiceId = result.Invoice.InvoiceId?.ToHex(),
                    Creator = result.Invoice.Creator,
                    Payer = result.Invoice.Payer,
                    Token = result.Invoice.Token,
                    UsdAmount = result.Invoice.UsdAmount,
                    PayAmount = result.Invoice.PayAmount,
                    UnlockTime = result.Invoice.UnlockTime,
                    LockDuration = result.Invoice.LockDuration,
                    Approver = result.Invoice.Approver,
                    LockedUntil = result.Invoice.LockedUntil,
                    StakedPayout = result.Invoice.StakedPayout,
                    ProfitClaimed = result.Invoice.ProfitClaimed,
                    Approved = result.Invoice.Approved,
                    Settled = result.Invoice.Settled
                };
            }
            catch (SmartContractRevertException revertEx)
            {
                _logger.LogError(
                    revertEx,
                    "Contract revert error during getInvoice: {Message}",
                    revertEx.Message);

                throw new BaseException("Blockchain contract reverted.");
            }
            catch (BaseException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Unexpected error during getInvoice.");

                throw new BaseException("An error happened while reading locked invoice.");
            }
        }


        ///// <summary>
        ///// use for delete single invoice
        ///// </summary>
        ///// <param name="invoiceId"></param>
        ///// <returns></returns>
        ///// <exception cref="BadRequestException"></exception>
        //public async Task<string> DeleteSingleInvoiceAsync(string invoiceId)
        //{
        //    if (string.IsNullOrEmpty(invoiceId))
        //        throw new BadRequestException("Invoice ID is null or empty.");

        //    try
        //    {
        //        var id = HexToByteArray32(invoiceId);

        //        var contract = _web3.Eth.GetContract(ContractAbi, _settings.ContractAddress);
        //        var function = contract.GetFunction("deleteInvoice");

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
        //        id
        //            }
        //        );

        //        if (receipt.Status.Value == 1)
        //        {
        //            _logger.LogInformation(
        //                "DeleteSingleInvoice successful. TxHash: {TxHash}",
        //                receipt.TransactionHash);

        //            return receipt.TransactionHash;
        //        }
        //        else
        //        {
        //            _logger.LogError(
        //                "DeleteSingleInvoice failed (reverted). TxHash: {TxHash}",
        //                receipt.TransactionHash);

        //            return null;
        //        }
        //    }
        //    catch (SmartContractRevertException revertEx)
        //    {
        //        _logger.LogError(
        //            revertEx,
        //            "Contract revert error during deleteInvoice: {Message}",
        //            revertEx.Message);

        //        return null;
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(
        //            ex,
        //            "Unexpected error during deleteInvoice.");

        //        return null;
        //    }
        //}

        private AvailableTokenData ValidateToken(string tokenName, string network = null)
        {

            if (tokenName == null)
                throw new BadRequestException($"Unsupported token name! {tokenName}");

            if (network == null)
            {
                var tokenData = _availableTokenData.FirstOrDefault(q => q.Name.Equals(tokenName, StringComparison.OrdinalIgnoreCase))
                 ?? throw new BadRequestException($"Unsupported token name! {tokenName}");
                return tokenData;
            }
            else
            {
                var tokenData = _availableTokenData.FirstOrDefault(q => q.Name.Equals(tokenName, StringComparison.OrdinalIgnoreCase) && q.Network == network)
                 ?? throw new BadRequestException($"Unsupported token name and network! {tokenName}");
                return tokenData;

            }

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
                var currentGasPrice = await _bep20Web3.Eth.GasPrice.SendRequestAsync();

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
