using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Bencodex;
using Bencodex.Types;
using Cocona;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Action;
using Libplanet.Action.State;
using Libplanet.Types.Assets;
using Libplanet.Types.Consensus;
using Libplanet.Types.Blocks;
using Libplanet.Types.Tx;
using RocksDbSharp;
using Serilog;
using Libplanet.Store.Trie;

namespace NineChronicles.Headless.Executable.Commands
{
    public partial class ReplayCommand : CoconaLiteConsoleAppBase
    {
        /// <summary>
        /// Almost duplicate https://github.com/planetarium/libplanet/blob/main/Libplanet/Action/ActionContext.cs.
        /// </summary>
        private sealed class ActionContext : IActionContext
        {
            public ActionContext(
                Address signer,
                TxId? txid,
                Address miner,
                long blockIndex,
                int blockProtocolVersion,
                IWorld previousState,
                int randomSeed,
                bool rehearsal = false)
            {
                Signer = signer;
                TxId = txid;
                Miner = miner;
                BlockIndex = blockIndex;
                BlockProtocolVersion = blockProtocolVersion;
                Rehearsal = rehearsal;
                PreviousState = previousState;
                RandomSeed = randomSeed;
            }

            public Address Signer { get; }

            public TxId? TxId { get; }

            public Address Miner { get; }

            public long BlockIndex { get; }

            public int BlockProtocolVersion { get; }

            public bool Rehearsal { get; }

            public IWorld PreviousState { get; }

            public int RandomSeed { get; }

            public bool BlockAction => TxId is null;

            public void UseGas(long gas)
            {
            }

            public long GasUsed() => 0;

            public long GasLimit() => 0;

            public IRandom GetRandom() => new Random(RandomSeed);
        }

        private sealed class Random : System.Random, IRandom
        {
            public Random(int seed)
                : base(seed)
            {
                Seed = seed;
            }

            public int Seed { get; private set; }
        }

        private sealed class LocalCacheBlockChainStates : IBlockChainStates
        {
            private readonly IBlockChainStates _source;
            private readonly RocksDb _rocksDb;

            public LocalCacheBlockChainStates(IBlockChainStates source, string cacheDirectory)
            {
                _source = source;
                var options = new DbOptions().SetCreateIfMissing();
                _rocksDb = RocksDb.Open(options, cacheDirectory);
            }
            public IWorldState GetWorldState(BlockHash? offset)
                => new LocalCacheWorldState(
                    _rocksDb,
                    _source.GetWorldState,
                    _source.GetAccountState,
                    offset);

            public IWorldState GetWorldState(HashDigest<SHA256>? hash)
                => throw new NotImplementedException();

            public IAccountState GetAccountState(HashDigest<SHA256>? hash)
                => new LocalCacheAccountState(
                    _rocksDb,
                    _source.GetAccountState,
                    hash);

            public IAccountState GetAccountState(Address address, BlockHash? offset)
                => throw new NotImplementedException();

            public IValue? GetState(Address address, Address accountAddress, BlockHash? offset) =>
                GetWorldState(offset).GetAccount(accountAddress).GetState(address);

            public IValue? GetState(Address address, HashDigest<SHA256>? stateRootHash) =>
                GetAccountState(stateRootHash).GetState(address);

            public FungibleAssetValue GetBalance(Address address, Currency currency, BlockHash? offset) =>
                GetWorldState(offset).GetAccount(ReservedAddresses.LegacyAccount).GetBalance(address, currency);

            public FungibleAssetValue GetTotalSupply(Currency currency, BlockHash? offset) =>
                GetWorldState(offset).GetAccount(ReservedAddresses.LegacyAccount).GetTotalSupply(currency);

            public ValidatorSet GetValidatorSet(BlockHash? offset) =>
                GetWorldState(offset).GetAccount(ReservedAddresses.LegacyAccount).GetValidatorSet();
        }

        private sealed class LocalCacheWorldState : IWorldState
        {
            private static readonly Codec _codec = new Codec();
            private readonly RocksDb _rocksDb;
            private readonly Func<BlockHash?, IWorldState> _sourceWorldStateGetterWithBlockHash;
            private readonly Func<HashDigest<SHA256>?, IAccountState> _sourceAccountStateGetterWithStateRootHash;
            private readonly BlockHash? _offset;

            public LocalCacheWorldState(
                RocksDb rocksDb,
                Func<BlockHash?, IWorldState> sourceWorldStateGetterWithBlockHash,
                Func<HashDigest<SHA256>?, IAccountState> sourceAccountStateGetterWithStateRootHash,
                BlockHash? offset)
            {
                _rocksDb = rocksDb;
                _sourceWorldStateGetterWithBlockHash = sourceWorldStateGetterWithBlockHash;
                _sourceAccountStateGetterWithStateRootHash = sourceAccountStateGetterWithStateRootHash;
                _offset = offset;
            }

