using Nethereum.Contracts.ContractHandlers;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using SLT.Services._BlockChain._MultiCallService.DTOs;
using static AutoShield.Utilities.Constants.RegisterMode;

namespace SLT.Services._BlockChain._MultiCallService
{
    public class MultiCallService : IMultiCallService, IScopedDependency
    {
        private readonly Web3 _web3;
        private readonly string _multicallAddress;
        private readonly ContractHandler _multicallContractHandler;

        public MultiCallService()
        {
            var rpcUrl = "https://bsc-dataseed.binance.org/";
            _web3 = new Web3(rpcUrl);

            _multicallAddress = "0xcA11bde05977b3631167028862bE2a173976CA11";
            _multicallContractHandler = _web3.Eth.GetContractHandler(_multicallAddress);
        }

        public async Task<List<byte[]>> ExecuteCallsAsync(List<MulticallCall> calls)
        {
            var aggregateFunction = new AggregateFunction { Calls = calls };

            try
            {
                var result = await _multicallContractHandler
                    .QueryAsync<AggregateFunction, AggregateOutput>(aggregateFunction);
                return result.ReturnData;
            }
            catch
            {
                var callData = _multicallContractHandler.GetFunction<AggregateFunction>().GetData(aggregateFunction);
                var callInput = new CallInput { To = _multicallAddress, Data = callData };
                var callResult = await _web3.Eth.Transactions.Call.SendRequestAsync(callInput);
                var result = _multicallContractHandler.GetFunction<AggregateFunction>()
                    .DecodeDTOTypeOutput<AggregateOutput>(callResult);
                return result.ReturnData;
            }
        }

        public async Task<List<MulticallResult>> ExecuteCallsTryAsync(List<MulticallCall> calls, bool requireSuccess = false)
        {
            var fn = new TryAggregateFunction { RequireSuccess = requireSuccess, Calls = calls };

            try
            {
                var res = await _multicallContractHandler
                    .QueryAsync<TryAggregateFunction, TryAggregateOutput>(fn);
                return res.ReturnData;
            }
            catch
            {
                var callData = _multicallContractHandler.GetFunction<TryAggregateFunction>().GetData(fn);
                var callInput = new CallInput { To = _multicallAddress, Data = callData };
                var callResult = await _web3.Eth.Transactions.Call.SendRequestAsync(callInput);
                var decoded = _multicallContractHandler.GetFunction<TryAggregateFunction>()
                    .DecodeDTOTypeOutput<TryAggregateOutput>(callResult);
                return decoded.ReturnData;
            }
        }
    }
}
