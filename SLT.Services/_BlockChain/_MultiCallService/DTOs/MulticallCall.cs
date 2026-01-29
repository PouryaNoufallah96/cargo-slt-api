using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace SLT.Services._BlockChain._MultiCallService.DTOs
{

    [Struct("Call")]
    public class MulticallCall
    {
        [Parameter("address", "target", 1)]
        public string Target { get; set; }

        [Parameter("bytes", "callData", 2)]
        public byte[] CallData { get; set; }
    }

    [Struct("Result")]
    public class MulticallResult
    {
        [Parameter("bool", "success", 1)]
        public bool Success { get; set; }

        [Parameter("bytes", "returnData", 2)]
        public byte[] ReturnData { get; set; }
    }

    [FunctionOutput]
    public class AggregateOutput
    {
        [Parameter("uint256", "blockNumber", 1)]
        public BigInteger BlockNumber { get; set; }

        [Parameter("bytes[]", "returnData", 2)]
        public List<byte[]> ReturnData { get; set; }
    }


    [Function("aggregate", typeof(AggregateOutput))]
    public class AggregateFunction : FunctionMessage
    {
        [Parameter("tuple[]", "calls", 1)]
        public List<MulticallCall> Calls { get; set; }
    }

    [FunctionOutput]
    public class TryAggregateOutput : IFunctionOutputDTO
    {
        [Parameter("tuple[]", "returnData", 1)]
        public List<MulticallResult> ReturnData { get; set; }
    }

    [Function("tryAggregate", typeof(TryAggregateOutput))]
    public class TryAggregateFunction : FunctionMessage
    {
        [Parameter("bool", "requireSuccess", 1)]
        public bool RequireSuccess { get; set; }

        [Parameter("tuple[]", "calls", 2)]
        public List<MulticallCall> Calls { get; set; }
    }

}
