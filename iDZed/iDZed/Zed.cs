﻿// This file is part of LeagueSharp.Common.
// 
// LeagueSharp.Common is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// LeagueSharp.Common is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with LeagueSharp.Common.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;

namespace iDZed
{
    internal static class Zed
    {
        private static Menu _menu;
        private static Orbwalking.Orbwalker _orbwalker;

        private static Obj_AI_Hero Player
        {
            get { return ObjectManager.Player; }
        }

        private delegate void OnOrbwalkingMode();

        // ReSharper disable once InconsistentNaming
        private static readonly Dictionary<SpellSlot, Spell> _spells = new Dictionary<SpellSlot, Spell>
        {
            { SpellSlot.Q, new Spell(SpellSlot.Q, 900f) },
            { SpellSlot.W, new Spell(SpellSlot.W, 550f) },
            { SpellSlot.E, new Spell(SpellSlot.E, 290f) },
            { SpellSlot.R, new Spell(SpellSlot.R, 625f) }
        };

        private static Dictionary<Orbwalking.OrbwalkingMode, OnOrbwalkingMode> _orbwalkingModesDictionary;

        public static void OnLoad()
        {
            Game.PrintChat("iDZed loaded!");
            ShadowManager.OnLoad();
            _orbwalkingModesDictionary = new Dictionary<Orbwalking.OrbwalkingMode, OnOrbwalkingMode>
            {
                { Orbwalking.OrbwalkingMode.Combo, Combo },
                { Orbwalking.OrbwalkingMode.Mixed, Harass },
                { Orbwalking.OrbwalkingMode.LastHit, Farm },
                { Orbwalking.OrbwalkingMode.LaneClear, Farm },
                { Orbwalking.OrbwalkingMode.None, () => { } }
            };
            InitMenu();
            InitSpells();
            InitEvents();
        }

        #region superduper combos

        private static void DoLineCombo(Obj_AI_Hero target)
        {
            if (ShadowManager.RShadow.ShadowObject == null && ShadowManager.RShadow.State == ShadowState.NotActive)
            {
                if (_spells[SpellSlot.R].IsReady() && _spells[SpellSlot.R].IsInRange(target))
                {
                    _spells[SpellSlot.R].Cast(target);
                }
            }

            if (ShadowManager.RShadow.ShadowObject != null)
            {
                Vector3 wCastLocation = Player.ServerPosition -
                                        Vector3.Normalize(target.ServerPosition - Player.ServerPosition) * 400;

                if (ShadowManager.WShadow.ShadowObject == null && ShadowManager.WShadow.State == ShadowState.NotActive)
                {
                    _spells[SpellSlot.W].Cast(wCastLocation);
                }
            }

            if (ShadowManager.WShadow.ShadowObject != null && ShadowManager.RShadow.ShadowObject != null &&
                ShadowManager.WShadow.State == ShadowState.Created && ShadowManager.RShadow.State == ShadowState.Created)
            {
                CastQ(target, true);
                CastE();
            }
        }

        private static void DoShadowCoax(Obj_AI_Hero target) {}

        #endregion

        #region Spell Casting

        private static void CastQ(Obj_AI_Hero target, bool usePrediction = false)
        {
            if (_spells[SpellSlot.Q].IsReady())
            {
                if (ShadowManager.WShadow != null && ShadowManager.WShadow.State == ShadowState.Created)
                {
                    _spells[SpellSlot.Q].UpdateSourcePosition(
                        ShadowManager.WShadow.ShadowObject.Position, ShadowManager.WShadow.ShadowObject.Position);
                    if (usePrediction)
                    {
                        var prediction = _spells[SpellSlot.Q].GetPrediction(target);
                        if (prediction.Hitchance >= HitChance.Medium)
                        {
                            _spells[SpellSlot.Q].Cast(prediction.CastPosition);
                        }
                    }
                    else
                    {
                        _spells[SpellSlot.Q].Cast(target);
                    }
                }
                else
                {
                    _spells[SpellSlot.Q].UpdateSourcePosition(Player.ServerPosition, Player.ServerPosition);
                    if (usePrediction)
                    {
                        var prediction = _spells[SpellSlot.Q].GetPrediction(target);
                        if (prediction.Hitchance >= HitChance.Medium)
                        {
                            _spells[SpellSlot.Q].Cast(prediction.CastPosition);
                        }
                    }
                    else
                    {
                        _spells[SpellSlot.Q].Cast(target);

                    }
                }
            }
        }

        private static void CastW(Obj_AI_Hero target)
        {
            if (ShadowManager.WShadow.State == ShadowState.NotActive)
            {
                if (_spells[SpellSlot.W].IsReady())
                {
                    Vector2 position = Player.ServerPosition.To2D()
                        .Extend(target.ServerPosition.To2D(), _spells[SpellSlot.W].Range);
                    if (position.Distance(target) <= _spells[SpellSlot.Q].Range)
                    {
                        _spells[SpellSlot.W].Cast(position);
                    }
                }
            }
        }

