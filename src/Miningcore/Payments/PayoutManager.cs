using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Data;
using System.Linq;
using System.Net.Http;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Autofac.Features.Metadata;
using Microsoft.Extensions.Hosting;
using Miningcore.Configuration;
using Miningcore.Extensions;
using Miningcore.Messaging;
using Miningcore.Mining;
using Miningcore.Notifications.Messages;
using Miningcore.Persistence;
using Miningcore.Persistence.Model;
using Miningcore.Persistence.Repositories;
using NLog;
using Org.BouncyCastle.Utilities.Net;
using Contract = Miningcore.Contracts.Contract;
using AutoMapper;

namespace Miningcore.Payments
{
    /// <summary>
    /// Coin agnostic payment processor
    /// </summary>
    public class PayoutManager : BackgroundService
    {
        private ISmartPoolRepository smartpoolRepo;
        private IConsensusRepository consensusRepo;
        private IMapper mapper;
        public PayoutManager(IComponentContext ctx,
            IConnectionFactory cf,
            IBlockRepository blockRepo,
            IShareRepository shareRepo,
            IBalanceRepository balanceRepo,
            ClusterConfig clusterConfig,
            IMessageBus messageBus)
        {
            Contract.RequiresNonNull(ctx, nameof(ctx));
            Contract.RequiresNonNull(cf, nameof(cf));
            Contract.RequiresNonNull(blockRepo, nameof(blockRepo));
            Contract.RequiresNonNull(shareRepo, nameof(shareRepo));
            Contract.RequiresNonNull(balanceRepo, nameof(balanceRepo));
            Contract.RequiresNonNull(messageBus, nameof(messageBus));

            this.ctx = ctx;
            this.cf = cf;
            this.blockRepo = blockRepo;
            this.shareRepo = shareRepo;
            this.balanceRepo = balanceRepo;
            this.messageBus = messageBus;
            this.clusterConfig = clusterConfig;
            smartpoolRepo = ctx.Resolve<ISmartPoolRepository>();
            consensusRepo = ctx.Resolve<IConsensusRepository>();
            mapper = ctx.Resolve<IMapper>();
            interval = TimeSpan.FromSeconds(clusterConfig.PaymentProcessing.Interval > 0 ?
                clusterConfig.PaymentProcessing.Interval : 600);
        }

        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();
        private readonly IBalanceRepository balanceRepo;
        private readonly IBlockRepository blockRepo;
        private readonly IConnectionFactory cf;
        private readonly IComponentContext ctx;
        private readonly IShareRepository shareRepo;
        private readonly IMessageBus messageBus;
        private readonly TimeSpan interval;
        private readonly ConcurrentDictionary<string, IMiningPool> pools = new();
        private readonly ClusterConfig clusterConfig;
        private readonly CompositeDisposable disposables = new();

        #if !DEBUG
        private static readonly TimeSpan initialRunDelay = TimeSpan.FromMinutes(1);
        #else
        private static readonly TimeSpan initialRunDelay = TimeSpan.FromSeconds(15);
        #endif

        private void AttachPool(IMiningPool pool)
        {
            pools.TryAdd(pool.Config.Id, pool);
        }

        private void OnPoolStatusNotification(PoolStatusNotification notification)
        {
            if(notification.Status == PoolStatus.Online)
                AttachPool(notification.Pool);
        }

