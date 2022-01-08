using System;

namespace Miningcore.Persistence.Model
{
    public class SmartPool
    {

        public string PoolId { get; set; }
        public string TransactionHash { get; set; }
        public ulong Epoch { get; set; }
        public ulong Height { get; set; }
        public string[] Members { get; set; }
        public ulong[] Fees { get; set; }
        public ulong[] Info { get; set; }
        public string[] Operators { get; set; }
        public string SmartPoolNFT { get; set; }
        public DateTime Created { get; set; }

        public long[] Blocks { get; set; }
    }
}
