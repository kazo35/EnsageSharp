﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;

using Ensage;
using Ensage.Common;
using Ensage.SDK.Helpers;
using Ensage.SDK.Renderer.Particle;
using Ensage.SDK.Service;
using Ensage.SDK.Service.Metadata;
using Ensage.SDK.Renderer;
using Ensage.SDK.Extensions;

using SharpDX;

namespace VisibleByEnemyPlus
{
    [ExportPlugin("VisibleByEnemyPlus", StartupMode.Auto, "YEEEEEEE", "3.0.0.2")]
    public class VisibleByEnemyPlus : Plugin
    {
        private Unit Unit { get; }

        private Lazy<IParticleManager> ParticleManager { get; }

        private Lazy<IRendererManager> RendererManager { get; }

        private VisibleByEnemyPlusConfig Config { get; set; }

        private List<Vector3> PosShrine { get; } = new List<Vector3>();

        private List<Vector3> PosNeutral { get; } = new List<Vector3>();

        private bool AddEffectType { get; set; }

        private int Red => Config.RedItem.Value;

        private int Green => Config.GreenItem.Value;

        private int Blue => Config.BlueItem.Value;

        private int Alpha => Config.AlphaItem.Value;

        private Vector2 ExtraPos { get; set; }

        private int ExtraSize { get; set; }

        [ImportingConstructor]
        public VisibleByEnemyPlus(
            [Import] IServiceContext context, 
            [Import] Lazy<IParticleManager> particlemanager,
            [Import] Lazy<IRendererManager> renderermanager)
        {
            Unit = context.Owner;
            ParticleManager = particlemanager;
            RendererManager = renderermanager;
        }

        protected override void OnActivate()
        {
            Config = new VisibleByEnemyPlusConfig();

            Config.DrawMinimapItem.PropertyChanged += DrawMinimapChanged;
            Config.EffectTypeItem.PropertyChanged += ItemChanged;

            Config.RedItem.PropertyChanged += ItemChanged;
            Config.GreenItem.PropertyChanged += ItemChanged;
            Config.BlueItem.PropertyChanged += ItemChanged;
            Config.AlphaItem.PropertyChanged += ItemChanged;

            if (Drawing.RenderMode == RenderMode.Dx9)
            {
                ExtraPos = new Vector2(8, 7);
                ExtraSize = 18;
            }
            else if (Drawing.RenderMode == RenderMode.Dx11)
            {
                ExtraPos = new Vector2(5, 7);
                ExtraSize = 15;
            }

            UpdateManager.Subscribe(LoopEntities, 250);

            if (Config.DrawMinimapItem)
            {
                RendererManager.Value.Draw += OnDraw;
            }
        }

        protected override void OnDeactivate()
        {
            if (Config.DrawMinimapItem)
            {
                RendererManager.Value.Draw -= OnDraw;
            }

            UpdateManager.Unsubscribe(LoopEntities);

            Config.DrawMinimapItem.PropertyChanged -= DrawMinimapChanged;
            Config.EffectTypeItem.PropertyChanged -= ItemChanged;

            Config.RedItem.PropertyChanged -= ItemChanged;
            Config.GreenItem.PropertyChanged -= ItemChanged;
            Config.BlueItem.PropertyChanged -= ItemChanged;
            Config.AlphaItem.PropertyChanged -= ItemChanged;

            Config?.Dispose();
        }

        private void DrawMinimapChanged(object sender, PropertyChangedEventArgs e)
        {
            if (Config.DrawMinimapItem)
            {
                RendererManager.Value.Draw += OnDraw;
            }
            else
            {
                RendererManager.Value.Draw -= OnDraw;
            }
        }

        private void ItemChanged(object sender, PropertyChangedEventArgs e)
        {
            if (Config.EffectTypeItem.Value.SelectedIndex == 0)
            {
                Config.RedItem.Item.SetFontColor(Color.Black);
                Config.GreenItem.Item.SetFontColor(Color.Black);
                Config.BlueItem.Item.SetFontColor(Color.Black);
                Config.AlphaItem.Item.SetFontColor(Color.Black);
            }
            else
            {
                Config.RedItem.Item.SetFontColor(new Color(Red, 0, 0, 255));
                Config.GreenItem.Item.SetFontColor(new Color(0, Green, 0, 255));
                Config.BlueItem.Item.SetFontColor(new Color(0, 0, Blue, 255));
                Config.AlphaItem.Item.SetFontColor(new Color(185, 176, 163, Alpha));
            }

            Unit.Stop();

            HandleEffect(Unit, true);
            AddEffectType = false;
        }

        private bool IsMine(Entity sender)
        {
            return sender.ClassId == ClassId.CDOTA_NPC_TechiesMines;
        }

        private bool IsShrine(Entity sender)
        {
            return sender.ClassId == ClassId.CDOTA_BaseNPC_Healer;
        }

        private bool IsPos(Vector3 pos)
        {
            return pos == new Vector3(-4224, 1279.969f, 384)
                || pos == new Vector3(639.9688f, -2560, 384)
                || pos == new Vector3(4191.969f, -1600, 385.1875f)
                || pos == new Vector3(-128.0313f, 2528, 385.1875f);
        }