        private async Task ProcessPoolsAsync(CancellationToken ct)
        {
            foreach(var pool in pools.Values.ToArray().Where(x => x.Config.Enabled && x.Config.PaymentProcessing.Enabled))
            {
                var config = pool.Config;

                logger.Info(() => $"Processing payments for pool {config.Id}");

                try
                {
                    var family = HandleFamilyOverride(config.Template.Family, config);

                    // resolve payout handler
                    var handlerImpl = ctx.Resolve<IEnumerable<Meta<Lazy<IPayoutHandler, CoinFamilyAttribute>>>>()
                        .First(x => x.Value.Metadata.SupportedFamilies.Contains(family)).Value;

                    var handler = handlerImpl.Value;
                    await handler.ConfigureAsync(clusterConfig, config, ct);

                    // resolve payout scheme
                    var scheme = ctx.ResolveKeyed<IPayoutScheme>(config.PaymentProcessing.PayoutScheme);

                    await UpdatePoolBalancesAsync(pool, config, handler, scheme, ct);
                    await PayoutPoolBalancesAsync(pool, config, handler, ct);
                }

                catch(InvalidOperationException ex)
                {
                    logger.Error(ex.InnerException ?? ex, () => $"[{config.Id}] Payment processing failed");
                }

                catch(AggregateException ex)
                {
                    switch(ex.InnerException)
                    {
                        case HttpRequestException httpEx:
                            logger.Error(() => $"[{config.Id}] Payment processing failed: {httpEx.Message}");
                            break;

                        default:
                            logger.Error(ex.InnerException, () => $"[{config.Id}] Payment processing failed");
                            break;
                    }
                }

                catch(Exception ex)
                {
                    logger.Error(ex, () => $"[{config.Id}] Payment processing failed");
                }
            }
        }

        private static CoinFamily HandleFamilyOverride(CoinFamily family, PoolConfig pool)
        {
            switch(family)
            {
                case CoinFamily.Equihash:
                    var equihashTemplate = pool.Template.As<EquihashCoinTemplate>();

                    if(equihashTemplate.UseBitcoinPayoutHandler)
                        return CoinFamily.Bitcoin;

                    break;
            }

            return family;
        }

        private async Task UpdatePoolBalancesAsync(IMiningPool pool, PoolConfig config, IPayoutHandler handler, IPayoutScheme scheme, CancellationToken ct)
        {

            var smartPoolJarPath = "";
            if(config.Extra.TryGetValue("smartPoolJarPath", out var result))
                smartPoolJarPath = ((string) result).Trim();

            if(smartPoolJarPath == "")
            {
                logger.Warn(() => "No smartPoolJarPath was found in configuration. Payout manager will not run jar commands or update db");
                return;
            }
            // get pending blockRepo for pool
            var pendingBlocks = await cf.Run(con => blockRepo.GetPendingBlocksForPayoutAsync(con, config.Id));

            // classify
            var updatedBlocks = await handler.ClassifyBlocksAsync(pool, pendingBlocks, ct);
            List<Block> blocksToSend = new List<Block>();
            if(updatedBlocks.Any())
            {
                foreach(var block in updatedBlocks.OrderBy(x => x.Created))
                {
                    logger.Info(() => $"Processing payments for pool {config.Id}, block {block.BlockHeight}");

                    await cf.RunTx(async (con, tx) =>
                    {
                        if(!block.Effort.HasValue)  // fill block effort if empty
                            await CalculateBlockEffortAsync(pool, config, block, handler, ct);

                        switch(block.ConfirmationProgress)
                        {
                            case double prog when prog == 1:
                                // blockchains that do not support block-reward payments via coinbase Tx
                                // must generate balance records for all reward recipients instead
                                //var blockReward = await handler.UpdateBlockRewardBalancesAsync(con, tx, pool, block, ct);

                                //await scheme.UpdateBalancesAsync(con, tx, pool, handler, block, blockReward, ct);
                                if(block.Status == BlockStatus.Pending)
                                {
                                    // Lets first update the block to have confirmation progress of 1 in the db
                                    await blockRepo.UpdateBlockAsync(con, tx, block);

                                    blocksToSend.Add(block);
                                }
                                else
                                {
                                    await blockRepo.UpdateBlockAsync(con, tx, block);
                                }
                                break;

                            case double prog when prog < 1:
                                await blockRepo.UpdateBlockAsync(con, tx, block);
                                break;
                        }
                    });
                }
                await cf.RunTx(async (con, tx) =>
                {
                    await SendPayoutToHolding(smartPoolJarPath, blocksToSend.ToArray(), ct);
                    foreach(Block block in blocksToSend.ToArray())
                    {
                        await blockRepo.UpdateBlockAsync(con, tx, block);
                    }
                });
            }
            else
                logger.Info(() => $"No updated blocks for pool {config.Id}");
        }

