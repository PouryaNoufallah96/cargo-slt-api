namespace SLT.Services._BlockChain.DTOs.Settings
{

    public static class TokenForwardSaleAbi
    {
        public const string Value = @"
   [
    {
        ""type"": ""function"",
        ""name"": ""createOrderedInvoices"",
        ""inputs"": [
            {
                ""name"": ""receiver"",
                ""type"": ""address"",
                ""internalType"": ""address""
            },
            {
                ""name"": ""ids"",
                ""type"": ""bytes32[]"",
                ""internalType"": ""bytes32[]""
            },
            {
                ""name"": ""tokens"",
                ""type"": ""address[]"",
                ""internalType"": ""address[]""
            },
            {
                ""name"": ""usdAmounts"",
                ""type"": ""uint256[]"",
                ""internalType"": ""uint256[]""
            },
            {
                ""name"": ""unlockTimes"",
                ""type"": ""uint256[]"",
                ""internalType"": ""uint256[]""
            }
        ],
        ""outputs"": [],
        ""stateMutability"": ""nonpayable""
    },
    {
        ""type"": ""function"",
        ""name"": ""createQuickInvoice"",
        ""inputs"": [
            {
                ""name"": ""id"",
                ""type"": ""bytes32"",
                ""internalType"": ""bytes32""
            },
            {
                ""name"": ""receiver"",
                ""type"": ""address"",
                ""internalType"": ""address""
            },
            {
                ""name"": ""token"",
                ""type"": ""address"",
                ""internalType"": ""address""
            },
            {
                ""name"": ""usdAmount"",
                ""type"": ""uint256"",
                ""internalType"": ""uint256""
            }
        ],
        ""outputs"": [],
        ""stateMutability"": ""nonpayable""
    },
    {
        ""type"": ""function"",
        ""name"": ""getPaymentAmount"",
        ""inputs"": [
            {
                ""name"": ""invoiceId"",
                ""type"": ""bytes32"",
                ""internalType"": ""bytes32""
            }
        ],
        ""outputs"": [
            {
                ""name"": """",
                ""type"": ""uint256"",
                ""internalType"": ""uint256""
            }
        ],
        ""stateMutability"": ""view""
    },
    {
        ""type"": ""function"",
        ""name"": ""payInvoice"",
        ""inputs"": [
            {
                ""name"": ""invoiceId"",
                ""type"": ""bytes32"",
                ""internalType"": ""bytes32""
            }
        ],
        ""outputs"": [],
        ""stateMutability"": ""nonpayable""
    },
    {
        ""type"": ""function"",
        ""name"": ""setInvoiceFee"",
        ""inputs"": [
            {
                ""name"": ""newFeeBps"",
                ""type"": ""uint256"",
                ""internalType"": ""uint256""
            },
            {
                ""name"": ""newFeeRecipient"",
                ""type"": ""address"",
                ""internalType"": ""address""
            }
        ],
        ""outputs"": [],
        ""stateMutability"": ""nonpayable""
    },
    {
        ""type"": ""function"",
        ""name"": ""deleteBatchInvoice"",
        ""inputs"": [
            {
                ""name"": ""invoiceIds"",
                ""type"": ""bytes32[]"",
                ""internalType"": ""bytes32[]""
            }
        ],
        ""outputs"": [],
        ""stateMutability"": ""nonpayable""
    },
    {
        ""type"": ""function"",
        ""name"": ""deleteInvoice"",
        ""inputs"": [
            {
                ""name"": ""invoiceId"",
                ""type"": ""bytes32"",
                ""internalType"": ""bytes32""
            }
        ],
        ""outputs"": [],
        ""stateMutability"": ""nonpayable""
    },
    {
        ""type"": ""event"",
        ""name"": ""InvoiceCreated"",
        ""inputs"": [
            {
                ""name"": ""invoiceId"",
                ""type"": ""bytes32"",
                ""indexed"": false,
                ""internalType"": ""bytes32""
            },
            {
                ""name"": ""creator"",
                ""type"": ""address"",
                ""indexed"": false,
                ""internalType"": ""address""
            },
            {
                ""name"": ""token"",
                ""type"": ""address"",
                ""indexed"": false,
                ""internalType"": ""address""
            },
            {
                ""name"": ""usdAmount"",
                ""type"": ""uint256"",
                ""indexed"": false,
                ""internalType"": ""uint256""
            },
            {
                ""name"": ""unlockTime"",
                ""type"": ""uint256"",
                ""indexed"": false,
                ""internalType"": ""uint256""
            }
        ],
        ""anonymous"": false
    },
    {
        ""type"": ""event"",
        ""name"": ""InvoicePaid"",
        ""inputs"": [
            {
                ""name"": ""invoiceId"",
                ""type"": ""bytes32"",
                ""indexed"": false,
                ""internalType"": ""bytes32""
            },
            {
                ""name"": ""payer"",
                ""type"": ""address"",
                ""indexed"": false,
                ""internalType"": ""address""
            },
            {
                ""name"": ""token"",
                ""type"": ""address"",
                ""indexed"": false,
                ""internalType"": ""address""
            },
            {
                ""name"": ""payAmount"",
                ""type"": ""uint256"",
                ""indexed"": false,
                ""internalType"": ""uint256""
            }
        ],
        ""anonymous"": false
    },
    {
        ""type"": ""event"",
        ""name"": ""InvoiceDeleted"",
        ""inputs"": [
            {
                ""name"": ""invoiceId"",
                ""type"": ""bytes32"",
                ""indexed"": false,
                ""internalType"": ""bytes32""
            }
        ],
        ""anonymous"": false
    }
]
    ";

        public const string StakeAbi = @"
[
    {
        ""type"": ""function"",
        ""name"": ""deposit"",
        ""inputs"": [
            {
                ""name"": ""depositId"",
                ""type"": ""bytes32"",
                ""internalType"": ""bytes32""
            },
            {
                ""name"": ""token"",
                ""type"": ""address"",
                ""internalType"": ""address""
            },
            {
                ""name"": ""amount"",
                ""type"": ""uint256"",
                ""internalType"": ""uint256""
            },
            {
                ""name"": ""lockDuration"",
                ""type"": ""uint256"",
                ""internalType"": ""uint256""
            }
        ],
        ""outputs"": [],
        ""stateMutability"": ""nonpayable""
    },
    {
        ""type"": ""function"",
        ""name"": ""getDeposit"",
        ""inputs"": [
            {
                ""name"": ""depositId"",
                ""type"": ""bytes32"",
                ""internalType"": ""bytes32""
            }
        ],
        ""outputs"": [
            {
                ""name"": """",
                ""type"": ""tuple"",
                ""internalType"": ""struct StakingTypes.Deposit"",
                ""components"": [
                    {
                        ""name"": ""depositId"",
                        ""type"": ""bytes32"",
                        ""internalType"": ""bytes32""
                    },
                    {
                        ""name"": ""depositor"",
                        ""type"": ""address"",
                        ""internalType"": ""address""
                    },
                    {
                        ""name"": ""token"",
                        ""type"": ""address"",
                        ""internalType"": ""address""
                    },
                    {
                        ""name"": ""principal"",
                        ""type"": ""uint256"",
                        ""internalType"": ""uint256""
                    },
                    {
                        ""name"": ""profit"",
                        ""type"": ""uint256"",
                        ""internalType"": ""uint256""
                    },
                    {
                        ""name"": ""profitRateBps"",
                        ""type"": ""uint256"",
                        ""internalType"": ""uint256""
                    },
                    {
                        ""name"": ""lockDuration"",
                        ""type"": ""uint256"",
                        ""internalType"": ""uint256""
                    },
                    {
                        ""name"": ""depositedAt"",
                        ""type"": ""uint256"",
                        ""internalType"": ""uint256""
                    },
                    {
                        ""name"": ""unlocksAt"",
                        ""type"": ""uint256"",
                        ""internalType"": ""uint256""
                    },
                    {
                        ""name"": ""status"",
                        ""type"": ""uint8"",
                        ""internalType"": ""enum StakingTypes.DepositStatus""
                    },
                    {
                        ""name"": ""profitWithdrawn"",
                        ""type"": ""uint256"",
                        ""internalType"": ""uint256""
                    }
                ]
            }
        ],
        ""stateMutability"": ""view""
    },
    {
        ""type"": ""function"",
        ""name"": ""getPlan"",
        ""inputs"": [
            {
                ""name"": ""planId"",
                ""type"": ""uint256"",
                ""internalType"": ""uint256""
            }
        ],
        ""outputs"": [
            {
                ""name"": """",
                ""type"": ""tuple"",
                ""internalType"": ""struct StakingTypes.Plan"",
                ""components"": [
                    {
                        ""name"": ""duration"",
                        ""type"": ""uint256"",
                        ""internalType"": ""uint256""
                    },
                    {
                        ""name"": ""profitRateBps"",
                        ""type"": ""uint256"",
                        ""internalType"": ""uint256""
                    },
                    {
                        ""name"": ""active"",
                        ""type"": ""bool"",
                        ""internalType"": ""bool""
                    }
                ]
            }
        ],
        ""stateMutability"": ""view""
    },
    {
        ""type"": ""function"",
        ""name"": ""previewAccruedProfit"",
        ""inputs"": [
            {
                ""name"": ""depositId"",
                ""type"": ""bytes32"",
                ""internalType"": ""bytes32""
            }
        ],
        ""outputs"": [
            {
                ""name"": ""claimable"",
                ""type"": ""uint256"",
                ""internalType"": ""uint256""
            }
        ],
        ""stateMutability"": ""view""
    },
    {
        ""type"": ""function"",
        ""name"": ""previewPayout"",
        ""inputs"": [
            {
                ""name"": ""depositId"",
                ""type"": ""bytes32"",
                ""internalType"": ""bytes32""
            }
        ],
        ""outputs"": [
            {
                ""name"": ""totalPayout"",
                ""type"": ""uint256"",
                ""internalType"": ""uint256""
            }
        ],
        ""stateMutability"": ""view""
    },
    {
        ""type"": ""function"",
        ""name"": ""withdrawProfit"",
        ""inputs"": [
            {
                ""name"": ""depositId"",
                ""type"": ""bytes32"",
                ""internalType"": ""bytes32""
            }
        ],
        ""outputs"": [],
        ""stateMutability"": ""nonpayable""
    },
    {
        ""type"": ""event"",
        ""name"": ""DepositCreated"",
        ""inputs"": [
            {
                ""name"": ""depositId"",
                ""type"": ""bytes32"",
                ""indexed"": false,
                ""internalType"": ""bytes32""
            },
            {
                ""name"": ""depositor"",
                ""type"": ""address"",
                ""indexed"": false,
                ""internalType"": ""address""
            },
            {
                ""name"": ""token"",
                ""type"": ""address"",
                ""indexed"": false,
                ""internalType"": ""address""
            },
            {
                ""name"": ""principal"",
                ""type"": ""uint256"",
                ""indexed"": false,
                ""internalType"": ""uint256""
            },
            {
                ""name"": ""profit"",
                ""type"": ""uint256"",
                ""indexed"": false,
                ""internalType"": ""uint256""
            },
            {
                ""name"": ""lockDuration"",
                ""type"": ""uint256"",
                ""indexed"": false,
                ""internalType"": ""uint256""
            },
            {
                ""name"": ""unlocksAt"",
                ""type"": ""uint256"",
                ""indexed"": false,
                ""internalType"": ""uint256""
            }
        ],
        ""anonymous"": false
    },
    {
        ""type"": ""event"",
        ""name"": ""Withdrawn"",
        ""inputs"": [
            {
                ""name"": ""depositId"",
                ""type"": ""bytes32"",
                ""indexed"": false,
                ""internalType"": ""bytes32""
            },
            {
                ""name"": ""depositor"",
                ""type"": ""address"",
                ""indexed"": false,
                ""internalType"": ""address""
            },
            {
                ""name"": ""principal"",
                ""type"": ""uint256"",
                ""indexed"": false,
                ""internalType"": ""uint256""
            },
            {
                ""name"": ""profit"",
                ""type"": ""uint256"",
                ""indexed"": false,
                ""internalType"": ""uint256""
            },
            {
                ""name"": ""totalPayout"",
                ""type"": ""uint256"",
                ""indexed"": false,
                ""internalType"": ""uint256""
            }
        ],
        ""anonymous"": false
    },
    {
        ""type"": ""event"",
        ""name"": ""ProfitWithdrawn"",
        ""inputs"": [
            {
                ""name"": ""depositId"",
                ""type"": ""bytes32"",
                ""indexed"": false,
                ""internalType"": ""bytes32""
            },
            {
                ""name"": ""depositor"",
                ""type"": ""address"",
                ""indexed"": false,
                ""internalType"": ""address""
            },
            {
                ""name"": ""token"",
                ""type"": ""address"",
                ""indexed"": false,
                ""internalType"": ""address""
            },
            {
                ""name"": ""profit"",
                ""type"": ""uint256"",
                ""indexed"": false,
                ""internalType"": ""uint256""
            }
        ],
        ""anonymous"": false
    }
]";

        public const string ERC20Abi = @"[
            { 'constant':true,'inputs':[{'name':'_owner','type':'address'}],'name':'balanceOf','outputs':[{'name':'balance','type':'uint256'}],'type':'function' },
            { 'constant':true,'inputs':[],'name':'decimals','outputs':[{'name':'','type':'uint8'}],'type':'function' },
            { 'constant':true,'inputs':[],'name':'symbol','outputs':[{'name':'','type':'string'}],'type':'function' }
        ]";

    }


}
