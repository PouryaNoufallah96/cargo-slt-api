
using Nethereum.Contracts.ContractHandlers;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using SLT.Services._BlockChain._MultiCallService.DTOs;
using static Utilities.Constants.RegisterMode;

namespace SLT.Services._BlockChain._MultiCallService
{
    public class MultiCallService : IMultiCallService, IScopedDependency
    {
        private readonly Web3 _bscWeb3;
        private readonly Web3 _ethWeb3;

        private readonly ContractHandler _bscMulticallHandler;
        private readonly ContractHandler _ethMulticallHandler;

        private const string MulticallAddress =
            "0xcA11bde05977b3631167028862bE2a173976CA11";

        public MultiCallService()
        {
            // BSC
            _bscWeb3 = new Web3("https://bsc-dataseed.binance.org/");
            _bscMulticallHandler =
                _bscWeb3.Eth.GetContractHandler(MulticallAddress);

            // ETH
            //_ethWeb3 = new Web3("https://eth-mainnet.nodereal.io");
            _ethWeb3 = new Web3("https://ethereum-rpc.publicnode.com");
            _ethMulticallHandler =
                _ethWeb3.Eth.GetContractHandler(MulticallAddress);
        }

        #region BSC

        public async Task<List<byte[]>> ExecuteBscCallsAsync(
            List<MulticallCall> calls)
        {
            return await ExecuteCallsInternalAsync(
                _bscWeb3,
                _bscMulticallHandler,
                calls);
        }

        public async Task<List<MulticallResult>> ExecuteBscTryCallsAsync(
            List<MulticallCall> calls,
            bool requireSuccess = false)
        {
            return await ExecuteTryCallsInternalAsync(
                _bscWeb3,
                _bscMulticallHandler,
                calls,
                requireSuccess);
        }

        #endregion

        #region Ethereum ERC20

        public async Task<List<byte[]>> ExecuteEthereumCallsAsync(
            List<MulticallCall> calls)
        {
            return await ExecuteCallsInternalAsync(
                _ethWeb3,
                _ethMulticallHandler,
                calls);
        }

        public async Task<List<MulticallResult>> ExecuteEthereumTryCallsAsync(
            List<MulticallCall> calls,
            bool requireSuccess = false)
        {
            return await ExecuteTryCallsInternalAsync(
                _ethWeb3,
                _ethMulticallHandler,
                calls,
                requireSuccess);
        }

        #endregion

        #region Internal Methods

        private async Task<List<byte[]>> ExecuteCallsInternalAsync(
            Web3 web3,
            ContractHandler handler,
            List<MulticallCall> calls)
        {
            var aggregateFunction = new AggregateFunction
            {
                Calls = calls
            };

            try
            {
                var result = await handler
                    .QueryAsync<AggregateFunction, AggregateOutput>(
                        aggregateFunction);

                return result.ReturnData;
            }
            catch
            {
                var callData = handler
                    .GetFunction<AggregateFunction>()
                    .GetData(aggregateFunction);

                var callInput = new CallInput
                {
                    To = MulticallAddress,
                    Data = callData
                };

                var callResult = await web3
                    .Eth
                    .Transactions
                    .Call
                    .SendRequestAsync(callInput);

                var decoded = handler
                    .GetFunction<AggregateFunction>()
                    .DecodeDTOTypeOutput<AggregateOutput>(callResult);

                return decoded.ReturnData;
            }
        }

        private async Task<List<MulticallResult>> ExecuteTryCallsInternalAsync(
            Web3 web3,
            ContractHandler handler,
            List<MulticallCall> calls,
            bool requireSuccess)
        {
            var fn = new TryAggregateFunction
            {
                RequireSuccess = requireSuccess,
                Calls = calls
            };

            try
            {
                var result = await handler
                    .QueryAsync<TryAggregateFunction, TryAggregateOutput>(fn);

                return result.ReturnData;
            }
            catch
            {
                var callData = handler
                    .GetFunction<TryAggregateFunction>()
                    .GetData(fn);

                var callInput = new CallInput
                {
                    To = MulticallAddress,
                    Data = callData
                };

                var callResult = await web3
                    .Eth
                    .Transactions
                    .Call
                    .SendRequestAsync(callInput);

                var decoded = handler
                    .GetFunction<TryAggregateFunction>()
                    .DecodeDTOTypeOutput<TryAggregateOutput>(callResult);

                return decoded.ReturnData;
            }
        }

        #endregion
    }
}
//using Nethereum.Contracts.ContractHandlers;
//using Nethereum.RPC.Eth.DTOs;
//using Nethereum.Web3;
//using SLT.Services._BlockChain._MultiCallService.DTOs;
//using static Utilities.Constants.RegisterMode;

//namespace SLT.Services._BlockChain._MultiCallService
//{
//    public class MultiCallService : IMultiCallService, IScopedDependency
//    {
//        private readonly Web3 _web3;
//        private readonly string _multicallAddress;
//        private readonly ContractHandler _multicallContractHandler;

//        public MultiCallService()
//        {
//            var rpcUrl = "https://bsc-dataseed.binance.org/";
//            _web3 = new Web3(rpcUrl);

//            _multicallAddress = "0xcA11bde05977b3631167028862bE2a173976CA11";
//            _multicallContractHandler = _web3.Eth.GetContractHandler(_multicallAddress);
//        }

//        public async Task<List<byte[]>> ExecuteCallsAsync(List<MulticallCall> calls)
//        {
//            var aggregateFunction = new AggregateFunction { Calls = calls };

//            try
//            {
//                var result = await _multicallContractHandler
//                    .QueryAsync<AggregateFunction, AggregateOutput>(aggregateFunction);
//                return result.ReturnData;
//            }
//            catch
//            {
//                var callData = _multicallContractHandler.GetFunction<AggregateFunction>().GetData(aggregateFunction);
//                var callInput = new CallInput { To = _multicallAddress, Data = callData };
//                var callResult = await _web3.Eth.Transactions.Call.SendRequestAsync(callInput);
//                var result = _multicallContractHandler.GetFunction<AggregateFunction>()
//                    .DecodeDTOTypeOutput<AggregateOutput>(callResult);
//                return result.ReturnData;
//            }
//        }

//        public async Task<List<MulticallResult>> ExecuteCallsTryAsync(List<MulticallCall> calls, bool requireSuccess = false)
//        {
//            var fn = new TryAggregateFunction { RequireSuccess = requireSuccess, Calls = calls };

//            try
//            {
//                var res = await _multicallContractHandler
//                    .QueryAsync<TryAggregateFunction, TryAggregateOutput>(fn);
//                return res.ReturnData;
//            }
//            catch
//            {
//                var callData = _multicallContractHandler.GetFunction<TryAggregateFunction>().GetData(fn);
//                var callInput = new CallInput { To = _multicallAddress, Data = callData };
//                var callResult = await _web3.Eth.Transactions.Call.SendRequestAsync(callInput);
//                var decoded = _multicallContractHandler.GetFunction<TryAggregateFunction>()
//                    .DecodeDTOTypeOutput<TryAggregateOutput>(callResult);
//                return decoded.ReturnData;
//            }
//        }

//    }
//}