        private bool IsNeutral(Unit sender)
        {
            return sender.ClassId == ClassId.CDOTA_BaseNPC_Creep_Neutral;
        }

        private bool IsUnit(Unit sender)
        {
            return !(sender is Hero) && !(sender is Building)
                   && (sender.ClassId != ClassId.CDOTA_BaseNPC_Creep_Lane 
                   && sender.ClassId != ClassId.CDOTA_BaseNPC_Creep_Siege 
                   || sender.IsControllable)
                   && sender.ClassId != ClassId.CDOTA_NPC_TechiesMines 
                   && sender.ClassId != ClassId.CDOTA_NPC_Observer_Ward
                   && sender.ClassId != ClassId.CDOTA_NPC_Observer_Ward_TrueSight
                   && sender.ClassId != ClassId.CDOTA_BaseNPC_Healer;
        }

        private bool IsWard(Entity sender)
        {
            return sender.ClassId == ClassId.CDOTA_NPC_Observer_Ward 
                || sender.ClassId == ClassId.CDOTA_NPC_Observer_Ward_TrueSight;
        }

        private void LoopEntities()
        {
            if (Config.AlliedHeroesItem)
            {
                foreach (var hero in EntityManager<Hero>.Entities.Where(x => x.Team == Unit.Team))
                {
                    HandleEffect(hero, hero.IsVisibleToEnemies);
                }
            }

            if (Config.BuildingsItem)
            {
                foreach (var building in EntityManager<Building>.Entities.Where(x => x.Team == Unit.Team))
                {
                    HandleEffect(building, building.IsVisibleToEnemies);
                }
            }

            if (Config.NeutralsItem)
            {
                foreach (var neutral in EntityManager<Unit>.Entities.Where(IsNeutral))
                {
                    HandleEffect(neutral, neutral.IsVisibleToEnemies);
                }
            }

            var Units = EntityManager<Unit>.Entities.Where(x => x.Team == Unit.Team).ToList();

            if (Config.WardsItem )
            {
                foreach (var ward in Units.Where(IsWard))
                {
                    HandleEffect(ward, ward.IsVisibleToEnemies);
                }
            }

            if (Config.MinesItem)
            {
                foreach (var mine in Units.Where(IsMine))
                {
                    HandleEffect(mine, mine.IsVisibleToEnemies);
                }
            }

            if (Config.ShrinesItem)
            {
                foreach (var shrine in Units.Where(IsShrine))
                {
                    HandleEffect(shrine, shrine.IsVisibleToEnemies);
                }
            }

            if (Config.UnitsItem)
            {
                foreach (var unit in Units.Where(IsUnit))
                {
                    HandleEffect(unit, unit.IsVisibleToEnemies);
                }
            }
        }

        private void HandleEffect(Unit unit, bool visible)
        {
            if (!AddEffectType && Unit.Animation.Name != "idle")
            {
                AddEffectType = true;
            }

            if (!unit.IsValid)
            {
                return;
            }

            if (visible && unit.IsAlive)
            {
                ParticleManager.Value.AddOrUpdate(
                    unit,
                    $"unit_{unit.Handle}",
                    Config.Effects[Config.EffectTypeItem.Value.SelectedIndex],
                    ParticleAttachment.AbsOriginFollow,
                    true,
                    1,
                    new Vector3(Red, Green, Blue),
                    2,
                    new Vector3(Alpha));

                if (!PosShrine.Any(x => x == unit.Position))
                {
                    if (IsPos(unit.Position))
                    {
                        PosShrine.Add(unit.Position);
                    }
                }
            }
            else if (AddEffectType)
            {
                ParticleManager.Value.Remove($"unit_{unit.Handle}");
                PosShrine.Remove(unit.Position);
            }

            if (visible && unit.IsVisible && unit.IsAlive && IsNeutral(unit) && unit.IsSpawned)
            {
                if (!PosNeutral.Any(x => x == unit.Position))
                {
                    PosNeutral.RemoveAll(x => x.Distance(unit.Position) < 500);
                    PosNeutral.Add(unit.Position);
                }
            }
            else if (AddEffectType)
            {
                PosNeutral.Remove(unit.Position);
            }
        }

        private void OnDraw(object sender, EventArgs e)
        {
            foreach (var pos in PosShrine.ToList())
            {
                RendererManager.Value.DrawText(
                    pos.WorldToMinimap() - ExtraPos,
                    "V",
                    System.Drawing.Color.FromArgb(
                        Config.DrawRedItem,
                        Config.DrawGreenItem,
                        Config.DrawBlueItem),
                    ExtraSize,
                    "Arial Black");
            }

            if (Config.DrawNeutralsItem)
            {
                foreach (var pos in PosNeutral.ToList())
                {
                    RendererManager.Value.DrawText(
                        pos.WorldToMinimap() - new Vector2(0, 5) - ExtraPos,
                        "●",
                        System.Drawing.Color.FromArgb(
                            Config.DrawAlphaItem,
                            Config.DrawRedItem,
                            Config.DrawGreenItem,
                            Config.DrawBlueItem),
                        ExtraSize,
                        "Arial Black");
                }
            }
        }
    }
}