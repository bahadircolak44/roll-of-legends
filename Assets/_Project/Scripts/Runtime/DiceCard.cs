using UnityEngine;

namespace RollOfLegends
{
    /// <summary>
    /// A card in the player's hand. Playing it rolls the die using the card's dice type.
    /// A Fixed card always rolls its own number, decided when the card is drawn.
    /// </summary>
    public readonly struct DiceCard
    {
        /// <summary>Chance that a normal draw is the rare DOUBLE card.</summary>
        public const float RareChance = 0.12f;

        public readonly DiceType Type;
        public readonly int FixedValue;

        public DiceCard(DiceType type, int fixedValue)
        {
            Type = type;
            FixedValue = fixedValue;
        }

        /// <summary>Draws a random card: usually a common type (a Fixed card gets a random 1-6), sometimes a rare DOUBLE.</summary>
        public static DiceCard Draw()
        {
            if (Random.value < RareChance) return DrawRare();
            DiceType type = Dice.RandomType();
            int fixedValue = type == DiceType.Fixed ? Random.Range(1, 7) : 0;
            return new DiceCard(type, fixedValue);
        }

        public static DiceCard DrawRare()
        {
            return new DiceCard(DiceType.Double, 0);
        }

        /// <summary>Each die thrown (two for a DOUBLE).</summary>
        public int[] RollDice()
        {
            return Dice.RollDice(Type, FixedValue);
        }

        public int Roll()
        {
            return Dice.Roll(Type, FixedValue);
        }

        public string Describe()
        {
            return Dice.Describe(Type, FixedValue);
        }
    }
}
