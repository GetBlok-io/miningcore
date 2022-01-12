using System;

namespace Miningcore.Api.Responses
{
    public class SmartPoolResponse
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

        public ulong[] Blocks { get; set; }
        public string Subpool_Id { get; set; }
    }
}
