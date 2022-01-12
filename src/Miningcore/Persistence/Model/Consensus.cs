using System;

namespace Miningcore.Persistence.Model
{
    public class Consensus
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
        public string Subpool_Id { get; set; }
    }
}