            public ITrie Trie => _sourceWorldStateGetterWithBlockHash(_offset).Trie;

            public bool Legacy { get; private set; }

            public IAccount GetAccount(Address address)
            {
                var key = WithBlockHash(address.ToByteArray());
                try
                {
                    return GetAccount(key);
                }
                catch (KeyNotFoundException)
                {
                    var account = _sourceWorldStateGetterWithBlockHash(_offset).GetAccount(address);
                    SetAccount(key, account);
                    return account;
                }
            }

            public IAccount GetAccount(byte[] key)
            {
                if (_rocksDb.Get(key) is not { } bytes)
                {
                    throw new KeyNotFoundException();
                }

                return new Account(_sourceAccountStateGetterWithStateRootHash(
                    new HashDigest<SHA256>(((Binary)_codec.Decode(bytes)).ToImmutableArray())));
            }

            private void SetAccount(byte[] key, IAccount? account)
            {
                _rocksDb.Put(key, account is null ? new byte[] { 0x78 } : account.Trie.Hash.ToByteArray());
            }

            private byte[] WithBlockHash(params byte[][] suffixes)
            {
                if (_offset is not { } blockHash)
                {
                    throw new InvalidOperationException();
                }

                var stream = new MemoryStream(Libplanet.Types.Blocks.BlockHash.Size + suffixes.Sum(s => s.Length));
                stream.Write(blockHash.ToByteArray());
                foreach (var suffix in suffixes)
                {
                    stream.Write(suffix);
                }

                return stream.ToArray();
            }
        }

        private sealed class LocalCacheAccountState : IAccountState
        {
            private static readonly Codec _codec = new Codec();
            private readonly RocksDb _rocksDb;
            private readonly Func<HashDigest<SHA256>?, IAccountState> _sourceAccountStateGetterWithStateRootHash;
            private readonly HashDigest<SHA256>? _stateRootHash;

            public LocalCacheAccountState(
                RocksDb rocksDb,
                Func<HashDigest<SHA256>?, IAccountState> sourceAccountStateGetterWithStateRootHash,
                HashDigest<SHA256>? stateRootHash)
            {
                _rocksDb = rocksDb;
                _sourceAccountStateGetterWithStateRootHash = sourceAccountStateGetterWithStateRootHash;
                _stateRootHash = stateRootHash;
            }

            public ITrie Trie => _sourceAccountStateGetterWithStateRootHash(_stateRootHash).Trie;

            public IValue? GetState(Address address)
            {
                var key = WithStateRootHash(address.ToByteArray());
                try
                {
                    return GetValue(key);
                }
                catch (KeyNotFoundException)
                {
                    var state = _sourceAccountStateGetterWithStateRootHash(_stateRootHash).GetState(address);
                    SetValue(key, state);
                    return state;
                }
            }

            public IReadOnlyList<IValue?> GetStates(IReadOnlyList<Address> addresses)
            {
                return addresses.Select(GetState).ToList();
            }

            public FungibleAssetValue GetBalance(Address address, Currency currency)
            {
                var key = WithStateRootHash(address.ToByteArray(), currency.Hash.ToByteArray());
                try
                {
                    var state = GetValue(key);
                    if (state is not Integer integer)
                    {
                        throw new InvalidOperationException();
                    }

                    return FungibleAssetValue.FromRawValue(currency, integer);
                }
                catch (KeyNotFoundException)
                {
                    var fav = _sourceAccountStateGetterWithStateRootHash(_stateRootHash).GetBalance(address, currency);
                    SetValue(key, (Integer)fav.RawValue);
                    return fav;
                }
            }

            public FungibleAssetValue GetTotalSupply(Currency currency)
            {
                var key = WithStateRootHash(currency.Hash.ToByteArray());
                try
                {
                    var state = GetValue(key);
                    if (state is not Integer integer)
                    {
                        throw new InvalidOperationException();
                    }

                    return FungibleAssetValue.FromRawValue(currency, integer);
                }
                catch (KeyNotFoundException)
                {
                    var fav = _sourceAccountStateGetterWithStateRootHash(_stateRootHash).GetTotalSupply(currency);
                    SetValue(key, (Integer)fav.RawValue);
                    return fav;
                }
            }

            public ValidatorSet GetValidatorSet()
            {
                var key = WithStateRootHash(new byte[] { 0x5f, 0x5f, 0x5f });
                try
                {
                    var state = GetValue(key);
                    return state is not null ? new ValidatorSet(state) : new ValidatorSet();
                }
                catch (KeyNotFoundException)
                {
                    var validatorSet = _sourceAccountStateGetterWithStateRootHash(_stateRootHash).GetValidatorSet();
                    SetValue(key, validatorSet.Bencoded);
                    return validatorSet;
                }
            }

