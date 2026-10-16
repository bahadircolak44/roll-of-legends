namespace RollOfLegends
{
    public enum TileEffect
    {
        None,
        Sword,   // +1 attack, then moves to another tile
        Potion,  // heals, then moves to another tile
        Spikes,  // damage, stays
        Snare,   // skip the next turn, stays
        Monster, // fight: win if your attack is high enough
    }

    /// <summary>What happens when a fighter lands on a tile effect. All numbers are tunable here.</summary>
    public static class TileRules
    {
        public const int SwordBonus = 1;
        public const int PotionHeal = 4;
        public const int SpikeDamage = 3;
        public const int MonsterPower = 3;
        public const int MonsterReward = 1;   // attack bonus for beating a monster
        public const int PassHomeHeal = 2;    // healing when passing your own base

        /// <summary>Applies the effect and returns a message for the player.</summary>
        /// <param name="removeEffect">True if the effect is used up and should leave this tile.</param>
        public static string Apply(Fighter fighter, TileEffect effect, out bool removeEffect)
        {
            removeEffect = false;
            switch (effect)
            {
                case TileEffect.Sword:
                    fighter.Attack += SwordBonus;
                    removeEffect = true;
                    return $"{fighter.Name} found a sword: ATK +{SwordBonus}";

                case TileEffect.Potion:
                    fighter.Heal(PotionHeal);
                    removeEffect = true;
                    return $"{fighter.Name} drank a potion: HP +{PotionHeal}";

                case TileEffect.Spikes:
                    fighter.TakeDamage(SpikeDamage);
                    return $"{fighter.Name} stepped on spikes: HP -{SpikeDamage}";

                case TileEffect.Snare:
                    fighter.SkipTurns = 1;
                    return $"{fighter.Name} is caught in a snare: skips the next turn";

                case TileEffect.Monster:
                    if (fighter.Attack >= MonsterPower)
                    {
                        fighter.Attack += MonsterReward;
                        removeEffect = true;
                        return $"{fighter.Name} defeated a monster: ATK +{MonsterReward}";
                    }
                    fighter.TakeDamage(MonsterPower);
                    return $"A monster was too strong for {fighter.Name}: HP -{MonsterPower}";

                default:
                    return null;
            }
        }
    }
}
