using BattleTech.UI;
using BattleTech;
using Localize;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Sheepy.BattleTechMod.AttackImprovementMod {
   using System.Diagnostics;
   using static Mod;
   using static System.Reflection.BindingFlags;

   public class UserInterface : BattleModModule {

      private static Color?[] NameplateColours = new Color?[ 3 ];
      private static Color?[] FloatingArmorColours = new Color?[ 3 ];

      public override void CombatStartsOnce () {
         NameplateColours = ParseColours( Settings.NameplateColourPlayer, Settings.NameplateColourEnemy, Settings.NameplateColourAlly );
         if ( Settings.ShowEnemyWounds != null || Settings.ShowNPCHealth != null ) {
            Patch( typeof( CombatHUDActorNameDisplay ), "RefreshInfo", typeof( VisibilityLevel ), null, "ShowNPCWounds" );
            Patch( typeof( Pilot ), "InjurePilot", null, "RefreshPilotNames" );
         }
         if ( NameplateColours != null )
            Patch( typeof( CombatHUDNumFlagHex ), "OnActorChanged", null, "SetNameplateColor" );
         FloatingArmorColours = ParseColours( Settings.FloatingArmorColourPlayer, Settings.FloatingArmorColourEnemy, Settings.FloatingArmorColourAlly );
         if ( FloatingArmorColours != null ) {
            BarOwners = new Dictionary<CombatHUDPipBar, ICombatant>();
            Patch( typeof( CombatHUDPipBar ), "ShowValue", new Type[]{ typeof( float ), typeof( Color ), typeof( Color ), typeof( Color ), typeof( bool ) }, "CombatHUDLifeBarPips", null );
            Patch( typeof( CombatHUDNumFlagHex ), "OnActorChanged", "SetPipBarOwner", null );
         }

         if ( Settings.FixMultiTargetBackout ) {
            TryRun( Log, () => {
               weaponTargetIndices = typeof( SelectionStateFireMulti ).GetProperty( "weaponTargetIndices", NonPublic | Instance );
               RemoveTargetedCombatant = typeof( SelectionStateFireMulti ).GetMethod( "RemoveTargetedCombatant", NonPublic | Instance );
               ClearTargetedActor = typeof( SelectionStateFireMulti ).GetMethod( "ClearTargetedActor", NonPublic | Instance | FlattenHierarchy );
            } );

            if ( ClearTargetedActor == null )
               Warn( "Cannot find SelectionStateFireMulti.ClearTargetedActor. MultiTarget backout may be slightly inconsistent." );
            if ( RemoveTargetedCombatant == null )
               Error( "Cannot find RemoveTargetedCombatant(), SelectionStateFireMulti not patched" );
            else if ( weaponTargetIndices == null )
               Error( "Cannot find weaponTargetIndices, SelectionStateFireMulti not patched" );
            else {
               Type MultiTargetType = typeof( SelectionStateFireMulti );
               Patch( typeof( CombatSelectionHandler ), "BackOutOneStep", null, "PreventMultiTargetBackout" );
               Patch( MultiTargetType, "get_CanBackOut", "OverrideMultiTargetCanBackout", null );
               Patch( MultiTargetType, "BackOut", "OverrideMultiTargetBackout", null );
               Patch( MultiTargetType, "RemoveTargetedCombatant", "OverrideRemoveTargetedCombatant", null );
            }
         }

         if ( Settings.FixLosPreviewHeight )
            Patch( typeof( Pathing ), "UpdateFreePath", null, "FixMoveDestinationHeight" );

         Type slotType = typeof( CombatHUDWeaponSlot );
         if ( Settings.ShowBaseHitchance ) {
            Patch( slotType, "UpdateToolTipsFiring", typeof( ICombatant ), "ShowBaseHitChance", null );
            Patch( slotType, "UpdateToolTipsMelee", typeof( ICombatant ), "ShowBaseMeleeChance", null );
         }
         if ( Settings.ShowShortRangeInBreakdown ) {
            rangedPenalty = ModifierList.GetRangedModifierFactor( "range" );
            if ( rangedPenalty == null )
               Warn( "Cannot get range penalty. ShowShortRangeInBreakdown disabled." );
            else
               Patch( slotType, "UpdateToolTipsFiring", typeof( ICombatant ), "ShowOptimalRange", null );
         }
         if ( Settings.ShowWeaponProp || Settings.WeaponRangeFormat != null )
            Patch( slotType, "GenerateToolTipStrings", null, "UpdateWeaponTooltip" );

         if ( Settings.FunctionKeySelectPC )
            Combat.MessageCenter.AddSubscriber( MessageCenterMessageType.KeyPressedMessage, KeyPressed );
      }

      public override void CombatEnds () {
         BarOwners?.Clear();
         if ( Settings.FunctionKeySelectPC )
            Combat.MessageCenter.RemoveSubscriber( MessageCenterMessageType.KeyPressedMessage, KeyPressed );
      }

      // ============ Keyboard Input ============

      public static void KeyPressed ( MessageCenterMessage message ) { try {
         if ( Combat == null ) return;
         string key = ( message as KeyPressedMessage )?.KeyCode;
         switch ( key ) {
            case "F1": SelectPC( 0, InControl.Key.F1 ); break;
            case "F2": SelectPC( 1, InControl.Key.F2 ); break;
            case "F3": SelectPC( 2, InControl.Key.F3 ); break;
            case "F4": SelectPC( 3, InControl.Key.F4 ); break;
         }
      }                 catch ( Exception ex ) { Error( ex ); } }

      private static void SelectPC ( int index, InControl.Key key ) {
         if ( BTInput.Instance?.FindActionBoundto( new InControl.KeyBindingSource( key ) ) != null ) return;
         List<AbstractActor> units = Combat?.LocalPlayerTeam?.units;
         if ( units == null || index >= units.Count || units[ index ] == null ) return;
         HUD.MechWarriorTray.FindPortraitForActor( units[ index ].GUID ).OnClicked();
      }

      // ============ Multi-Target ============

      private static bool ReAddStateData = false;

      public static void PreventMultiTargetBackout ( CombatSelectionHandler __instance ) {
         if ( ReAddStateData ) {
            // Re-add self state onto selection stack to prevent next backout from cancelling command
            __instance.NotifyChange( CombatSelectionHandler.SelectionChange.StateData );
         }
      }

      [ Harmony.HarmonyPriority( Harmony.Priority.Low ) ]
      public static bool OverrideMultiTargetCanBackout ( SelectionStateFireMulti __instance, ref bool __result ) {
         __result = __instance.Orders == null && __instance.AllTargetedCombatantsCount > 0;
         return false;
      }

      private static PropertyInfo weaponTargetIndices;
      private static MethodInfo RemoveTargetedCombatant, ClearTargetedActor;
      private static readonly object[] RemoveTargetParams = new object[]{ null, false };

      [ Harmony.HarmonyPriority( Harmony.Priority.Low ) ]
      public static bool OverrideMultiTargetBackout ( SelectionStateFireMulti __instance, ref ICombatant ___targetedCombatant ) { try {
         SelectionStateFireMulti me = __instance;
         List<ICombatant> allTargets = me.AllTargetedCombatants;
         int count = allTargets.Count;
         if ( count > 0 ) {
            // Change target to second newest to reset keyboard focus and thus dim cancelled target's LOS
            ICombatant newTarget = count > 1 ? allTargets[ count - 2 ] : null;
            HUD.SelectionHandler.TrySelectTarget( newTarget );
            // Try one of the reflection ways to set new target
            if ( newTarget == null && ClearTargetedActor != null )
               ClearTargetedActor.Invoke( me, null ); // Hide fire button
            else if ( ___targetedCombatant != null )
               ___targetedCombatant = newTarget; // Skip soft lock sound
            else
               me.SetTargetedCombatant( newTarget );
            // The only line that is same as old!
            RemoveTargetedCombatant.Invoke( me, RemoveTargetParams );
            // Amend selection state later
            ReAddStateData = true;
            return false;
         }
         return true;
      }                 catch ( Exception ex ) { return Error( ex ); } }

      [ Harmony.HarmonyPriority( Harmony.Priority.Low ) ]
      public static bool OverrideRemoveTargetedCombatant ( SelectionStateFireMulti __instance, ICombatant target, bool clearedForFiring ) { try {
         List<ICombatant> allTargets = __instance.AllTargetedCombatants;
         int index = target == null ? allTargets.Count - 1 : allTargets.IndexOf( target );
         if ( index < 0 ) return false;

         // Fix weaponTargetIndices
         Dictionary<Weapon, int> indice = (Dictionary<Weapon, int>) weaponTargetIndices.GetValue( __instance, null );
         foreach ( Weapon weapon in indice.Keys.ToArray() ) {
            if ( indice[ weapon ] > index )
               indice[ weapon ] -= - 1;
            else if ( indice[ weapon ] == index )
               indice[ weapon ] = -1;
         }
         // End Fix

         allTargets.RemoveAt( index );
         Combat.MessageCenter.PublishMessage( new ActorMultiTargetClearedMessage( index.ToString(), clearedForFiring ) );
         return false;
      }                 catch ( Exception ex ) { return Error( ex ); } }

      // ============ Floating Nameplate ============

      public static void ShowNPCWounds ( CombatHUDActorNameDisplay __instance, VisibilityLevel visLevel ) { try {
         AbstractActor actor = __instance.DisplayedCombatant as AbstractActor;
         Pilot pilot = actor?.GetPilot();
         Team team = actor?.team;
         if ( pilot == null || ( team != null && team.IsLocalPlayer ) ) return;
         string format = null;
         object[] args = null;
         if ( team.IsFriendly( BattleTechGame?.Combat?.LocalPlayerTeam ) ) {
            format = Settings.ShowNPCHealth;
            args = new object[]{ __instance.PilotNameText.text, pilot.Injuries, pilot.Health - pilot.Injuries, pilot.Health };
         } else if ( visLevel == VisibilityLevel.LOSFull ) {
            format = Settings.ShowEnemyWounds;
            args = new object[]{ __instance.PilotNameText.text, pilot.Injuries, "?", "?" };
         }
         if ( format == null || ( ! format.StartsWith( "{0}" ) && pilot.Injuries <= 0 ) ) return;
         __instance.PilotNameText.SetText( Translate( format, args ) );
      }                 catch ( Exception ex ) { Error( ex ); } }

      public static void RefreshPilotNames ( Pilot __instance ) { try {
         if ( __instance.IsIncapacitated ) return;
         AbstractActor actor = Combat.AllActors.First( e => e.GetPilot() == __instance );
         if ( actor == null ) return;
         HUD.InWorldMgr.GetNumFlagForCombatant( actor )?.ActorInfo?.NameDisplay?.RefreshInfo();
         //if ( HUD.SelectedActor == actor )
         //   HUD.MechTray.ActorInfo.NameDisplay.RefreshInfo();
         if ( HUD.SelectedTarget == actor )
            HUD.TargetingComputer.ActorInfo.NameDisplay.RefreshInfo();
      }                 catch ( Exception ex ) { Error( ex ); } }

      // Colours are Player, Enemy, and Ally
      private static Color? GetTeamColour ( ICombatant owner, Color?[] Colours ) {
         Team team = owner?.team;
         if ( team == null || owner.IsDead ) return null;

         if ( team.IsLocalPlayer ) return Colours[0];
         if ( team.IsEnemy( BattleTechGame?.Combat?.LocalPlayerTeam ) ) return Colours[1];
         if ( team.IsFriendly( BattleTechGame?.Combat?.LocalPlayerTeam ) ) return Colours[2];
         return null;
      }

      public static void SetNameplateColor ( CombatHUDNumFlagHex __instance ) { try {
         Color? colour = GetTeamColour( __instance.DisplayedCombatant, NameplateColours );
         if ( colour == null ) return;
         CombatHUDActorNameDisplay names = __instance.ActorInfo?.NameDisplay;
         if ( names == null ) return;
         names.PilotNameText.faceColor = colour.GetValueOrDefault();
         if ( colour != Color.black ) {
            names.MechNameText.outlineWidth = 0.2f;
            names.MechNameText.outlineColor = Color.black;
         }
         names.MechNameText.faceColor = colour.GetValueOrDefault();
      }                 catch ( Exception ex ) { Error( ex ); } }

      private static Dictionary<CombatHUDPipBar, ICombatant> BarOwners;

      public static void SetPipBarOwner ( CombatHUDNumFlagHex __instance ) {
         ICombatant owner = __instance.DisplayedCombatant;
         CombatHUDLifeBarPips bar = __instance.ActorInfo?.ArmorBar;
         if ( bar == null ) return;
         if ( owner != null )
            BarOwners[ bar ] = owner;
         else if ( BarOwners.ContainsKey( bar ) )
            BarOwners.Remove( bar );
      }

      public static void CombatHUDLifeBarPips ( CombatHUDPipBar __instance, ref Color shownColor ) { try {
         if ( ! ( __instance is CombatHUDLifeBarPips me ) || ! BarOwners.TryGetValue( __instance, out ICombatant owner ) ) return;
         Color? colour = GetTeamColour( owner, FloatingArmorColours );
         if ( colour != null )
            shownColor = colour.GetValueOrDefault();
      }                 catch ( Exception ex ) { Error( ex ); } }

      // ============ Others ============

      public static void FixMoveDestinationHeight ( Pathing __instance ) {
         __instance.ResultDestination.y = Combat.MapMetaData.GetLerpedHeightAt( __instance.ResultDestination );
      }

      // ============ Weapon Hint ============

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

      public static void ShowOptimalRange ( CombatHUDWeaponSlot __instance, ICombatant target ) { try {
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
   }
}