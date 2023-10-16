using System;
using System.Collections.Generic;
using System.Linq;
using Bencodex;
using Bencodex.Types;
using Nekoyume.Model.State;
using static Lib9c.SerializeKeys;

namespace NineChronicles.Headless
{
    public static class AvatarStateExtension
    {
        private static readonly Codec Codec = new Codec();

        public static byte[] ToRaw(this AvatarState avatarState)
        {
            // Suppose avatarAddresses = [a, b, c]
            // Then,   addresses =       [a,                    b,                    c,
            //                            aInventoryKey,        bInventoryKey,        cInventoryKey,
            //                            aWorldInformationKey, bWorldInformationKey, cWorldInformationKey,
            //                            aQuestListKey,        bQuestListKey,        cQuestListKey]
            switch (avatarState.Version)
            {
                case 1:
#pragma warning disable CS0618
                    return Codec.Encode(new Dictionary(new Dictionary<IKey, IValue>
                    {
                        [(Text)LegacyNameKey] = (Text)avatarState.name,
                        [(Text)LegacyCharacterIdKey] = (Integer)avatarState.characterId,
                        [(Text)LegacyLevelKey] = (Integer)avatarState.level,
                        [(Text)ExpKey] = (Integer)avatarState.exp,
                        [(Text)LegacyInventoryKey] = avatarState.inventory.Serialize(),
                        [(Text)LegacyWorldInformationKey] = avatarState.worldInformation.Serialize(),
                        [(Text)LegacyUpdatedAtKey] = avatarState.updatedAt.Serialize(),
                        [(Text)LegacyAgentAddressKey] = avatarState.agentAddress.Serialize(),
                        [(Text)LegacyQuestListKey] = avatarState.questList.Serialize(),
                        [(Text)LegacyMailBoxKey] = avatarState.mailBox.Serialize(),
                        [(Text)LegacyBlockIndexKey] = (Integer)avatarState.blockIndex,
                        [(Text)LegacyDailyRewardReceivedIndexKey] = (Integer)avatarState.dailyRewardReceivedIndex,
                        [(Text)LegacyActionPointKey] = (Integer)avatarState.actionPoint,
                        [(Text)LegacyStageMapKey] = avatarState.stageMap.Serialize(),
                        [(Text)LegacyMonsterMapKey] = avatarState.monsterMap.Serialize(),
                        [(Text)LegacyItemMapKey] = avatarState.itemMap.Serialize(),
                        [(Text)LegacyEventMapKey] = avatarState.eventMap.Serialize(),
                        [(Text)LegacyHairKey] = (Integer)avatarState.hair,
                        [(Text)LensKey] = (Integer)avatarState.lens,
                        [(Text)LegacyEarKey] = (Integer)avatarState.ear,
                        [(Text)LegacyTailKey] = (Integer)avatarState.tail,
                        [(Text)LegacyCombinationSlotAddressesKey] = avatarState.combinationSlotAddresses
                    .OrderBy(i => i)
                    .Select(i => i.Serialize())
                    .Serialize(),
                        [(Text)LegacyNonceKey] = avatarState.Nonce.Serialize(),
                        [(Text)LegacyRankingMapAddressKey] = avatarState.RankingMapAddress.Serialize(),
                    }.Union(new Dictionary(new Dictionary<IKey, IValue>
                    {
                        [(Text)LegacyAddressKey] = avatarState.address.Serialize(),
                    }))));
#pragma warning restore CS0618

                case 2:
#pragma warning disable CS0618
                    return Codec.Encode(new Dictionary(new Dictionary<IKey, IValue>
                    {
                        [(Text)NameKey] = (Text)avatarState.name,
                        [(Text)CharacterIdKey] = (Integer)avatarState.characterId,
                        [(Text)LevelKey] = (Integer)avatarState.level,
                        [(Text)ExpKey] = (Integer)avatarState.exp,
                        [(Text)UpdatedAtKey] = avatarState.updatedAt.Serialize(),
                        [(Text)AgentAddressKey] = avatarState.agentAddress.Serialize(),
                        [(Text)MailBoxKey] = avatarState.mailBox.Serialize(),
                        [(Text)BlockIndexKey] = (Integer)avatarState.blockIndex,
                        [(Text)DailyRewardReceivedIndexKey] = (Integer)avatarState.dailyRewardReceivedIndex,
                        [(Text)ActionPointKey] = (Integer)avatarState.actionPoint,
                        [(Text)StageMapKey] = avatarState.stageMap.Serialize(),
                        [(Text)MonsterMapKey] = avatarState.monsterMap.Serialize(),
                        [(Text)ItemMapKey] = avatarState.itemMap.Serialize(),
                        [(Text)EventMapKey] = avatarState.eventMap.Serialize(),
                        [(Text)HairKey] = (Integer)avatarState.hair,
                        [(Text)LensKey] = (Integer)avatarState.lens,
                        [(Text)EarKey] = (Integer)avatarState.ear,
                        [(Text)TailKey] = (Integer)avatarState.tail,
                        [(Text)CombinationSlotAddressesKey] = avatarState.combinationSlotAddresses
                    .OrderBy(i => i)
                    .Select(i => i.Serialize())
                    .Serialize(),
                        [(Text)RankingMapAddressKey] = avatarState.RankingMapAddress.Serialize(),
                    }.Union(new Dictionary(new Dictionary<IKey, IValue>
                    {
                        [(Text)AddressKey] = avatarState.address.Serialize(),
                    }))));
#pragma warning restore CS0618

                case 3: 
                    return Codec.Encode(avatarState.SerializeList());

                default:
                    throw new NotSupportedException("Version of AvatarState have to be in 1, 2, 3");
            }
        }
    }
}