        private static void CastE()
        {
            if (!_spells[SpellSlot.E].IsReady())
            {
                return;
            }
            if (
                HeroManager.Enemies.Count(
                    hero =>
                        hero.IsValidTarget() &&
                        (hero.Distance(Player.ServerPosition) <= _spells[SpellSlot.E].Range ||
                         (ShadowManager.WShadow.ShadowObject != null &&
                          hero.Distance(ShadowManager.WShadow.ShadowObject.ServerPosition) <= _spells[SpellSlot.E].Range) ||
                         (ShadowManager.RShadow.ShadowObject != null &&
                          hero.Distance(ShadowManager.RShadow.ShadowObject.ServerPosition) <= _spells[SpellSlot.E].Range))) >
                0)
            {
                _spells[SpellSlot.E].Cast();
            }
        }

        #endregion

        #region Modes Region

        private static void Combo()
        {
            Obj_AI_Hero target = TargetSelector.GetTarget(
                _spells[SpellSlot.W].Range + _spells[SpellSlot.Q].Range, TargetSelector.DamageType.Physical);

            if (_spells[SpellSlot.R].IsReady() && _spells[SpellSlot.W].IsReady() && _spells[SpellSlot.E].IsReady() &&
                _spells[SpellSlot.Q].IsReady())
            {
                if (_menu.Item("com.idz.zed.combo.user").GetValue<bool>())
                {
                    DoLineCombo(target);
                }
            }
            else
            {
                if (_menu.Item("com.idz.zed.combo.usew").GetValue<bool>())
                {
                    CastW(target);
                }
                if (_menu.Item("com.idz.zed.combo.useq").GetValue<bool>())
                {
                    CastQ(target, true);
                }
                if (_menu.Item("com.idz.zed.combo.usee").GetValue<bool>())
                {
                    CastE();
                }
            }
        }

        private static void Harass()
        {
            if (!_menu.Item("com.idz.zed.harass.useHarass").GetValue<bool>())
            {
                return;
            }

            Obj_AI_Hero target = TargetSelector.GetTarget(
                _spells[SpellSlot.W].Range + _spells[SpellSlot.Q].Range, TargetSelector.DamageType.Physical);
            switch (_menu.Item("com.idz.zed.harass.harassMode").GetValue<StringList>().SelectedIndex)
            {
                case 0: // "Q-E"
                    CastQ(target, true);
                    CastE();
                    break;
                case 1: //"W-E-Q"
                    if (ShadowManager.WShadow.ShadowObject == null &&
                        ShadowManager.WShadow.State == ShadowState.NotActive)
                    {
                        var position = Player.ServerPosition.To2D()
                            .Extend(target.ServerPosition.To2D(), _spells[SpellSlot.E].Range);
                        if (position.Distance(target) <= _spells[SpellSlot.Q].Range)
                        {
                            _spells[SpellSlot.W].Cast(target);
                        }
                    }
                    if (ShadowManager.WShadow.State == ShadowState.Travelling) //TODO this is fast harass m8 :S
                    {
                        if (_spells[SpellSlot.E].IsReady())
                        {
                            _spells[SpellSlot.E].Cast();
                        }
                        if (_spells[SpellSlot.Q].IsReady())
                        {
                            _spells[SpellSlot.Q].Cast(target.ServerPosition);
                        }
                    }

                    break;
                case 2: //"W-Q-E" 
                    if (ShadowManager.WShadow.ShadowObject == null &&
                        ShadowManager.WShadow.State == ShadowState.NotActive)
                    {
                        var position = Player.ServerPosition.To2D()
                            .Extend(target.ServerPosition.To2D(), _spells[SpellSlot.E].Range);
                        if (position.Distance(target) <= _spells[SpellSlot.Q].Range)
                        {
                            _spells[SpellSlot.W].Cast(target);
                        }
                    }
                    if (ShadowManager.WShadow.State == ShadowState.Travelling) //TODO this is fast harass m8 :S
                    {
                        if (_spells[SpellSlot.Q].IsReady())
                        {
                            _spells[SpellSlot.Q].Cast(target.ServerPosition);
                        }

                        if (_spells[SpellSlot.E].IsReady())
                        {
                            _spells[SpellSlot.E].Cast();
                        }
                    }
                    break;
            }
        }

