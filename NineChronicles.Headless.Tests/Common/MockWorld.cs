using Libplanet.Store.Trie;

namespace NineChronicles.Headless.Tests.Common
{
#nullable enable
    using System.Collections.Immutable;
    using System.Diagnostics.Contracts;
    using Libplanet.Action.State;
    using Libplanet.Crypto;

    /// <summary>
    /// A rough replica of https://github.com/planetarium/libplanet/blob/main/Libplanet/State/World.cs
    /// except this has its constructors exposed as public for testing.
    /// </summary>
    [Pure]
    public class MockWorld : IWorld
    {
        private readonly IWorldState _baseState;

        public MockWorld()
            : this(new MockWorldState())
        {
        }

        public MockWorld(Address address, IAccount account)
            : this(
                new MockWorldState(),
                new MockWorldDelta(
                    ImmutableDictionary<Address, IAccount>.Empty.SetItem(address, account)))
        {
        }

        public MockWorld(IWorldState baseState)
            : this(baseState, new MockWorldDelta())
        {
        }

        private MockWorld(IWorldState baseState, IWorldDelta delta)
        {
            _baseState = baseState;
            Delta = delta;
            Trie = new MerkleTrie(new MemoryKeyValueStore());
        }

        public ITrie Trie { get; }

        /// <inheritdoc/>
        public bool Legacy => true;

        /// <inheritdoc/>
        public IWorldDelta Delta { get; private set; }

        public IAccount GetAccount(Address address)
        {
            return Delta.Accounts.TryGetValue(address, out IAccount? account)
                ? account!
                : _baseState.GetAccount(address);
        }

        public IWorld SetAccount(Address address, IAccount account)
        {
            if (!address.Equals(ReservedAddresses.LegacyAccount)
                && account.Delta.UpdatedFungibleAssets.Count > 0)
            {
                return this;
            }

            return new MockWorld(
                this,
                new MockWorldDelta(Delta.Accounts.SetItem(address, account)));
        }
    }
}
