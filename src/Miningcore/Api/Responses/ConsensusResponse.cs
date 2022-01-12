using System;

namespace Miningcore.Api.Responses
{
    public class ConsensusResponse
    {
        public string PoolId { get; set; }
        public string TransactionHash { get; set; }
        public ulong Epoch { get; set; }
        public ulong Height { get; set; }
        public string SmartPoolNFT { get; set; }
        public string Miner { get; set; }
        public ulong Shares { get; set; }
        public ulong MinPayout { get; set; }
        public ulong StoredPayout { get; set; }
        public DateTime Created { get; set; }
        public ulong Paid { get; set; }
        public string SubpoolId { get; set; }
    }
}
