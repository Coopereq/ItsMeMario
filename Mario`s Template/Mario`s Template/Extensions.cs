﻿using System;
using System.Collections.Generic;
using System.Linq;
using EloBuddy;
using EloBuddy.SDK;
using EloBuddy.SDK.Menu;
using EloBuddy.SDK.Menu.Values;
using SharpDX;

// ReSharper disable CoVariantArrayConversion

namespace Mario_s_Template
{
    public static class Extensions
    {
        #region Misc

        public static bool CanMove(this Obj_AI_Base target)
        {
            return !(target.MoveSpeed < 50) && !target.IsStunned && !target.HasBuffOfType(BuffType.Stun) && !target.HasBuffOfType(BuffType.Fear) &&
                   !target.HasBuffOfType(BuffType.Snare) && !target.HasBuffOfType(BuffType.Knockup) && !target.HasBuff("Recall") &&
                   !target.HasBuffOfType(BuffType.Knockback) && !target.HasBuffOfType(BuffType.Charm) && !target.HasBuffOfType(BuffType.Taunt) &&
                   !target.HasBuffOfType(BuffType.Suppression) && (!target.Spellbook.IsChanneling || target.IsMoving);
        }

        #endregion Misc

        #region Vector

        public static bool IsSolid(this Vector3 pos)
        {
            return pos.ToNavMeshCell().CollFlags.HasFlag(CollisionFlags.Building) && pos.ToNavMeshCell().CollFlags.HasFlag(CollisionFlags.Wall);
        }

        public static Vector3 GetBestCircularFarmPosition(this Spell.Skillshot spell, int count = 3, int hitchance = 85)
        {
            var minions =
                EntityManager.MinionsAndMonsters.GetLaneMinions()
                    .Where(
                        m => m.IsValidTarget(spell.Range))
                    .ToArray();

            if (minions.Length == 0 && minions != null) return Vector3.Zero;

            var farmLocation = Prediction.Position.PredictCircularMissileAoe(minions, spell.Range, spell.Width,
                spell.CastDelay, spell.Speed).OrderByDescending(r => r.GetCollisionObjects<Obj_AI_Minion>().Length).FirstOrDefault();

            if (farmLocation != null && farmLocation.HitChancePercent >= hitchance && farmLocation.CollisionObjects.Length >= count)
            {
                return farmLocation.CastPosition;
            }

            return Vector3.Zero;
        }

        public static Vector3 GetBestLinearFarmPosition(this Spell.Skillshot spell, int minMinionsToHit = 3)
        {
            var minions =
                EntityManager.MinionsAndMonsters.GetLaneMinions().Where(m => m.IsValidTarget(spell.Range)).ToArray();

            var bestPos = EntityManager.MinionsAndMonsters.GetLineFarmLocation(minions, spell.Width,
                (int) spell.Range, Player.Instance.Position.To2D());

            if (minions.Length > 0 && bestPos.HitNumber >= minMinionsToHit)
            {
                return bestPos.CastPosition;
            }

            return Vector3.Zero;
        }

        public static Vector3 GetBestCircularCastPosition(this Spell.Skillshot spell, int count = 3, int hitchance = 75)
        {
            var heros =
                EntityManager.Heroes.Enemies.Where(
                    m => m.IsValidTarget(spell.Range))
                    .ToArray();

            if (heros.Length == 0 && heros != null) return Vector3.Zero;

            var castPos = Prediction.Position.PredictCircularMissileAoe(heros, spell.Range, spell.Width,
                spell.CastDelay, spell.Speed).OrderByDescending(r => r.GetCollisionObjects<Obj_AI_Minion>().Length).FirstOrDefault();

            if (castPos != null && castPos.HitChancePercent >= hitchance)
            {
                return castPos.CastPosition;
            }

            return Vector3.Zero;
        }

