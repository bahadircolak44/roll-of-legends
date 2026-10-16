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
            EffectResult result = TileRules.Apply(fighter, TileEffect.Sword);
            Assert.AreEqual(3, fighter.Attack);
            Assert.AreEqual("+1 ATK", result.Change);
            Assert.IsTrue(result.Good);
            Assert.IsTrue(result.RemoveEffect);
        }

        [Test]
        public void Potion_HealsAndIsUsedUp()
        {
            Fighter fighter = NewFighter();
            fighter.TakeDamage(10);
            EffectResult result = TileRules.Apply(fighter, TileEffect.Potion);
            Assert.AreEqual(14, fighter.Hp);
            Assert.IsTrue(result.RemoveEffect);
        }

        [Test]
        public void Spikes_HurtAndStay()
        {
            Fighter fighter = NewFighter();
            EffectResult result = TileRules.Apply(fighter, TileEffect.Spikes);
            Assert.AreEqual(17, fighter.Hp);
            Assert.IsFalse(result.Good);
            Assert.IsFalse(result.RemoveEffect);
        }

        [Test]
        public void Snare_SkipsNextTurnAndStays()
        {
            Fighter fighter = NewFighter();
            EffectResult result = TileRules.Apply(fighter, TileEffect.Snare);
            Assert.AreEqual(1, fighter.SkipTurns);
            Assert.IsFalse(result.RemoveEffect);
        }

        [Test]
        public void Monster_BeatsWeakFighter()
        {
            Fighter fighter = NewFighter(); // attack 2 < monster power 3
            EffectResult result = TileRules.Apply(fighter, TileEffect.Monster);
            Assert.AreEqual(17, fighter.Hp);
            Assert.AreEqual(2, fighter.Attack);
            Assert.IsFalse(result.RemoveEffect);
        }

        [Test]
        public void Monster_IsDefeatedByStrongFighter()
        {
            Fighter fighter = NewFighter();
            fighter.Attack = 3;
            EffectResult result = TileRules.Apply(fighter, TileEffect.Monster);
            Assert.AreEqual(20, fighter.Hp);
            Assert.AreEqual(4, fighter.Attack);
            Assert.IsTrue(result.RemoveEffect);
        }

        [Test]
        public void EmptyTile_DoesNothing()
        {
            Fighter fighter = NewFighter();
            Assert.IsFalse(TileRules.Apply(fighter, TileEffect.None).HasEffect);
            Assert.AreEqual(20, fighter.Hp);
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
