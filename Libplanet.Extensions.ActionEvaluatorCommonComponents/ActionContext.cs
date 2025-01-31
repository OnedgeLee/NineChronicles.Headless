using System.Security.Cryptography;
using Libplanet.Action;
using Libplanet.Assets;
using Libplanet.Blocks;
using Libplanet.State;
using Libplanet.Tx;

namespace Libplanet.Extensions.ActionEvaluatorCommonComponents;

public class ActionContext : IActionContext
{
    public ActionContext(BlockHash? genesisHash, Address signer, TxId? txId, Address miner, long blockIndex,
        bool rehearsal, AccountStateDelta previousStates, IRandom random, HashDigest<SHA256>? previousStateRootHash,
        bool blockAction)
    {
        GenesisHash = genesisHash;
        Signer = signer;
        TxId = txId;
        Miner = miner;
        BlockIndex = blockIndex;
        Rehearsal = rehearsal;
        PreviousStates = previousStates;
        Random = random;
        PreviousStateRootHash = previousStateRootHash;
        BlockAction = blockAction;
    }

    public BlockHash? GenesisHash { get; }
    public Address Signer { get; init; }
    public TxId? TxId { get; }
    public Address Miner { get; init; }
    public long BlockIndex { get; init; }
    public bool Rehearsal { get; init; }
    public AccountStateDelta PreviousStates { get; init; }
    IAccountStateDelta IActionContext.PreviousStates => PreviousStates;
    public IRandom Random { get; init; }
    public HashDigest<SHA256>? PreviousStateRootHash { get; init; }
    public bool BlockAction { get; init; }

    public void PutLog(string log)
    {
        throw new NotImplementedException();
    }

    public IActionContext GetUnconsumedContext()
    {
        return new ActionContext(GenesisHash, Signer, TxId, Miner, BlockIndex, Rehearsal, PreviousStates,
            new Random(Random.Seed), PreviousStateRootHash, BlockAction);
    }

    public long GasUsed() => 0;

    public long GasLimit() => 0;
}
