using NUnit.Framework;

namespace RollOfLegends.Tests
{
    public class FighterRulesTests
    {
        private static Fighter NewFighter()
        {
            return new Fighter("You", homeTile: 0, startHp: 20, attack: 2, baseHp: 10);
        }

        private static Fighter NewRival()
        {
            return new Fighter("Rival", homeTile: 14, startHp: 20, attack: 2, baseHp: 10);
        }

        // ---------- Health, defense, base ----------

        [Test]
        public void Hp_HasNoUpperLimitButNeverGoesBelowZero()
        {
            Fighter fighter = NewFighter();
            fighter.Heal(15);
            Assert.AreEqual(35, fighter.Hp);
            fighter.TakeDamage(100);
            Assert.AreEqual(0, fighter.Hp);
            Assert.IsTrue(fighter.IsKnockedOut);
        }

        [Test]
        public void Defense_ReducesDamageButAtLeastOneGetsThrough()
        {
            Fighter fighter = NewFighter();
            fighter.Defense = 2;
            Assert.AreEqual(1, fighter.TakeDamage(3));
            Assert.AreEqual(1, fighter.TakeDamage(1), "Always at least 1 damage");
            Assert.AreEqual(18, fighter.Hp);
        }

        [Test]
        public void Base_CanBeDamagedAndRepaired()
        {
            Fighter fighter = NewFighter();
            Assert.AreEqual(4, fighter.DamageBase(4));
            fighter.RepairBase(7);
            Assert.AreEqual(13, fighter.BaseHp, "Base health has no upper limit");
            Assert.AreEqual(13, fighter.DamageBase(50), "Can't take more than what is left");
            Assert.AreEqual(0, fighter.BaseHp);
        }

        [Test]
        public void Respawn_StartsOverAndLosesAllBuffsButKeepsTheBase()
        {
            Fighter fighter = NewFighter();
            fighter.Tile = 9;
            fighter.Heal(10);
            fighter.LevelUp(3);
            fighter.Attack = 6;
            fighter.Defense = 2;
            fighter.NextRollBonus = 2;
            fighter.LuckyDraw = true;
            fighter.DamageBase(3);
            fighter.TakeDamage(100);
            fighter.Respawn();
            Assert.AreEqual(0, fighter.Tile);
            Assert.AreEqual(20, fighter.Hp);
            Assert.AreEqual(1, fighter.Level);
            Assert.AreEqual(2, fighter.Attack);
            Assert.AreEqual(fighter.StartSiege, fighter.Siege);
            Assert.AreEqual(0, fighter.Defense);
            Assert.AreEqual(0, fighter.NextRollBonus);
            Assert.IsFalse(fighter.LuckyDraw);
            Assert.AreEqual(7, fighter.BaseHp, "The base keeps its health");
        }

        [Test]
        public void RollBonus_IsAppliedOnceAndYouAlwaysMoveAtLeastOne()
        {
            Fighter fighter = NewFighter();
            fighter.NextRollBonus = -3;
            Assert.AreEqual(1, fighter.UseRollBonus(2));
            Assert.AreEqual(2, fighter.UseRollBonus(2), "The bonus is used up");
            fighter.NextRollBonus = 2;
            Assert.AreEqual(6, fighter.UseRollBonus(4));
        }

        // ---------- Tile effects ----------

        [Test]
        public void Sword_RaisesAttackAndIsUsedUp()
        {
            Fighter fighter = NewFighter();
            EffectResult result = TileRules.Apply(fighter, NewRival(), TileEffect.Sword);
            Assert.AreEqual(3, fighter.Attack);
            Assert.AreEqual("+1 ATK", result.Change);
            Assert.IsTrue(result.Good);
            Assert.IsTrue(result.RemoveEffect);
        }

        [Test]
        public void HealthPotion_HealsPastTheStartAndIsUsedUp()
        {
            Fighter fighter = NewFighter();
            EffectResult result = TileRules.Apply(fighter, NewRival(), TileEffect.HealthPotion);
            Assert.AreEqual(20 + TileRules.PotionHeal, fighter.Hp);
            Assert.IsTrue(result.RemoveEffect);
        }

        [Test]
        public void RepairElixir_HealsTheBase()
        {
            Fighter fighter = NewFighter();
            EffectResult result = TileRules.Apply(fighter, NewRival(), TileEffect.RepairElixir);
            Assert.AreEqual(10 + TileRules.BaseRepair, fighter.BaseHp);
            Assert.AreEqual(20, fighter.Hp, "Only the base is healed");
            StringAssert.Contains("BASE", result.Change);
        }