        public static BestCastPosition GetBestLinearCastPosition(IEnumerable<AIHeroClient> entities, float width, int range,
            Vector2? sourcePosition = null)
        {
            var targets = entities.ToArray();
            switch (targets.Length)
            {
                case 0:
                    return new BestCastPosition();
                case 1:
                    return new BestCastPosition {CastPosition = targets[0].ServerPosition, HitNumber = 1};
            }

            var posiblePositions = new List<Vector2>(targets.Select(o => o.ServerPosition.To2D()));
            foreach (var target in targets)
            {
                posiblePositions.AddRange(from t in targets
                    where t.NetworkId != target.NetworkId
                    select (t.ServerPosition.To2D() + target.ServerPosition.To2D())/2);
            }

            var startPos = sourcePosition ?? Player.Instance.ServerPosition.To2D();
            var minionCount = 0;
            var result = Vector2.Zero;

            foreach (var pos in posiblePositions.Where(o => o.IsInRange(startPos, range)))
            {
                var endPos = startPos + range*(pos - startPos).Normalized();
                var count = targets.Count(o => o.ServerPosition.To2D().Distance(startPos, endPos, true, true) <= width*width);

                if (count >= minionCount)
                {
                    result = endPos;
                    minionCount = count;
                }
            }

            return new BestCastPosition {CastPosition = result.To3DWorld(), HitNumber = minionCount};
        }

        public struct BestCastPosition
        {
            public int HitNumber;
            public Vector3 CastPosition;
        }

        #endregion Vector

        #region Spells

        #region CanCast

        public static bool CanCast(this Obj_AI_Base target, Spell.SpellBase spell, Menu m)
        {
            if (spell == null) return false;
            return target.IsValidTarget(spell.Range) && spell.IsReady() && m.GetCheckBoxValue(spell.Slot.ToString().ToLower() + "Use");
        }

        public static bool CanCast(this Obj_AI_Base target, Spell.Active spell, Menu m)
        {
            var asBase = spell as Spell.SpellBase;
            return target.CanCast(asBase, m);
        }

        public static bool CanCast(this Obj_AI_Base target, Spell.Skillshot spell, Menu m, int hitchancePercent = 75)
        {
            var asBase = spell as Spell.SpellBase;
            var pred = spell.GetPrediction(target);
            return target.CanCast(asBase, m) && pred.HitChancePercent >= 75;
        }

        public static bool CanCast(this Obj_AI_Base target, Spell.Chargeable spell, Menu m)
        {
            var asBase = spell as Spell.SpellBase;
            return target.CanCast(asBase, m);
        }

        public static bool CanCast(this Obj_AI_Base target, Spell.Ranged spell, Menu m)
        {
            var asBase = spell as Spell.SpellBase;
            return target.CanCast(asBase, m);
        }

        public static bool CanCast(this Obj_AI_Base target, Spell.Targeted spell, Menu m)
        {
            var asBase = spell as Spell.SpellBase;
            return target.CanCast(asBase, m);
        }

        #endregion CanCast

        #region TryToCast

        public static bool TryToCast(this Spell.SpellBase spell, Obj_AI_Base target, Menu m)
        {
            if (target == null) return false;
            return target.CanCast(spell, m) && spell.Cast(target);
        }

        public static bool TryToCast(this Spell.Active spell, Obj_AI_Base target, Menu m)
        {
            if (target == null) return false;
            return target.CanCast(spell, m) && spell.Cast();
        }

        public static bool TryToCast(this Spell.Skillshot spell, Obj_AI_Base target, Menu m, int percent = 75)
        {
            if (target == null) return false;
            return target.CanCast(spell, m, percent) && spell.Cast(target);
        }

        public static bool TryToCast(this Spell.Targeted spell, Obj_AI_Base target, Menu m)
        {
            if (target == null) return false;
            return target.CanCast(spell, m) && spell.Cast(target);
        }

        public static bool TryToCast(this Spell.Chargeable spell, Obj_AI_Base target, Menu m)
        {
            if (target == null) return false;
            return target.CanCast(spell, m) && spell.Cast(target);
        }

        public static bool TryToCast(this Spell.Ranged spell, Obj_AI_Base target, Menu m)
        {
            if (target == null) return false;
            return target.CanCast(spell, m) && spell.Cast(target);
        }

        #endregion TryToCast

        #endregion Spells

        #region Menus

        #region Creating

        public static void CreateCheckBox(this Menu m, string displayName, string uniqueId, bool defaultValue = true)
        {
            try
            {
                m.Add(uniqueId, new CheckBox(displayName, defaultValue));
            }
            catch (Exception)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("Error creating the checkbox with the uniqueID = ");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(uniqueId);
                Console.ResetColor();
            }
        }