        private async Task PayoutPoolBalancesAsync(IMiningPool pool, PoolConfig config, IPayoutHandler handler, CancellationToken ct)
        {
            var smartPoolJarPath = "";
            if(config.Extra.TryGetValue("smartPoolJarPath", out var result))
                smartPoolJarPath = ((string) result).Trim();

            if(smartPoolJarPath == "")
            {
                logger.Warn(() => "No smartPoolJarPath was found in configuration. Payout manager will not run jar commands or update db");
                return;
            }
            var smartpool = (await cf.Run(con => smartpoolRepo.GetLastSmartPoolEntryAsync(con, config.Id)));
            var smartPoolObj = mapper.Map<Persistence.Postgres.Entities.SmartPool>(smartpool);
            
            // get first 5 confirmed blocks and pay them out
            var confirmedBlocks = await cf.Run(con => blockRepo.GetConfirmedBlocksForPayoutAsync(con, config.Id));

            var blocksToCheck = (await cf.Run(con => blockRepo.GetBlocksByHeight(con, config.Id, smartPoolObj.blocks, BlockStatus.Confirmed)));


            var exitCode = -1;
            await cf.RunTx(async (con, tx) =>
            {
                // We check each block from last smart pool entry that is still confirmed.
                exitCode = await CheckLastPayments(smartPoolJarPath, smartpool.Height, blocksToCheck, ct);
                foreach(Block block in blocksToCheck)
                {
                    await blockRepo.UpdateBlockAsync(con, tx, block);
                }
            });
            // If exit code is 0 or no blocks that are still confirmed, get next 1-5 confirmed blocks and pay them out
            if(exitCode == 0 || blocksToCheck.Length == 0)
            {
                confirmedBlocks = await cf.Run(con => blockRepo.GetConfirmedBlocksForPayoutAsync(con, config.Id));
            }

            await cf.RunTx(async (con, tx) =>
            {
                // We distribute payouts for each confirmed block.
                await DistributePayouts(smartPoolJarPath, confirmedBlocks, ct);
            });

        }

        private Task NotifyPayoutFailureAsync(Balance[] balances, PoolConfig pool, Exception ex)
        {
            messageBus.SendMessage(new PaymentNotification(pool.Id, ex.Message, balances.Sum(x => x.Amount), pool.Template.Symbol));

            return Task.FromResult(true);
        }

        private async Task CalculateBlockEffortAsync(IMiningPool pool, PoolConfig config, Block block, IPayoutHandler handler, CancellationToken ct)
        {
            // get share date-range
            var from = DateTime.MinValue;
            var to = block.Created;

            // get last block for pool
            var lastBlock = await cf.Run(con => blockRepo.GetBlockBeforeAsync(con, config.Id, new[]
            {
                BlockStatus.Confirmed,
                BlockStatus.Orphaned,
                BlockStatus.Pending,
            }, block.Created));

            if(lastBlock != null)
                from = lastBlock.Created;

            // get combined diff of all shares for block
            var accumulatedShareDiffForBlock = await cf.Run(con =>
                shareRepo.GetAccumulatedShareDifficultyBetweenCreatedAsync(con, config.Id, from, to));

            // handler has the final say
            if(accumulatedShareDiffForBlock.HasValue)
                await handler.CalculateBlockEffortAsync(pool, block, accumulatedShareDiffForBlock.Value, ct);
        }

        private async Task SendPayoutToHolding(String jarPath, Block[] blocks, CancellationToken ct)
        {
            Process p = new Process();

            string cmdString = "";

            if(blocks.Length > 1)
            {
                cmdString += "[";
                for(int i = 0; i < blocks.Length; i++)
                {
                    cmdString += blocks[i].BlockHeight.ToString();
                    if(i != blocks.Length - 1)
                    {
                        cmdString += ",";
                    }
                }
                cmdString += "]";
            }
            else if(blocks.Length == 1)
            {
                cmdString = blocks[0].BlockHeight.ToString();
            }
            else
            {
                return;
            }

            p.StartInfo.UseShellExecute = false;
            p.StartInfo.FileName = "java";
            p.StartInfo.Arguments = $"-jar {jarPath}smartpool.jar -c={jarPath}sp_config.json -h {cmdString}";
            logger.Info(() => $"Command being run: java {p.StartInfo.Arguments}");
            p.Start();
            
            await p.WaitForExitAsync(ct);
            if(p.ExitCode == 0)
            {
                logger.Info(() => $"Successfully sent rewards for block(s) {cmdString} to holding address.");
                foreach(Block block in blocks)
                {
                    block.Status = BlockStatus.Confirmed;
                }
                logger.Info(() => $"Block status now changed to confirmed.");
            }
            else
            {
                logger.Warn(() => $"Rewards for block(s) at height {cmdString} could not be sent to holding address!");
                logger.Warn(() => $"SmartPoolApp exited with code {p.ExitCode}");
            }
        }