        [Test]
        public void Shield_RaisesDefense()
        {
            Fighter fighter = NewFighter();
            TileRules.Apply(fighter, NewRival(), TileEffect.Shield);
            Assert.AreEqual(1, fighter.Defense);
        }

        [Test]
        public void Chest_MakesTheNextCardRare()
        {
            Fighter fighter = NewFighter();
            TileRules.Apply(fighter, NewRival(), TileEffect.Chest);
            Assert.IsTrue(fighter.LuckyDraw);
        }

        [Test]
        public void Spikes_HurtAndStay()
        {
            Fighter fighter = NewFighter();
            EffectResult result = TileRules.Apply(fighter, NewRival(), TileEffect.Spikes);
            Assert.AreEqual(17, fighter.Hp);
            Assert.IsFalse(result.Good);
            Assert.IsFalse(result.RemoveEffect);
        }

        [Test]
        public void Snare_SkipsNextTurnAndStays()
        {
            Fighter fighter = NewFighter();
            EffectResult result = TileRules.Apply(fighter, NewRival(), TileEffect.Snare);
            Assert.AreEqual(1, fighter.SkipTurns);
            Assert.IsFalse(result.RemoveEffect);
        }

        [Test]
        public void Forest_LowersTheNextRollAndStays()
        {
            Fighter fighter = NewFighter();
            EffectResult result = TileRules.Apply(fighter, NewRival(), TileEffect.Forest);
            Assert.AreEqual(-TileRules.ForestPenalty, fighter.NextRollBonus);
            Assert.IsFalse(result.RemoveEffect);
        }

        // ---------- Monster fights ----------

        [TestCase(MonsterTier.Small, 5, 5)]
        [TestCase(MonsterTier.Medium, 3, 4)]
        [TestCase(MonsterTier.Big, 1, 2)]
        public void Monster_HasFewerWinningNumbersTheBiggerItIs(MonsterTier tier, int fewest, int most)
        {
            for (int i = 0; i < 100; i++)
            {
                Monster monster = Monster.Create(tier);
                Assert.That(monster.WinningNumbers.Length, Is.InRange(fewest, most));
                CollectionAssert.AllItemsAreUnique(monster.WinningNumbers);
                foreach (int number in monster.WinningNumbers) Assert.That(number, Is.InRange(1, 6));
            }
        }

        [Test]
        public void Fight_RollingAWinningNumberDefeatsTheMonster()
        {
            Fighter fighter = NewFighter();
            EffectResult result = TileRules.FightMonster(fighter, new Monster(MonsterTier.Big, 2, 6), 6);
            Assert.IsTrue(result.Good);
            Assert.IsTrue(result.RemoveEffect);
            Assert.AreEqual(4, fighter.Level, "Big monster: +3 LV");
            Assert.AreEqual(5, fighter.Attack, "Each level: +1 ATK");
            Assert.AreEqual(23, fighter.Hp, "Big monster: +3 HP");
        }

        [Test]
        public void Fight_OtherNumbersLetTheMonsterStrikeBack()
        {
            Fighter fighter = NewFighter();
            fighter.Defense = 1;
            EffectResult result = TileRules.FightMonster(fighter, new Monster(MonsterTier.Big, 2, 6), 3);
            Assert.IsFalse(result.Good);
            Assert.IsFalse(result.RemoveEffect, "The monster stays");
            Assert.AreEqual(20 - (5 - 1), fighter.Hp, "Big monster hits for 5, minus 1 defense");
        }

        [Test]
        public void Fight_BiggerMonstersGiveMoreLevels()
        {
            Fighter fighter = NewFighter();
            TileRules.FightMonster(fighter, new Monster(MonsterTier.Small, 1, 2, 3, 4, 5), 1);
            Assert.AreEqual(23, fighter.Hp);
            Assert.AreEqual(2, fighter.Level, "Small: +1 LV");
            TileRules.FightMonster(fighter, new Monster(MonsterTier.Medium, 1, 2, 3), 1);
            Assert.AreEqual(4, fighter.Level, "Medium: +2 LV");
        }

        [Test]
        public void LevelUp_RaisesAttackAndSiege()
        {
            Fighter fighter = NewFighter();
            int siege = fighter.Siege;
            fighter.LevelUp(2);
            Assert.AreEqual(3, fighter.Level);
            Assert.AreEqual(4, fighter.Attack);
            Assert.AreEqual(siege + 2, fighter.Siege);
        }