        public static void CreateSlider(this Menu m, string displayName, string uniqueId, int defaultValue = 0, int minValue = 0, int maxValue = 100)
        {
            try
            {
                m.Add(uniqueId, new Slider(displayName, defaultValue, minValue, maxValue));
            }
            catch (Exception)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("Error creating the slider with the uniqueID = ");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(uniqueId);
                Console.ResetColor();
            }
        }

        public static void CreateComboBox(this Menu m, string displayName, string uniqueId, List<string> options, int defaultValue = 0)
        {
            try
            {
                m.Add(uniqueId, new ComboBox(displayName, options, defaultValue));
            }
            catch (Exception)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("Error creating the combobox with the uniqueID = ");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(uniqueId);
                Console.ResetColor();
            }
        }

        #endregion Creating

        #region Getting

        public static bool GetCheckBoxValue(this Menu m, string uniqueId)
        {
            try
            {
                return m.Get<CheckBox>(uniqueId).CurrentValue;
            }
            catch (Exception)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("Error getting the checkbox with the uniqueID = ");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(uniqueId);
                Console.ResetColor();
            }
            return false;
        }

        public static int GetSliderValue(this Menu m, string uniqueId)
        {
            try
            {
                return m.Get<Slider>(uniqueId).CurrentValue;
            }
            catch (Exception)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("Error getting the slider with the uniqueID = ");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(uniqueId);
                Console.ResetColor();
            }
            return -1;
        }

        public static int GetComboBoxValue(this Menu m, string uniqueId)
        {
            try
            {
                return m.Get<ComboBox>(uniqueId).CurrentValue;
            }
            catch (Exception)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("Error getting the combobox with the uniqueID = ");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(uniqueId);
                Console.ResetColor();
            }
            return -1;
        }

        #endregion Getting

        #endregion Menus

        #region GetTargetHelper

        public static Obj_AI_Minion GetLastHitMinion(this Spell.SpellBase spell)
        {
            return
                EntityManager.MinionsAndMonsters.GetLaneMinions()
                    .FirstOrDefault(
                        m =>
                            m.IsValidTarget(spell.Range) && Prediction.Health.GetPrediction(m, spell.CastDelay) <= m.GetDamage(spell.Slot) &&
                            m.IsEnemy);
        }

        public static AIHeroClient GetKillableHero(this Spell.SpellBase spell)
        {
            return
                EntityManager.Heroes.Enemies.FirstOrDefault(
                    e =>
                        e.IsValidTarget(spell.Range) && Prediction.Health.GetPrediction(e, spell.CastDelay) <= e.GetDamage(spell.Slot) &&
                        !e.HasUndyingBuff());
        }

        public static Obj_AI_Base GetJungleMinion(this Spell.SpellBase spell)
        {
            return
                EntityManager.MinionsAndMonsters.GetJungleMonsters()
                    .OrderByDescending(m => m.Health)
                    .FirstOrDefault(m => m.IsValidTarget(spell.Range));
        }

        public static int CountEnemyLaneMinions(this Obj_AI_Base target, float range = 100)
        {
            return EntityManager.MinionsAndMonsters.GetLaneMinions().Count(m => m.Distance(target) <= range);
        }

        public static int CountEnemyJungleMinions(this Obj_AI_Base target, float range = 100)
        {
            return EntityManager.MinionsAndMonsters.GetJungleMonsters().Count(m => m.Distance(target) <= range);
        }

        #endregion GetTargetHelper

        #region Damages

        public static float GetAlliesDamagesNear(this Obj_AI_Base target, float percent = 0.7f, int range = 700)
        {
            var dmg = 0f;
            var slots = new[] {SpellSlot.Q, SpellSlot.W, SpellSlot.E, SpellSlot.R};

            foreach (var a in EntityManager.Heroes.Allies.Where(a => a.IsInRange(target, range)))
            {
                dmg += a.GetAutoAttackDamage(target);
                dmg += a.Spellbook.Spells.Where(s => slots.Contains(s.Slot) && s.IsReady).Sum(s => a.GetSpellDamage(target, s.Slot));
            }
            return dmg*percent;
        }

        public static float GetEnemiesDamagesNear(this Obj_AI_Base target, float percent = 0.7f, int range = 700)
        {
            var dmg = 0f;
            var slots = new[] {SpellSlot.Q, SpellSlot.W, SpellSlot.E, SpellSlot.R};

            foreach (var a in EntityManager.Heroes.Allies.Where(a => a.IsInRange(target, range)))
            {
                dmg += a.GetAutoAttackDamage(target);
                dmg += a.Spellbook.Spells.Where(s => slots.Contains(s.Slot) && s.IsReady).Sum(s => a.GetSpellDamage(target, s.Slot));
            }
            return dmg*percent;
        }

        public static float GetTotalDamage(this Obj_AI_Base target)
        {
            var slots = new[] {SpellSlot.Q, SpellSlot.W, SpellSlot.E, SpellSlot.R};
            var dmg = Player.Spells.Where(s => slots.Contains(s.Slot)).Sum(s => target.GetDamage(s.Slot));
            dmg += Orbwalker.CanAutoAttack ? Player.Instance.GetAutoAttackDamage(target) : 0f;

            return dmg;
        }

        public static bool HasMinionAggro(this Obj_AI_Base minion)
        {
            return HPPrediction.ActiveAttacks.Values.Any(m => m.Source is Obj_AI_Minion && m.Target.NetworkId == minion.NetworkId);
        }

        public static bool HasTurretAggro(this Obj_AI_Base minion)
        {
            return HPPrediction.ActiveAttacks.Values.Any(m => m.Source is Obj_AI_Turret && m.Target.NetworkId == minion.NetworkId);
        }

        public static int TurretAggroStartTick(this Obj_AI_Base minion)
        {
            var ActiveTurret = HPPrediction.ActiveAttacks.Values
                .FirstOrDefault(m => m.Source is Obj_AI_Turret && m.Target.NetworkId == minion.NetworkId);
            return ActiveTurret?.StartTick ?? 0;
        }

        public static Obj_AI_Base GetAggroTurret(this Obj_AI_Base minion)
        {
            var ActiveTurret = HPPrediction.ActiveAttacks.Values
                .FirstOrDefault(m => m.Source is Obj_AI_Turret && m.Target.NetworkId == minion.NetworkId);
            return ActiveTurret?.Source;
        }

        #region Items

        public static float GetEchoLudenDamage(this Obj_AI_Base target)
        {
            var dmg = 0f;
            var echo = new Item(ItemId.Ludens_Echo);

            if (echo.IsOwned() && Player.GetBuff("itemmagicshankcharge").Count == 100)
            {
                dmg += Player.Instance.CalculateDamageOnUnit(target, DamageType.Magical, (float) (100 + 0.1*Player.Instance.FlatMagicDamageMod));
            }
            return dmg;
        }

        public static float GetSheenDamage(this Obj_AI_Base target)
        {
            var sheenItems = new List<Item>
            {
                new Item(ItemId.Lich_Bane),
                new Item(ItemId.Trinity_Force),
                new Item(ItemId.Iceborn_Gauntlet),
                new Item(ItemId.Sheen)
            };
            var item = sheenItems.FirstOrDefault(i => i.IsReady() && i.IsOwned());
            if (item != null)
            {
                var AD = Player.Instance.FlatPhysicalDamageMod;
                var AP = Player.Instance.FlatMagicDamageMod;
                switch (item.Id)
                {
                    case ItemId.Lich_Bane:
                        return Player.Instance.CalculateDamageOnUnit(target, DamageType.Magical, AD*0.75f + AP*0.5f);
                    case ItemId.Trinity_Force:
                        return Player.Instance.CalculateDamageOnUnit(target, DamageType.Physical, AD*2f);
                    case ItemId.Iceborn_Gauntlet:
                        return Player.Instance.CalculateDamageOnUnit(target, DamageType.Physical, AD*1.25f);
                    case ItemId.Sheen:
                        return Player.Instance.CalculateDamageOnUnit(target, DamageType.Physical, AD*1f);
                }
            }

            return 0f;
        }

        #endregion Items

        #endregion Damages
    }
}