            private IValue? GetValue(byte[] key)
            {
                if (_rocksDb.Get(key) is not { } bytes)
                {
                    throw new KeyNotFoundException();
                }

                return bytes[0] == 'x' ? null : _codec.Decode(bytes);
            }

            private void SetValue(byte[] key, IValue? value)
            {
                _rocksDb.Put(key, value is null ? new byte[] { 0x78 } : _codec.Encode(value));
            }

            private byte[] WithStateRootHash(params byte[][] suffixes)
            {
                if (_stateRootHash is not { } stateRootHash)
                {
                    throw new InvalidOperationException();
                }

                var stream = new MemoryStream(HashDigest<SHA256>.Size + suffixes.Sum(s => s.Length));
                stream.Write(stateRootHash.ToByteArray());
                foreach (var suffix in suffixes)
                {
                    stream.Write(suffix);
                }

                return stream.ToArray();
            }
        }

        /// <summary>
        /// Almost duplicate https://github.com/planetarium/libplanet/blob/main/Libplanet/Action/ActionEvaluator.cs#L286.
        /// </summary>
        private static IEnumerable<ActionEvaluation> EvaluateActions(
            HashDigest<SHA256> preEvaluationHash,
            long blockIndex,
            int blockProtocolVersion,
            TxId? txid,
            IWorld previousStates,
            Address miner,
            Address signer,
            byte[] signature,
            IImmutableList<IAction> actions,
            ILogger? logger = null)
        {
            ActionContext CreateActionContext(
                IWorld prevState,
                int randomSeed)
            {
                return new ActionContext(
                    signer: signer,
                    txid: txid,
                    miner: miner,
                    blockIndex: blockIndex,
                    blockProtocolVersion: blockProtocolVersion,
                    previousState: prevState,
                    randomSeed: randomSeed);
            }

            byte[] hashedSignature;
            using (var hasher = SHA1.Create())
            {
                hashedSignature = hasher.ComputeHash(signature);
            }

            byte[] preEvaluationHashBytes = preEvaluationHash.ToByteArray();
            int seed = ActionEvaluator.GenerateRandomSeed(preEvaluationHashBytes, hashedSignature, signature, 0);

            IWorld states = previousStates;
            foreach (IAction action in actions)
            {
                Exception? exc = null;
                IWorld nextStates = states;
                ActionContext context = CreateActionContext(nextStates, seed);

                try
                {
                    Stopwatch stopwatch = new Stopwatch();
                    stopwatch.Start();
                    nextStates = action.Execute(context);
                    logger?
                        .Information(
                            "Action {Action} took {DurationMs} ms to execute",
                            action,
                            stopwatch.ElapsedMilliseconds);
                }
                catch (OutOfMemoryException e)
                {
                    // Because OutOfMemory is thrown non-deterministically depending on the state
                    // of the node, we should throw without further handling.
                    var message =
                        "Action {Action} of tx {TxId} of block #{BlockIndex} with " +
                        "pre-evaluation hash {PreEvaluationHash} threw an exception " +
                        "during execution";
                    logger?.Error(
                        e,
                        message,
                        action,
                        txid,
                        blockIndex,
                        ByteUtil.Hex(preEvaluationHash.ByteArray));
                    throw;
                }
                catch (Exception e)
                {
                    var message =
                        "Action {Action} of tx {TxId} of block #{BlockIndex} with " +
                        "pre-evaluation hash {PreEvaluationHash} threw an exception " +
                        "during execution";
                    logger?.Error(
                        e,
                        message,
                        action,
                        txid,
                        blockIndex,
                        ByteUtil.Hex(preEvaluationHash.ByteArray));
                    var innerMessage =
                        $"The action {action} (block #{blockIndex}, " +
                        $"pre-evaluation hash {ByteUtil.Hex(preEvaluationHash.ByteArray)}, " +
                        $"tx {txid} threw an exception during execution.  " +
                        "See also this exception's InnerException property";
                    logger?.Error(
                        "{Message}\nInnerException: {ExcMessage}", innerMessage, e.Message);
                    exc = new UnexpectedlyTerminatedActionException(
                        innerMessage,
                        preEvaluationHash,
                        blockIndex,
                        txid,
                        null,
                        action,
                        e);
                }

                // As IActionContext.Random is stateful, we cannot reuse
                // the context which is once consumed by Execute().
                ActionContext equivalentContext = CreateActionContext(states, seed);

                yield return new ActionEvaluation(
                    action: action,
                    inputContext: equivalentContext,
                    outputState: nextStates,
                    exception: exc);

                if (exc is { })
                {
                    yield break;
                }

                states = nextStates;
                unchecked
                {
                    seed++;
                }
            }
        }
    }
}