        private async Task DistributePayouts(String jarPath, Block[] blocks, CancellationToken ct)
        {
            // Start the child process.
            Process p = new Process();
            // Redirect the output stream of the child process.
            string cmdString = "";

            if(blocks.Length > 1)
            {
                cmdString += "[";
                for(int i = 0; i < blocks.Length; i++)
                {
                    cmdString += blocks[i].BlockHeight.ToString();
                    if(i != blocks.Length - 1)
                    {
                        cmdString += ",";
                    }
                }
                cmdString += "]";
            }
            else if(blocks.Length == 1)
            {
                cmdString = blocks[0].BlockHeight.ToString();
            }
            else
            {
                return;
            }

            p.StartInfo.UseShellExecute = false;
            p.StartInfo.FileName = "java";
            p.StartInfo.Arguments = $"-jar {jarPath}smartpool.jar -c={jarPath}sp_config.json -d {cmdString}";
            
            logger.Info(() => $"Command being run: java {p.StartInfo.Arguments}");

            p.Start();
            
            await p.WaitForExitAsync(ct);
            if(p.ExitCode == 0)
            {
                logger.Info(() => $"Successfully sent payouts to members of SmartPool.");
                
            }
            else
            {
                logger.Warn(() => $"Payouts for block(s) {cmdString} could not be sent to SmartPool members!");
                logger.Warn(() => $"SmartPoolApp exited with code {p.ExitCode}");
            }
        }

        private async Task<int> CheckLastPayments(String jarPath, ulong height, Block[] blocks, CancellationToken ct)
        {


            // Start the child process.
            Process p = new Process();
            // Redirect the output stream of the child process.
            string cmdString = height.ToString();


            p.StartInfo.UseShellExecute = false;
            p.StartInfo.FileName = "java";
            p.StartInfo.Arguments = $"-jar {jarPath}smartpool.jar -c={jarPath}sp_config.json -chcl {cmdString}";

            logger.Info(() => $"Command being run: java {p.StartInfo.Arguments}");

            p.Start();

            await p.WaitForExitAsync(ct);
            if(p.ExitCode == 0)
            {
                logger.Info(() => $"Found confirmed tx! Changing last 5 blocks to have paid status!");
                foreach(Block block in blocks)
                {
                    block.Status = BlockStatus.Paid;
                }
                logger.Info(() => $"Block statuses now changed to paid.");
            }
            else
            {
                logger.Warn(() => $"Payouts for block(s) {cmdString} could not be sent to SmartPool members!");
                logger.Warn(() => $"SmartPoolApp exited with code {p.ExitCode}");
            }
            return p.ExitCode;
        }

        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            try
            {
                // monitor pool lifetime
                disposables.Add(messageBus.Listen<PoolStatusNotification>()
                    .ObserveOn(TaskPoolScheduler.Default)
                    .Subscribe(OnPoolStatusNotification));

                logger.Info(() => "Online");

                // Allow all pools to actually come up before the first payment processing run
                await Task.Delay(initialRunDelay, ct);

                while(!ct.IsCancellationRequested)
                {
                    try
                    {
                        await ProcessPoolsAsync(ct);
                    }

                    catch(Exception ex)
                    {
                        logger.Error(ex);
                    }

                    await Task.Delay(interval, ct);
                }

                logger.Info(() => "Offline");
            }

            finally
            {
                disposables.Dispose();
            }
        }
    }
}
