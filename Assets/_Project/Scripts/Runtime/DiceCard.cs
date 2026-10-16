using UnityEngine;

namespace RollOfLegends
{
    /// <summary>
    /// A card in the player's hand. Playing it rolls the die using the card's dice type.
    /// A Fixed card always rolls its own number, decided when the card is drawn.
    /// </summary>
    public readonly struct DiceCard
    {
        public readonly DiceType Type;
        public readonly int FixedValue;

        public DiceCard(DiceType type, int fixedValue)
        {
            Type = type;
            FixedValue = fixedValue;
        }

        /// <summary>Draws a random card: any dice type, and a random number 1-6 for Fixed cards.</summary>
        public static DiceCard Draw()
        {
            DiceType type = Dice.RandomType();
            int fixedValue = type == DiceType.Fixed ? Random.Range(1, 7) : 0;
            return new DiceCard(type, fixedValue);
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