        private static void Farm()
        {
            var allMinions = MinionManager.GetMinions(Player.ServerPosition, 1000f);
            var qMinion =
                allMinions.FirstOrDefault(
                    x => x.IsValidTarget(_spells[SpellSlot.Q].Range) && _spells[SpellSlot.Q].IsInRange(x));
            if (_menu.Item("com.idz.zed.farm.useQ").GetValue<bool>() && _spells[SpellSlot.Q].IsReady())
            {
                if (qMinion != null && _spells[SpellSlot.Q].GetDamage(qMinion) > qMinion.Health)
                {
                    _spells[SpellSlot.Q].Cast(qMinion);
                }
            }
            if (_menu.Item("com.idz.zed.farm.useE").GetValue<bool>() && _spells[SpellSlot.E].IsReady())
            {
                foreach (var minion in
                    MinionManager.GetMinions(Player.ServerPosition, _spells[SpellSlot.E].Range)
                        .Where(
                            minion =>
                                !Orbwalking.InAutoAttackRange(minion) &&
                                minion.Health < 0.75 * _spells[SpellSlot.E].GetDamage(minion)))
                {
                    _spells[SpellSlot.E].Cast(minion);
                }
            }
        }

        #endregion

        #region Initialization Region

        private static void InitMenu()
        {
            _menu = new Menu("iDZed - Reloaded", "com.idz.zed", true);
            Menu tsMenu = new Menu("[iDZed] TargetSelector", "com.idz.zed.targetselector");
            TargetSelector.AddToMenu(tsMenu);
            _menu.AddSubMenu(tsMenu);

            Menu orbwalkMenu = new Menu("[iDZed] Orbwalker", "com.idz.zed.orbwalker");
            _orbwalker = new Orbwalking.Orbwalker(orbwalkMenu);
            _menu.AddSubMenu(orbwalkMenu);

            Menu comboMenu = new Menu("[iDZed] Combo", "com.idz.zed.combo");
            {
                comboMenu.AddItem(new MenuItem("com.idz.zed.combo.useq", "Use Q").SetValue(true));
                comboMenu.AddItem(new MenuItem("com.idz.zed.combo.usew", "Use W").SetValue(true));
                comboMenu.AddItem(new MenuItem("com.idz.zed.combo.usee", "Use E").SetValue(true));
                comboMenu.AddItem(new MenuItem("com.idz.zed.combo.user", "Use R").SetValue(true));
                comboMenu.AddItem(new MenuItem("com.idz.zed.combo.swapw", "Swap W For Follow").SetValue(false));
                comboMenu.AddItem(new MenuItem("com.idz.zed.combo.swapr", "Swap R On kill").SetValue(true));
                comboMenu.AddItem(
                    new MenuItem("com.idz.zed.combo.mode", "Combo Mode").SetValue(
                        new StringList(new[] { "Normal Mode", "Burst Mode" })));
            }
            _menu.AddSubMenu(comboMenu);

            Menu harassMenu = new Menu("[iDZed] Harass", "com.idz.zed.harass");
            {
                harassMenu.AddItem(new MenuItem("com.idz.zed.harass.useHarass", "Use Harass").SetValue(true));
                harassMenu.AddItem(
                    new MenuItem("com.idz.zed.harass.harassMode", "Harass Mode").SetValue(
                        new StringList(new[] { "Q-E", "W-E-Q", "W-Q-E" })));
            }
            _menu.AddSubMenu(harassMenu);

            Menu farmMenu = new Menu("[iDZed] Farm", "com.idz.zed.farm");
            {
                farmMenu.AddItem(new MenuItem("com.idz.zed.farm.useQ", "Use Q in Farm").SetValue(true));
                farmMenu.AddItem(new MenuItem("com.idz.zed.farm.useE", "Use E in Farm").SetValue(true));
            }
            _menu.AddSubMenu(farmMenu);

            _menu.AddToMainMenu();
        }

        private static void InitSpells()
        {
            _spells[SpellSlot.Q].SetSkillshot(0.25f, 50f, 1700f, false, SkillshotType.SkillshotLine);
            _spells[SpellSlot.W].SetSkillshot(.25f, 270f, float.MaxValue, false, SkillshotType.SkillshotCircle);
            _spells[SpellSlot.E].SetSkillshot(0f, 220f, float.MaxValue, false, SkillshotType.SkillshotCircle);
        }

        private static void InitEvents()
        {
            Game.OnUpdate += Game_OnUpdate;
            Drawing.OnDraw += Drawing_OnDraw;
        }

        #endregion

        #region Events Region

        private static void Game_OnUpdate(EventArgs args)
        {
            _orbwalkingModesDictionary[_orbwalker.ActiveMode]();
        }

        private static void Drawing_OnDraw(EventArgs args)
        {
            foreach (Shadow shadow in
                ShadowManager._shadowsList.Where(sh => sh.State != ShadowState.NotActive && sh.ShadowObject != null))
            {
                Render.Circle.DrawCircle(shadow.ShadowObject.Position, 60f, System.Drawing.Color.Orange);
            }
        }

        #endregion
    }
}