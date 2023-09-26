using Libplanet.Action.State;
using Nekoyume.Action.Extensions;
using Nekoyume.Model.Exceptions;
using Nekoyume.Model.State;
using Nekoyume.Module;
using NineChronicles.Headless.Tests.Common;
using Xunit;
using static Lib9c.SerializeKeys;

namespace NineChronicles.Headless.Tests
{
    public class AccountStateExtensionTest
    {
        [Theory]
        [InlineData(true, false, false, false, false)]
        [InlineData(true, false, false, false, true)]
        [InlineData(false, true, true, true, false)]
        [InlineData(false, false, true, true, true)]
        [InlineData(false, true, false, true, true)]
        [InlineData(false, true, true, false, true)]
        public void GetAvatarState(bool backward, bool inventoryExist, bool worldInformationExist, bool questListExist, bool exc)
        {
            IWorld mockWorld = new MockWorld();

            mockWorld = backward
                ? AvatarModule.SetAvatarState(
                    mockWorld,
                    Fixtures.AvatarAddress,
                    Fixtures.AvatarStateFX)
                : AvatarModule.SetAvatarStateV2(
                    mockWorld,
                    Fixtures.AvatarAddress,
                    Fixtures.AvatarStateFX);

            if (exc)
            {
                Assert.Null(AvatarModule.GetAvatarState(mockWorld, default));
            }
            else
            {
                AvatarState avatarState;
                try
                {
                    avatarState = AvatarModule.GetAvatarStateV2(mockWorld, Fixtures.AvatarAddress);
                }
                catch
                {
                    avatarState = AvatarModule.GetAvatarState(mockWorld, Fixtures.AvatarAddress);
                }
                

                Assert.NotNull(avatarState.inventory);
                Assert.NotNull(avatarState.worldInformation);
                Assert.NotNull(avatarState.questList);
            }
        }
    }
}
