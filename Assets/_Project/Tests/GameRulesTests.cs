using System.Collections.Generic;
using NUnit.Framework;

namespace RollOfLegends.Tests
{
    public class GameRulesTests
    {
        private const int Samples = 600;

        [Test]
        public void Odd_OnlyRollsOneThreeOrFive()
        {
            AssertRollsExactly(DiceType.Odd, 1, 3, 5);
        }

        [Test]
        public void Even_OnlyRollsTwoFourOrSix()
        {
            AssertRollsExactly(DiceType.Even, 2, 4, 6);
        }

        [Test]
        public void Random_RollsEveryNumberFromOneToSix()
        {
            AssertRollsExactly(DiceType.Random, 1, 2, 3, 4, 5, 6);
        }

        [TestCase(1)]
        [TestCase(4)]
        [TestCase(6)]
        public void Fixed_AlwaysRollsTheFixedValue(int fixedValue)
        {
            AssertRollsExactly(DiceType.Fixed, fixedValue);
        }

        [Test]
        public void Fixed_ClampsOutOfRangeValues()
        {
            Assert.AreEqual(1, Dice.Roll(DiceType.Fixed, 0));
            Assert.AreEqual(6, Dice.Roll(DiceType.Fixed, 9));
        }

        [Test]
        public void RandomType_PicksAllFourDiceTypes()
        {
            var seen = new HashSet<DiceType>();
            for (int i = 0; i < Samples; i++)
            {
                seen.Add(Dice.RandomType());
            }
            CollectionAssert.AreEquivalent(new[] { DiceType.Odd, DiceType.Even, DiceType.Fixed, DiceType.Random }, seen);
        }

        [TestCase(0, 6, 14, 6)]
        [TestCase(13, 1, 14, 0)]
        [TestCase(12, 3, 14, 1)]
        public void Board_Advance_WrapsAroundTheLoop(int current, int steps, int tileCount, int expected)
        {
            Assert.AreEqual(expected, Board.Advance(current, steps, tileCount));
        }

        private static void AssertRollsExactly(DiceType type, params int[] expectedFaces)
        {
            var seen = new HashSet<int>();
            for (int i = 0; i < Samples; i++)
            {
                int value = Dice.Roll(type, expectedFaces[0]);
                CollectionAssert.Contains(expectedFaces, value, $"{type} rolled {value}");
                seen.Add(value);
            }
            CollectionAssert.AreEquivalent(new HashSet<int>(expectedFaces), seen, $"{type} never rolled some faces");
        }
    }
}
