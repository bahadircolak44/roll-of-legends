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

        /// <summary>Applies the effect to the fighter and describes what happened.</summary>
        public static EffectResult Apply(Fighter fighter, TileEffect effect)
        {
            switch (effect)
            {
                case TileEffect.Sword:
                    fighter.Attack += SwordBonus;
                    return new EffectResult("found a sword", $"+{SwordBonus} ATK", good: true, removeEffect: true);

                case TileEffect.Potion:
                    fighter.Heal(PotionHeal);
                    return new EffectResult("drank a potion", $"+{PotionHeal} HP", good: true, removeEffect: true);

                case TileEffect.Spikes:
                    fighter.TakeDamage(SpikeDamage);
                    return new EffectResult("stepped on spikes", $"-{SpikeDamage} HP", good: false, removeEffect: false);

                case TileEffect.Snare:
                    fighter.SkipTurns = 1;
                    return new EffectResult("got caught in a snare", "SKIP TURN", good: false, removeEffect: false);

                case TileEffect.Monster:
                    if (fighter.Attack >= MonsterPower)
                    {
                        fighter.Attack += MonsterReward;
                        return new EffectResult("defeated a monster", $"+{MonsterReward} ATK", good: true, removeEffect: true);
                    }
                    fighter.TakeDamage(MonsterPower);
                    return new EffectResult("was beaten by a monster", $"-{MonsterPower} HP", good: false, removeEffect: false);

                default:
                    return default;
            }
        }
    }

    /// <summary>What a tile effect did, for the history log ("found a sword") and the floating text ("+1 ATK").</summary>
    public readonly struct EffectResult
    {
        public readonly string Description;
        public readonly string Change;
        public readonly bool Good;
        public readonly bool RemoveEffect;

        public EffectResult(string description, string change, bool good, bool removeEffect)
        {
            Description = description;
            Change = change;
            Good = good;
            RemoveEffect = removeEffect;
        }

        public bool HasEffect => Description != null;
    }
}
