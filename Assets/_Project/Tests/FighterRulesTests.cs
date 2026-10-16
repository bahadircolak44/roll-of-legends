using NUnit.Framework;

namespace RollOfLegends.Tests
{
    public class FighterRulesTests
    {
        private static Fighter NewFighter()
        {
            return new Fighter("You", homeTile: 0, maxHp: 20, attack: 2, baseHp: 8);
        }

        [Test]
        public void Hp_NeverGoesBelowZeroOrAboveMax()
        {
            Fighter fighter = NewFighter();
            fighter.Heal(10);
            Assert.AreEqual(20, fighter.Hp);
            fighter.TakeDamage(50);
            Assert.AreEqual(0, fighter.Hp);
            Assert.IsTrue(fighter.IsKnockedOut);
        }

        [Test]
        public void Respawn_ReturnsHomeWithFullHp()
        {
            Fighter fighter = NewFighter();
            fighter.Tile = 9;
            fighter.TakeDamage(20);
            fighter.Respawn();
            Assert.AreEqual(0, fighter.Tile);
            Assert.AreEqual(20, fighter.Hp);
        }

        [Test]
        public void Sword_RaisesAttackAndIsUsedUp()
        {
            Fighter fighter = NewFighter();
            TileRules.Apply(fighter, TileEffect.Sword, out bool removed);
            Assert.AreEqual(3, fighter.Attack);
            Assert.IsTrue(removed);
        }

        [Test]
        public void Potion_HealsAndIsUsedUp()
        {
            Fighter fighter = NewFighter();
            fighter.TakeDamage(10);
            TileRules.Apply(fighter, TileEffect.Potion, out bool removed);
            Assert.AreEqual(14, fighter.Hp);
            Assert.IsTrue(removed);
        }

        [Test]
        public void Spikes_HurtAndStay()
        {
            Fighter fighter = NewFighter();
            TileRules.Apply(fighter, TileEffect.Spikes, out bool removed);
            Assert.AreEqual(17, fighter.Hp);
            Assert.IsFalse(removed);
        }

        [Test]
        public void Snare_SkipsNextTurnAndStays()
        {
            Fighter fighter = NewFighter();
            TileRules.Apply(fighter, TileEffect.Snare, out bool removed);
            Assert.AreEqual(1, fighter.SkipTurns);
            Assert.IsFalse(removed);
        }

        [Test]
        public void Monster_BeatsWeakFighter()
        {
            Fighter fighter = NewFighter(); // attack 2 < monster power 3
            TileRules.Apply(fighter, TileEffect.Monster, out bool removed);
            Assert.AreEqual(17, fighter.Hp);
            Assert.AreEqual(2, fighter.Attack);
            Assert.IsFalse(removed);
        }

        [Test]
        public void Monster_IsDefeatedByStrongFighter()
        {
            Fighter fighter = NewFighter();
            fighter.Attack = 3;
            TileRules.Apply(fighter, TileEffect.Monster, out bool removed);
            Assert.AreEqual(20, fighter.Hp);
            Assert.AreEqual(4, fighter.Attack);
            Assert.IsTrue(removed);
        }

        [Test]
        public void RivalAI_PrefersSwordOverSpikes()
        {
            Fighter rival = new Fighter("Rival", 14, 20, 2, 8);
            Fighter player = NewFighter();
            player.Tile = 5;
            TileEffect EffectAt(int tile) => tile == 16 ? TileEffect.Spikes : tile == 17 ? TileEffect.Sword : TileEffect.None;

            var hand = new[] { new DiceCard(DiceType.Fixed, 2), new DiceCard(DiceType.Fixed, 3) };
            Assert.AreEqual(1, RivalAI.ChooseCard(hand, rival, player, EffectAt, 28));
        }

        [Test]
        public void RivalAI_GoesForTheEnemyBase()
        {
            Fighter rival = new Fighter("Rival", 14, 20, 2, 8);
            rival.Tile = 25;
            Fighter player = NewFighter(); // home tile 0
            player.Tile = 10;

            var hand = new[] { new DiceCard(DiceType.Fixed, 2), new DiceCard(DiceType.Fixed, 4) };
            Assert.AreEqual(1, RivalAI.ChooseCard(hand, rival, player, _ => TileEffect.None, 28),
                "Moving 4 from tile 25 passes the player's base on tile 0");
        }
    }
}
