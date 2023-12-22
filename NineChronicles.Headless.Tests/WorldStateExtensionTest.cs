using System.Collections.Immutable;
using Libplanet.Action.State;
using Libplanet.Crypto;
using Nekoyume.Action;
using Nekoyume.Module;
using NineChronicles.Headless.Tests.Common;
using Xunit;
using static Lib9c.SerializeKeys;

namespace NineChronicles.Headless.Tests
{
    public class WorldStateExtensionTest
    {
        // FIXME: This test puts all value in the legacy address, should test also with new world-based model.
        [Theory]
        [InlineData(true, false, false, false, false)]
        [InlineData(true, false, false, false, true)]
        [InlineData(false, true, true, true, false)]
        [InlineData(false, false, true, true, true)]
        [InlineData(false, true, false, true, true)]
        [InlineData(false, true, true, false, true)]
        public void GetAvatarStateLegacy(bool backward, bool inventoryExist, bool worldInformationExist, bool questListExist, bool exc)
        {
            MockAccountState mockAccountState = new MockAccountState();

            mockAccountState = backward
                ? mockAccountState.SetState(Fixtures.AvatarAddress, Fixtures.AvatarStateFX.Serialize())
                : mockAccountState.SetState(Fixtures.AvatarAddress, Fixtures.AvatarStateFX.SerializeV2());
            mockAccountState = inventoryExist
                ? mockAccountState.SetState(
                    Fixtures.AvatarAddress.Derive(LegacyInventoryKey),
                    Fixtures.AvatarStateFX.inventory.Serialize())
                : mockAccountState;
            mockAccountState = worldInformationExist
                ? mockAccountState.SetState(
                    Fixtures.AvatarAddress.Derive(LegacyWorldInformationKey),
                    Fixtures.AvatarStateFX.worldInformation.Serialize())
                : mockAccountState;
            mockAccountState = questListExist
                ? mockAccountState.SetState(
                    Fixtures.AvatarAddress.Derive(LegacyQuestListKey),
                    Fixtures.AvatarStateFX.questList.Serialize())
                : mockAccountState;

            var mockWorld =
                new MockWorld(new MockWorldState(
                    ImmutableDictionary<Address, IAccount>.Empty.Add(
                        ReservedAddresses.LegacyAccount,
                        new MockAccount(mockAccountState))));
            if (exc)
            {
                Assert.Throws<InvalidAddressException>(() => mockWorld.GetAvatarState(default));
            }
            else
            {
                var avatarState = mockWorld.GetAvatarState(Fixtures.AvatarAddress);

                Assert.NotNull(avatarState.inventory);
                Assert.NotNull(avatarState.worldInformation);
                Assert.NotNull(avatarState.questList);
            }
        }
    }
}
