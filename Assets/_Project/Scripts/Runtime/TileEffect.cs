using UnityEngine;

namespace RollOfLegends
{
    public enum TileEffect
    {
        None,
        Sword,        // +1 attack (used up)
        HealthPotion, // heals your hero (used up)
        RepairElixir, // repairs your base (used up)
        Shield,       // +1 defense (used up)
        SpellBook,    // casts a random spell (used up)
        Chest,        // your next card is a rare DOUBLE card (used up)
        Spikes,       // damage (stays)
        Snare,        // skip the next turn (stays)
        Forest,       // your next roll is lower (stays)
        Monster,      // fight: win if your attack is high enough
    }

    public enum Spell
    {
        Fireball, // damages the other hero, ignoring armor
        Quake,    // damages the other base
        Haste,    // your next roll is higher
    }

    /// <summary>What happens when a fighter lands on a tile effect. All numbers are tunable here.</summary>
    public static class TileRules
    {
        public const int SwordBonus = 1;
        public const int PotionHeal = 5;
        public const int BaseRepair = 3;
        public const int ShieldBonus = 1;
        public const int SpikeDamage = 3;
        public const int MonsterPower = 3;
        public const int MonsterReward = 1;   // attack bonus for beating a monster
        public const int ForestPenalty = 3;   // next roll minus this
        public const int FireballDamage = 4;
        public const int QuakeDamage = 2;
        public const int HasteBonus = 2;      // next roll plus this
        public const int PassHomeHeal = 2;    // healing when passing your own base

        /// <summary>Applies the effect of <paramref name="mover"/>'s landing tile and describes what happened.</summary>
        public static EffectResult Apply(Fighter mover, Fighter other, TileEffect effect)
        {
            switch (effect)
            {
                case TileEffect.Sword:
                    mover.Attack += SwordBonus;
                    return new EffectResult("found a sword", $"+{SwordBonus} ATK", good: true, removeEffect: true);

                case TileEffect.HealthPotion:
                    mover.Heal(PotionHeal);
                    return new EffectResult("drank a health potion", $"+{PotionHeal} HP", good: true, removeEffect: true);

                case TileEffect.RepairElixir:
                    mover.RepairBase(BaseRepair);
                    return new EffectResult("repaired the base with an elixir", $"+{BaseRepair} BASE", good: true, removeEffect: true);

                case TileEffect.Shield:
                    mover.Defense += ShieldBonus;
                    return new EffectResult("picked up a shield", $"+{ShieldBonus} DEF", good: true, removeEffect: true);

                case TileEffect.SpellBook:
                    return CastSpell(mover, other, (Spell)Random.Range(0, 3));

                case TileEffect.Chest:
                    mover.LuckyDraw = true;
                    return new EffectResult("opened a treasure chest: next card is a rare DOUBLE", "RARE CARD!", good: true, removeEffect: true);

                case TileEffect.Spikes:
                {
                    int taken = mover.TakeDamage(SpikeDamage);
                    return new EffectResult("stepped on spikes", $"-{taken} HP", good: false, removeEffect: false);
                }

                case TileEffect.Snare:
                    mover.SkipTurns = 1;
                    return new EffectResult("got caught in a snare", "SKIP TURN", good: false, removeEffect: false);

                case TileEffect.Forest:
                    mover.NextRollBonus -= ForestPenalty;
                    return new EffectResult("got lost in the forest", $"NEXT ROLL -{ForestPenalty}", good: false, removeEffect: false);

                case TileEffect.Monster:
                    if (mover.Attack >= MonsterPower)
                    {
                        mover.Attack += MonsterReward;
                        return new EffectResult("defeated a monster", $"+{MonsterReward} ATK", good: true, removeEffect: true);
                    }
                    int hurt = mover.TakeDamage(MonsterPower);
                    return new EffectResult("was beaten by a monster", $"-{hurt} HP", good: false, removeEffect: false);

                default:
                    return default;
            }
        }

        public static EffectResult CastSpell(Fighter caster, Fighter other, Spell spell)
        {
            switch (spell)
            {
                case Spell.Fireball:
                    other.TakePiercingDamage(FireballDamage);
                    return new EffectResult($"read a spell book: Fireball hits {other.Name}", "FIREBALL!", good: true, removeEffect: true,
                        otherChange: $"-{FireballDamage} HP", otherChangeOnBase: false);

                case Spell.Quake:
                    int taken = other.DamageBase(QuakeDamage);
                    return new EffectResult($"read a spell book: Earthquake shakes {other.Name}'s base", "EARTHQUAKE!", good: true, removeEffect: true,
                        otherChange: $"-{taken} BASE", otherChangeOnBase: true);

                default:
                    caster.NextRollBonus += HasteBonus;
                    return new EffectResult("read a spell book: Haste", $"NEXT ROLL +{HasteBonus}", good: true, removeEffect: true);
            }
        }

        /// <summary>Short name shown on the landing preview ("3 · Potion").</summary>
        public static string ShortName(TileEffect effect)
        {
            switch (effect)
            {
                case TileEffect.HealthPotion: return "Potion";
                case TileEffect.RepairElixir: return "Repair";
                case TileEffect.SpellBook: return "Spell";
                case TileEffect.None: return "";
                default: return effect.ToString();
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

        /// <summary>What happened to the other side, if anything (e.g. "-4 HP" from a fireball).</summary>
        public readonly string OtherChange;
        public readonly bool OtherChangeOnBase;

        public EffectResult(string description, string change, bool good, bool removeEffect,
            string otherChange = null, bool otherChangeOnBase = false)
        {
            Description = description;
            Change = change;
            Good = good;
            RemoveEffect = removeEffect;
            OtherChange = otherChange;
            OtherChangeOnBase = otherChangeOnBase;
        }

        public bool HasEffect => Description != null;
    }
}
