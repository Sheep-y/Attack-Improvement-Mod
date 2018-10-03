using BattleTech.UI;
using BattleTech;
using Localize;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Sheepy.BattleTechMod.AttackImprovementMod {
   using static Mod;
   using static System.Reflection.BindingFlags;

   public class UserInterfaceWeapons : BattleModModule {

      public override void CombatStartsOnce () {
         Type slotType = typeof( CombatHUDWeaponSlot ), panelType = typeof( CombatHUDWeaponPanel );

         if ( Settings.AggressiveMultiTargetAssignment ) {
            SlotSetTargetIndexMethod = slotType.GetMethod( "SetTargetIndex", NonPublic | Instance );
            if ( SlotSetTargetIndexMethod != null ) {
               Patch( panelType, "OnActorMultiTargeted", "OverrideMultiTargetAssignment", null );
               Patch( panelType, "OnActorMultiTargetCleared", "OverrideMultiTargetAssignment", null );
            } else
               Warn( "CombatHUDWeaponSlot.SetTargetIndex not found. AggressiveMultiTargetAssignment not patched." );
         }

         if ( Settings.ShowBaseHitchance ) {
            Patch( slotType, "UpdateToolTipsFiring", typeof( ICombatant ), "ShowBaseHitChance", null );
            Patch( slotType, "UpdateToolTipsMelee", typeof( ICombatant ), "ShowBaseMeleeChance", null );
         }
         if ( Settings.ShowNeutralRangeInBreakdown ) {
            rangedPenalty = ModifierList.GetRangedModifierFactor( "range" );
            Patch( slotType, "UpdateToolTipsFiring", typeof( ICombatant ), "ShowNeutralRange", null );
         }
         if ( Settings.ShowWeaponProp || Settings.WeaponRangeFormat != null )
            Patch( slotType, "GenerateToolTipStrings", null, "UpdateWeaponTooltip" );

         if ( Settings.ShowReducedWeaponDamage || Settings.CalloutWeaponStability )
            Patch( slotType, "RefreshDisplayedWeapon", null, "UpdateWeaponDamage" );
         if ( Settings.ShowTotalWeaponDamage ) {
            Patch( panelType, "ShowWeaponsUpTo", null, "ShowTotalDamageSlot" );
            Patch( panelType, "RefreshDisplayedWeapons", "ResetTotalWeaponDamage", "ShowTotalWeaponDamage" );
         }
         if ( Settings.ShowReducedWeaponDamage || Settings.ShowTotalWeaponDamage ) {
            // Update damage numbers (and multi-target highlights) _after_ all slots are in a correct state.
            Patch( typeof( SelectionStateFireMulti ), "SetTargetedCombatant", null, "RefreshTotalDamage" );
            Patch( slotType, "OnPointerUp", null, "RefreshTotalDamage" );
         }
         if ( Settings.CalloutWeaponStability )
            Patch( typeof( CombatSelectionHandler ), "ProcessInput", "ToggleStabilityDamage", null );

         if ( HasMod( "com.joelmeador.WeaponRealizer", "WeaponRealizer.Core" ) ) TryRun( ModLog, InitWeaponRealizerBridge );
      }

      private void InitWeaponRealizerBridge () {
         Assembly WeaponRealizer = AppDomain.CurrentDomain.GetAssemblies().First( e => e.GetName().Name == "WeaponRealizer" );
         Type WeaponRealizerCalculator = WeaponRealizer?.GetType( "WeaponRealizer.Calculator" );
         WeaponRealizerDamageModifiers = WeaponRealizerCalculator?.GetMethod( "ApplyAllDamageModifiers", Static | NonPublic );
         if ( WeaponRealizerDamageModifiers == null )
            BattleMod.BTML_LOG.Warn( "Attack Improvement Mod cannot bridge with WeaponRealizer. Damage prediction may be inaccurate." );
         else
            Info( "Attack Improvement Mod has bridged with WeaponRealizer.Calculator on damage prediction." );
      }

      // ============ Multi-Target target selection ============

      private static MethodInfo SlotSetTargetIndexMethod;

      public static bool OverrideMultiTargetAssignment ( CombatHUDWeaponPanel __instance, List<CombatHUDWeaponSlot> ___WeaponSlots ) { try {
         SelectionStateFireMulti multi = ActiveState as SelectionStateFireMulti;
         List<ICombatant> targets = multi?.AllTargetedCombatants;
         if ( targets.IsNullOrEmpty() ) return true;
            foreach ( CombatHUDWeaponSlot slot in ___WeaponSlots ) {
            Weapon w = slot?.DisplayedWeapon;
            if ( w == null || w.Category == WeaponCategory.Melee ) continue;
            float hitChance = 0;
            foreach ( ICombatant target in targets ) {
               if ( ! w.IsEnabled || ! w.WillFireAtTarget( target ) ) continue;
               float newChance = Combat.ToHit.GetToHitChance( w.parent, w, target, w.parent.CurrentPosition, target.CurrentPosition, 1, MeleeAttackType.NotSet, false );
               if ( newChance <= hitChance ) continue;
               SlotSetTargetIndexMethod.Invoke( slot, new object[]{ multi.AssignWeaponToTarget( w, target ), false } );
               hitChance = newChance;
            }
         }
         __instance.RefreshDisplayedWeapons();
         return false;
      }                 catch ( Exception ex ) { return Error( ex ); } }

      // ============ Mouseover Hint ============

      public static void ShowBaseHitChance ( CombatHUDWeaponSlot __instance, ICombatant target ) { try {
         if ( HUD.SelectedActor is Mech mech ) {
            float baseChance = RollModifier.StepHitChance( Combat.ToHit.GetBaseToHitChance( mech ) ) * 100;
            __instance.ToolTipHoverElement.BuffStrings.Add( new Text( "{0} {1} = {2:0}%", Translate( Pilot.PILOTSTAT_GUNNERY ), mech.SkillGunnery, baseChance ) );
         }
      }                 catch ( Exception ex ) { Error( ex ); } }

      public static void ShowBaseMeleeChance ( CombatHUDWeaponSlot __instance, ICombatant target ) { try {
         if ( HUD.SelectedActor is Mech mech ) {
            float baseChance = RollModifier.StepHitChance( Combat.ToHit.GetBaseMeleeToHitChance( mech ) ) * 100;
            __instance.ToolTipHoverElement.BuffStrings.Add( new Text( "{0} {1} = {2:0}%", Translate( Pilot.PILOTSTAT_PILOTING ), mech.SkillPiloting, baseChance ) );
         }
      }                 catch ( Exception ex ) { Error( ex ); } }

      private static Func<ModifierList.AttackModifier> rangedPenalty;

      public static void ShowNeutralRange ( CombatHUDWeaponSlot __instance, ICombatant target ) { try {
         ModifierList.AttackModifier range = rangedPenalty();
         if ( range.Value != 0 ) return;
         __instance.ToolTipHoverElement.BuffStrings.Add( new Text( range.DisplayName ) );
      }                 catch ( Exception ex ) { Error( ex ); } }

      public static void UpdateWeaponTooltip ( CombatHUDWeaponSlot __instance ) { try {
         Weapon weapon = __instance.DisplayedWeapon;
         List<Text> spec = __instance.ToolTipHoverElement?.WeaponStrings;
         if ( weapon == null || spec == null || spec.Count != 3 ) return;
         if ( Settings.ShowWeaponProp && ! string.IsNullOrEmpty( weapon.weaponDef.BonusValueA ) )
            spec[0] = new Text( string.IsNullOrEmpty( weapon.weaponDef.BonusValueB ) ? "{0}" : "{0}, {1}", weapon.weaponDef.BonusValueA, weapon.weaponDef.BonusValueB );
         if ( Settings.WeaponRangeFormat != null )
            spec[2] = new Text( Settings.WeaponRangeFormat, weapon.MinRange, weapon.ShortRange, weapon.MediumRange, weapon.LongRange, weapon.MaxRange );
      }                 catch ( Exception ex ) { Error( ex ); } }

      // ============ Weapon Panel ============

      private static MethodInfo WeaponRealizerDamageModifiers;

      private static float TotalDamage, AverageDamage;
      private static CombatHUDWeaponSlot TotalSlot;

      private static void AddToTotalDamage ( float dmg, CombatHUDWeaponSlot slot ) {
         Weapon w = slot.DisplayedWeapon;
         if ( ! w.IsEnabled ) return;
         float chance = slot.HitChance, multiplied = dmg * w.ShotsWhenFired;
         ICombatant target = HUD.SelectedTarget;
         if ( chance <= 0 ) {
            if ( target != null ) return; // Hit Chance is -999.9 if it can't fire at target (Method ClearHitChance)
            chance = 1; // Otherwise, preview full damage when no target is selected.
         }
         SelectionState state = HUD.SelectionHandler.ActiveState;
         if ( state is SelectionStateFireMulti multi
           && state.AllTargetedCombatants.Contains( HUD.SelectedTarget )
           && HUD.SelectedTarget != multi.GetSelectedTarget( w ) ) return;
         TotalDamage += multiplied;
         AverageDamage += multiplied * chance;
      }

      public static void UpdateWeaponDamage ( CombatHUDWeaponSlot __instance, ICombatant target ) { try {
         Weapon weapon = __instance.DisplayedWeapon;
         if ( weapon == null || weapon.Category == WeaponCategory.Melee || ! weapon.CanFire ) return;
         string text = null;
         if ( Settings.CalloutFriendlyFire && BTInput.Instance.Combat_ToggleCallouts().IsPressed ) {
            float dmg = weapon.Instability();
            if ( Settings.ShowReducedWeaponDamage && target is AbstractActor actor )
               dmg *= actor.StatCollection.GetValue<float>("ReceivedInstabilityMultiplier") * actor.EntrenchedMultiplier;
            AddToTotalDamage( dmg, __instance );
            text = FormatStabilityDamage( dmg );
         } else {
            if ( ActiveState is SelectionStateFireMulti multi && __instance.TargetIndex < 0 ) return;
            Vector2 position = ActiveState?.PreviewPos ?? weapon.parent.CurrentPosition;
            float raw = weapon.DamagePerShotAdjusted(), dmg = raw; // damage displayed by vanilla
            if ( target != null ) {
               dmg = weapon.DamagePerShotFromPosition( MeleeAttackType.NotSet, position, target ); // damage with all masks and reductions factored
               if ( WeaponRealizerDamageModifiers != null )
                  dmg = (float) WeaponRealizerDamageModifiers.Invoke( null, new object[]{ weapon.parent, target, weapon, dmg, false } );
            }
            AddToTotalDamage( dmg, __instance );
            if ( target == null || Math.Abs( raw - dmg ) < 0.01 ) return;
            text = ( (int) dmg ).ToString();
         }
         if ( weapon.HeatDamagePerShot > 0 )
            text = string.Format( HUD.WeaponPanel.HeatFormatString, text, Mathf.RoundToInt( weapon.HeatDamagePerShot ) );
         if ( weapon.ShotsWhenFired > 1 )
            text = string.Format( "{0}</color> (x{1})", text, weapon.ShotsWhenFired );
         __instance.DamageText.SetText( text, new object[0] );
      }                 catch ( Exception ex ) { Error( ex ); } }

      public static void ShowTotalDamageSlot ( CombatHUDWeaponPanel __instance, int topIndex, List<CombatHUDWeaponSlot> ___WeaponSlots ) { try {
         TotalSlot = null;
         if ( topIndex <= 0 || topIndex >= ___WeaponSlots.Count || __instance.DisplayedActor == null || ! ( __instance.DisplayedActor is Mech mech ) ) return;
         TotalSlot = ___WeaponSlots[ topIndex ];
         TotalSlot.transform.parent.gameObject.SetActive( true );
         TotalSlot.DisplayedWeapon = null;
         TotalSlot.WeaponText.text = Translate( "Total" );
         TotalSlot.AmmoText.text = "";
         TotalSlot.MainImage.color = Color.clear;
         TotalSlot.ToggleButton.childImage.color = Color.clear;
      }                 catch ( Exception ex ) { Error( ex ); } }

      public static void ResetTotalWeaponDamage () {
         TotalDamage = AverageDamage = 0;
      }

      public static void ShowTotalWeaponDamage ( List<CombatHUDWeaponSlot> ___WeaponSlots ) { try {
         if ( TotalSlot == null ) return;
         if ( ! Settings.ShowReducedWeaponDamage ) { // Sum damage when reduced damage patch is not applied.
            bool ShowStability = Settings.CalloutFriendlyFire && BTInput.Instance.Combat_ToggleCallouts().IsPressed;
            foreach ( CombatHUDWeaponSlot slot in ___WeaponSlots ) {
               Weapon w = slot.DisplayedWeapon;
               if ( w != null && w.IsEnabled && w.CanFire )
                  AddToTotalDamage( ShowStability ? w.Instability() : w.DamagePerShotAdjusted(), slot );
            }
         }
         TotalSlot.DamageText.text = FormatTotalDamage( TotalDamage, true );
         TotalSlot.HitChanceText.text = FormatTotalDamage( AverageDamage, true );
      }                 catch ( Exception ex ) { Error( ex ); } }

      public static void RefreshTotalDamage () {
         if ( ActiveState is SelectionStateFireMulti )
            HUD.WeaponPanel.RefreshDisplayedWeapons(); // Refresh weapon highlight AFTER HUD.SelectedTarget is updated.
      }

      private static bool ShowingStabilityDamage = false;

      public static void ToggleStabilityDamage () { try {
         bool CalloutPressed = BTInput.Instance.Combat_ToggleCallouts().IsPressed;
         if ( ShowingStabilityDamage != CalloutPressed ) {
            ShowingStabilityDamage = CalloutPressed;
            HUD.WeaponPanel.RefreshDisplayedWeapons();
         }
      }                 catch ( Exception ex ) { Error( ex ); } }

      // ============ Helpers ============

      private const string StabilityPrefix = "<#FFFF00>", StabilityPostfix = "s";

      private static string FormatStabilityDamage ( float dmg, bool alwaysShowNumber = false ) {
         if ( dmg < 1 && ! alwaysShowNumber ) return StabilityPrefix + "--";
         return StabilityPrefix + (int) dmg + StabilityPostfix;
      }

      private static string FormatTotalDamage ( float dmg, bool alwaysShowNumber = false ) {
         if ( ShowingStabilityDamage ) return FormatStabilityDamage( dmg, alwaysShowNumber );
         return ( (int) dmg ).ToString();
      }
  }
}