        [Test]
        public void Spear_RaisesSiegeOnly()
        {
            Fighter fighter = NewFighter();
            int siege = fighter.Siege;
            EffectResult result = TileRules.Apply(fighter, NewRival(), TileEffect.Spear);
            Assert.AreEqual(siege + 1, fighter.Siege);
            Assert.AreEqual(2, fighter.Attack);
            Assert.AreEqual("+1 SIEGE", result.Change);
            Assert.IsTrue(result.RemoveEffect);
        }

        [Test]
        public void EmptyTile_DoesNothing()
        {
            Fighter fighter = NewFighter();
            Assert.IsFalse(TileRules.Apply(fighter, NewRival(), TileEffect.None).HasEffect);
            Assert.AreEqual(20, fighter.Hp);
        }

        // ---------- Spells ----------

        [Test]
        public void Fireball_HitsTheOtherHeroThroughArmor()
        {
            Fighter caster = NewFighter();
            Fighter rival = NewRival();
            rival.Defense = 5;
            EffectResult result = TileRules.CastSpell(caster, rival, Spell.Fireball);
            Assert.AreEqual(20 - TileRules.FireballDamage, rival.Hp);
            Assert.IsFalse(result.OtherChangeOnBase);
            Assert.IsNotNull(result.OtherChange);
        }

        [Test]
        public void Quake_DamagesTheOtherBase()
        {
            Fighter rival = NewRival();
            EffectResult result = TileRules.CastSpell(NewFighter(), rival, Spell.Quake);
            Assert.AreEqual(10 - TileRules.QuakeDamage, rival.BaseHp);
            Assert.IsTrue(result.OtherChangeOnBase);
        }

        [Test]
        public void Wisdom_GivesOneToThreeLevels()
        {
            for (int i = 0; i < 50; i++)
            {
                Fighter caster = NewFighter();
                EffectResult result = TileRules.CastSpell(caster, NewRival(), Spell.Wisdom);
                Assert.That(caster.Level, Is.InRange(2, 4));
                Assert.AreEqual(Spell.Wisdom, result.SpellCast);
            }
        }

        [Test]
        public void Haste_RaisesYourNextRoll()
        {
            Fighter caster = NewFighter();
            TileRules.CastSpell(caster, NewRival(), Spell.Haste);
            Assert.AreEqual(TileRules.HasteBonus, caster.NextRollBonus);
        }

        // ---------- Rival AI ----------

        [Test]
        public void RivalAI_PrefersSwordOverSpikes()
        {
            Fighter rival = NewRival();
            Fighter player = NewFighter();
            player.Tile = 5;
            TileEffect EffectAt(int tile) => tile == 16 ? TileEffect.Spikes : tile == 17 ? TileEffect.Sword : TileEffect.None;

            var hand = new[] { new DiceCard(DiceType.Fixed, 2), new DiceCard(DiceType.Fixed, 3) };
            Assert.AreEqual(1, RivalAI.ChooseCard(hand, rival, player, EffectAt, 28));
        }

        [Test]
        public void RivalAI_GoesForTheEnemyBase()
        {
            Fighter rival = NewRival();
            rival.Tile = 25;
            Fighter player = NewFighter(); // home tile 0
            player.Tile = 10;

            var hand = new[] { new DiceCard(DiceType.Fixed, 2), new DiceCard(DiceType.Fixed, 4) };
            Assert.AreEqual(1, RivalAI.ChooseCard(hand, rival, player, _ => TileEffect.None, 28),
                "Moving 4 from tile 25 passes the player's base on tile 0");
        }

        [Test]
        public void RivalAI_AccountsForItsForestPenalty()
        {
            Fighter rival = NewRival(); // on tile 14
            rival.NextRollBonus = -3;
            Fighter player = NewFighter();
            player.Tile = 5;
            // With -3, FIXED 5 lands on 16 (a sword) and FIXED 6 lands on 17 (spikes).
            TileEffect EffectAt(int tile) => tile == 16 ? TileEffect.Sword : tile == 17 ? TileEffect.Spikes : TileEffect.None;

            var hand = new[] { new DiceCard(DiceType.Fixed, 6), new DiceCard(DiceType.Fixed, 5) };
            Assert.AreEqual(1, RivalAI.ChooseCard(hand, rival, player, EffectAt, 28));
        }
    }
}
