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

   public class WeaponInfo : BattleModModule {

      public override void CombatStartsOnce () {
         Type SlotType = typeof( CombatHUDWeaponSlot ), PanelType = typeof( CombatHUDWeaponPanel );

         if ( Settings.SaturationOfLoadout != 0 || Settings.ShowDamageInLoadout || Settings.ShowMeleeDamageInLoadout || Settings.ShowAlphaDamageInLoadout != null )
            Patch( typeof( CombatHUDTargetingComputer ), "RefreshWeaponList", null, "EnhanceWeaponLoadout" );

         if ( Settings.ShowBaseHitchance ) {
            Patch( SlotType, "UpdateToolTipsFiring", typeof( ICombatant ), "ShowBaseHitChance", null );
            Patch( SlotType, "UpdateToolTipsMelee", typeof( ICombatant ), "ShowBaseMeleeChance", null );
         }
         if ( Settings.ShowNeutralRangeInBreakdown ) {
            rangedPenalty = ModifierList.GetRangedModifierFactor( "range" );
            Patch( SlotType, "UpdateToolTipsFiring", typeof( ICombatant ), "ShowNeutralRange", null );
         }
         if ( Settings.ShowWeaponProp || Settings.WeaponRangeFormat != null )
            Patch( SlotType, "GenerateToolTipStrings", null, "UpdateWeaponTooltip" );

         if ( Settings.ShowReducedWeaponDamage || Settings.CalloutWeaponStability )
            Patch( SlotType, "RefreshDisplayedWeapon", null, "UpdateWeaponDamage" );
         if ( Settings.ShowTotalWeaponDamage ) {
            Patch( PanelType, "ShowWeaponsUpTo", null, "ShowTotalDamageSlot" );
            Patch( PanelType, "RefreshDisplayedWeapons", "ResetTotalWeaponDamage", "ShowTotalWeaponDamage" );
            Patch( SlotType, "RefreshHighlighted", "BypassTotalSlotHighlight", null );
         }
         if ( Settings.ShowReducedWeaponDamage || Settings.ShowTotalWeaponDamage ) {
            // Update damage numbers (and multi-target highlights) _after_ all slots are in a correct state.
            Patch( typeof( SelectionStateFireMulti ), "SetTargetedCombatant", null, "RefreshTotalDamage" );
            Patch( SlotType, "OnPointerUp", null, "RefreshTotalDamage" );
         }
         if ( Settings.CalloutWeaponStability )
            HeauUpDisplay.HookCalloutToggle( ToggleStabilityDamage );

         if ( HasMod( "com.joelmeador.WeaponRealizer", "WeaponRealizer.Core" ) ) TryRun( ModLog, InitWeaponRealizerBridge );
      }

      public override void CombatStarts () {
         if ( string.IsNullOrEmpty( RollCorrection.WeaponHitChanceFormat ) )
            BaseChanceFormat = "{2:0}%";
         else
            BaseChanceFormat = RollCorrection.WeaponHitChanceFormat.Replace( "{0:", "{2:" );
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

      // ============ Weapon Loadout ============

      private const string MetaColour = "<#888888FF>";
      private static Color[] ByType;
      private static TMPro.TextMeshProUGUI loadout;

      private static void DisableLoadoutLineWrap ( List<TMPro.TextMeshProUGUI> weaponNames ) {
         foreach ( TMPro.TextMeshProUGUI weapon in weaponNames )
            weapon.enableWordWrapping = false;
      }

      public static void EnhanceWeaponLoadout ( CombatHUDTargetingComputer __instance, List<TMPro.TextMeshProUGUI> ___weaponNames, UIManager ___uiManager ) { try {
         UIColorRefs colours = ___uiManager.UIColorRefs;
         AbstractActor actor = __instance.ActivelyShownCombatant as AbstractActor;
         List<Weapon> weapons = actor?.Weapons;
         if ( actor == null || weapons == null || colours == null ) return;
         if ( ByType == null ) {
            ByType = LerpWeaponColours( colours );
            DisableLoadoutLineWrap( ___weaponNames );
         }
         float close = 0, medium = 0, far = 0;
         for ( int i = Math.Min( ___weaponNames.Count, weapons.Count ) - 1 ; i >= 0 ; i-- ) {
            Weapon w = weapons[ i ];
            if ( w == null || ! w.CanFire ) continue;
            float damage = w.DamagePerShot * w.ShotsWhenFired;
            if ( Settings.ShowDamageInLoadout )
               ___weaponNames[ i ].text = ___weaponNames[ i ].text.Replace( " +", "+" ) + MetaColour + " (" + damage + ")";
            if ( ByType.Length > 0 && ___weaponNames[ i ].color == colours.qualityA ) {
               if ( (int) w.Category < ByType.Length )
                  ___weaponNames[ i ].color = ByType[ (int) w.Category ];
            }
            if ( w.MaxRange <= 90 )       close += damage;
            else if ( w.MaxRange <= 360 ) medium += damage;
            else                          far += damage;
         }

         if ( Settings.ShowAlphaDamageInLoadout != null && HasDamageLabel( colours.white ) )
            loadout.text = string.Format( Settings.ShowAlphaDamageInLoadout, close + medium + far, close, medium, far, medium + far );

         if ( Settings.ShowMeleeDamageInLoadout && actor is Mech mech ) {
            int start = weapons.Count, dmg = (int) (mech.MeleeWeapon?.DamagePerShot * mech.MeleeWeapon?.ShotsWhenFired);
            string format = Settings.ShowDamageInLoadout ? "{0} {1}({2})" : "{0} {1}{2}";
            if ( start < ___weaponNames.Count && dmg > 0 )
               SetWeapon( ___weaponNames[ start ], colours.white, format, Translate( "Melee" ), MetaColour, dmg );
            dmg = (int) (mech.DFAWeapon?.DamagePerShot * mech.DFAWeapon?.ShotsWhenFired);
            if ( actor.WorkingJumpjets > 0 && start + 1 < ___weaponNames.Count && dmg > 0 )
               SetWeapon( ___weaponNames[ start + 1 ], colours.white, format, Translate( "DFA" ), MetaColour, dmg );
         }
      }                 catch ( Exception ex ) { Error( ex ); } }

      private static bool HasDamageLabel ( Color white ) {
         if ( loadout != null ) return true;
         loadout = GameObject.Find( "tgtWeaponsLabel" )?.GetComponent<TMPro.TextMeshProUGUI>();
         if ( loadout == null ) return false;
         loadout.rectTransform.sizeDelta = new Vector2( 200, loadout.rectTransform.sizeDelta.y );
         loadout.transform.Translate( 10, 0, 0 );
         loadout.alignment = TMPro.TextAlignmentOptions.Left;
         loadout.fontStyle = TMPro.FontStyles.Normal;
         loadout.color = white;
         return true;
      }

      private static Color[] LerpWeaponColours ( UIColorRefs ui ) {
         if ( Settings.SaturationOfLoadout <= 0 ) return new Color[0];
         Color[] colours = new Color[]{ Color.clear, ui.ballisticColor, ui.energyColor, ui.missileColor, ui.smallColor };
         float saturation = (float) Settings.SaturationOfLoadout, lower = saturation * 0.8f;
         for ( int i = colours.Length - 1 ; i > 0 ; i-- ) {
            Color.RGBToHSV( colours[ i ], out float h, out float s, out float v );
            colours[ i ] = Color.HSVToRGB( h, i == 3 ? lower : saturation, 1 );
         }
         return colours;
      }

      private static void SetWeapon ( TMPro.TextMeshProUGUI ui, Color color, string text, params object[] augs ) {
         ui.text = augs.Length <= 0 ? text : new Text( text, augs ).ToString();
         ui.color = color;
         ui.transform.parent.gameObject.SetActive( true );
      }

      // ============ Mouseover Hint ============

      private static string BaseChanceFormat;

      public static void ShowBaseHitChance ( CombatHUDWeaponSlot __instance, ICombatant target ) { try {
         if ( HUD.SelectedActor is Mech mech ) {
            float baseChance = RollModifier.StepHitChance( Combat.ToHit.GetBaseToHitChance( mech ) ) * 100;
            __instance.ToolTipHoverElement.BuffStrings.Add( new Text( "{0} {1} = " + BaseChanceFormat, Translate( Pilot.PILOTSTAT_GUNNERY ), mech.SkillGunnery, baseChance ) );
         }
      }                 catch ( Exception ex ) { Error( ex ); } }

      public static void ShowBaseMeleeChance ( CombatHUDWeaponSlot __instance, ICombatant target ) { try {
         if ( HUD.SelectedActor is Mech mech ) {
            float baseChance = RollModifier.StepHitChance( Combat.ToHit.GetBaseMeleeToHitChance( mech ) ) * 100;
            __instance.ToolTipHoverElement.BuffStrings.Add( new Text( "{0} {1} = " + BaseChanceFormat, Translate( Pilot.PILOTSTAT_PILOTING ), mech.SkillPiloting, baseChance ) );
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

      private static bool ShowingStabilityDamage; // Set only by ToggleStabilityDamage, which only triggers when CalloutWeaponStability is on
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
         if ( ShowingStabilityDamage ) {
            if ( ! __instance.WeaponText.text.Contains( HeatPrefix ) )
               __instance.WeaponText.text += FormatHeat( weapon.HeatGenerated );
            float dmg = weapon.Instability();
            if ( Settings.ShowReducedWeaponDamage && target is AbstractActor actor )
               dmg *= actor.StatCollection.GetValue<float>("ReceivedInstabilityMultiplier") * actor.EntrenchedMultiplier;
            AddToTotalDamage( dmg, __instance );
            text = FormatStabilityDamage( dmg );
         } else {
            if ( __instance.WeaponText.text.Contains( HeatPrefix ) )
               __instance.WeaponText.text = weapon.UIName.ToString();
            if ( ActiveState is SelectionStateFireMulti multi && __instance.TargetIndex < 0 ) return;
            float raw = weapon.DamagePerShotAdjusted(), dmg = raw; // damage displayed by vanilla
            if ( target != null ) {
               AbstractActor owner = weapon.parent;
               Vector2 position = ( owner.HasMovedThisRound ? null : ActiveState?.PreviewPos ) ?? owner.CurrentPosition;
               dmg = weapon.DamagePerShotFromPosition( MeleeAttackType.NotSet, position, target ); // damage with all masks and reductions factored
               //Info( "{0} {1} => {2} {3} {4}, Dir {5}", owner.CurrentPosition, position, target, target.CurrentPosition, target.CurrentRotation, Combat.HitLocation.GetAttackDirection( position, target ) );
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
         __instance.DamageText.text = text;
      }                 catch ( Exception ex ) { Error( ex ); } }

      public static void ShowTotalDamageSlot ( CombatHUDWeaponPanel __instance, int topIndex, List<CombatHUDWeaponSlot> ___WeaponSlots ) { try {
         TotalSlot = null;
         if ( topIndex <= 0 || topIndex >= ___WeaponSlots.Count || __instance.DisplayedActor == null ) return;
         TotalSlot = ___WeaponSlots[ topIndex ];
         TotalSlot.transform.parent.gameObject.SetActive( true );
         TotalSlot.DisplayedWeapon = null;
         TotalSlot.WeaponText.text = GetTotalLabel();
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
            foreach ( CombatHUDWeaponSlot slot in ___WeaponSlots ) {
               Weapon w = slot.DisplayedWeapon;
               if ( w != null && w.IsEnabled && w.CanFire )
                  AddToTotalDamage( ShowingStabilityDamage ? w.Instability() : w.DamagePerShotAdjusted(), slot );
            }
         }
         TotalSlot.WeaponText.text = GetTotalLabel();
         TotalSlot.DamageText.text = FormatTotalDamage( TotalDamage, true );
         TotalSlot.HitChanceText.text = FormatTotalDamage( AverageDamage, true );
      }                 catch ( Exception ex ) { Error( ex ); } }

      public static void RefreshTotalDamage () { try {
         if ( ActiveState is SelectionStateFireMulti )
            HUD?.WeaponPanel?.RefreshDisplayedWeapons(); // Refresh weapon highlight AFTER HUD.SelectedTarget is updated.
      }                 catch ( Exception ex ) { Error( ex ); } }

      public static void ToggleStabilityDamage ( bool IsCallout ) { try {
         ShowingStabilityDamage = IsCallout;
         HUD?.WeaponPanel?.RefreshDisplayedWeapons();
      }                 catch ( Exception ex ) { Error( ex ); } }

      public static bool BypassTotalSlotHighlight ( CombatHUDWeaponSlot __instance ) { try {
         if ( __instance.DisplayedWeapon == null ) return false; // Skip highlight if no weapon in this slot
         return true;
      }                 catch ( Exception ex ) { return Error( ex ); } }

      // ============ Helpers ============

      private const string HeatPrefix = " <#FF0000>", HeatPostfix = "H";
      private const string StabilityPrefix = "<#FFFF00>", StabilityPostfix = "s";

      private static string FormatStabilityDamage ( float dmg, bool alwaysShowNumber = false ) {
         if ( dmg < 1 && ! alwaysShowNumber ) return StabilityPrefix + "--";
         return StabilityPrefix + (int) dmg + StabilityPostfix;
      }

      private static string FormatHeat ( float heat ) {
         if ( heat < 1 ) return HeatPrefix + "--";
         return HeatPrefix + (int) heat + HeatPostfix;
      }

      private static string FormatTotalDamage ( float dmg, bool alwaysShowNumber = false ) {
         if ( ShowingStabilityDamage ) return FormatStabilityDamage( dmg, alwaysShowNumber );
         return ( (int) dmg ).ToString();
      }

      private static string GetTotalLabel () {
         string label = ShowingStabilityDamage ? "Total{0} Stability" : "Total{0}", target = null;
         if ( ActiveState is SelectionStateFireMulti multi ) {
            switch ( multi.GetTargetIndex( HUD.SelectedTarget ) ) {
               case 0 : target = " A"; break;
               case 1 : target = " B"; break;
               case 2 : target = " C"; break;
            }
         }
         return string.Format( label, target );
      }
   }
}