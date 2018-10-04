using BattleTech.UI;
using BattleTech;
using InControl;
using Localize;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Sheepy.BattleTechMod.AttackImprovementMod {
   using static Mod;
   using static System.Reflection.BindingFlags;

   public class Targetting : BattleModModule {

      public override void CombatStartsOnce () {
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

         if ( Settings.ShiftKeyReverseSelection ) {
            SelectNext = typeof( CombatSelectionHandler ).GetMethod( "ProcessSelectNext", NonPublic | Instance );
            SelectPrev = typeof( CombatSelectionHandler ).GetMethod( "ProcessSelectPrevious", NonPublic | Instance );
            if ( BTInput.Instance.FindActionBoundto( new KeyBindingSource( Key.LeftShift ) ) != null ) {
               Warn( "Left Shift is binded. ShiftKeyReverseSelection disabled." );
            } else {
               if ( AnyNull( SelectNext, SelectPrev ) ) {
                  Warn( "CombatSelectionHandler.ProcessSelectNext and/or ProcessSelectPrevious not found. ShiftKeyReverseSelection not fully patched." );
               } else {
                  Patch( typeof( CombatSelectionHandler ), "ProcessSelectNext", "CheckReverseNextSelection", null );
                  Patch( typeof( CombatSelectionHandler ), "ProcessSelectPrevious", "CheckReversePrevSelection", null );
               }
            }
         }

         if ( Settings.FixLosPreviewHeight )
            Patch( typeof( Pathing ), "UpdateFreePath", null, "FixMoveDestinationHeight" );

         if ( Settings.CalloutFriendlyFire ) {
            Patch( typeof( AbstractActor ), "VisibilityToTargetUnit", "MakeFriendsVisible", null );
            Patch( typeof( CombatGameState ), "get_AllEnemies", "AddFriendsToEnemies", null );
            Patch( typeof( CombatGameState ), "GetAllTabTargets", null, "AddFriendsToTargets" );
            Patch( typeof( SelectionStateFire ), "CalcPossibleTargets", null, "AddFriendsToTargets" );
            Patch( typeof( SelectionStateFire ), "ProcessClickedCombatant", null, "SuppressHudSafety" );
            Patch( typeof( SelectionStateFire ), "get_ValidateInfoTargetAsFireTarget", null, "SuppressIFF" );
            Patch( typeof( CombatSelectionHandler ), "TrySelectTarget", null, "SuppressSafety" );
            Patch( typeof( CombatSelectionHandler ), "ProcessInput", "ToggleFriendlyFire", null );
         }
      }

      // ============ Line of Fire ============

      public static void FixMoveDestinationHeight ( Pathing __instance ) {
         __instance.ResultDestination.y = Combat.MapMetaData.GetLerpedHeightAt( __instance.ResultDestination );
      }

      // ============ Reverse Selection ============

      private static bool IsSelectionReversed;
      private static MethodInfo SelectNext, SelectPrev;

      public static bool CheckReverseNextSelection ( CombatSelectionHandler __instance, ref bool __result ) {
         return CheckReverseSelection( __instance, ref __result, SelectPrev );
      }

      public static bool CheckReversePrevSelection ( CombatSelectionHandler __instance, ref bool __result ) {
         return CheckReverseSelection( __instance, ref __result, SelectNext );
      }

      public static bool CheckReverseSelection ( CombatSelectionHandler __instance, ref bool __result, MethodInfo reverse ) { try {
         if ( ! Input.GetKey( KeyCode.LeftShift ) ) return true; // Shift not pressed. Continue.
         IsSelectionReversed = ! IsSelectionReversed;
         if ( ! IsSelectionReversed ) return true; // In reversed selection. Allow to pass.
         __result = (bool) reverse.Invoke( __instance, null ); // Otherwise call reverse selection.
         return false;
      }                 catch ( Exception ex ) { return Error( ex ); } }

      // ============ Multi-Target ============

      private static bool ReAddStateData;

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

      // ============ Friendly Fire ============

      private static bool FriendlyFire = false;

      public static bool MakeFriendsVisible ( AbstractActor __instance, ref VisibilityLevel __result, ICombatant targetUnit ) {
         if ( ! __instance.IsFriendly( targetUnit ) ) return true;
         __result = VisibilityLevel.LOSFull;
         return false;
      }

      public static bool AddFriendsToEnemies ( CombatGameState __instance, ref List<AbstractActor> __result ) {
         if ( ! FriendlyFire ) return true;
         __result = __instance.AllActors.FindAll( e => ! e.IsDead && e != HUD?.SelectedActor );
         return false;
      }

      public static void AddFriendsToTargets ( List<ICombatant> __result, AbstractActor actor ) {
         if ( ! FriendlyFire || ! actor.team.IsLocalPlayer ) return;
         int before = __result.Count;
         foreach ( Team team in Combat.Teams )
            if ( team.IsLocalPlayer || team.IsFriendly( Combat.LocalPlayerTeam ) )
               foreach ( ICombatant unit in team.units )
                  if ( ! unit.IsDead && unit != actor )
                     __result.Add( unit );
      }

      public static void SuppressHudSafety ( SelectionStateFire __instance, ICombatant combatant, ref bool __result ) { try {
         if ( __result || ! FriendlyFire || __instance.Orders != null ) return;
         if ( combatant.team.IsFriendly( Combat.LocalPlayerTeam ) && HUD.SelectionHandler.TrySelectTarget( combatant ) ) {
            __instance.SetTargetedCombatant( combatant );
            __result = true;
         }
      }                 catch ( Exception ex ) { Error( ex ); } }

      public static void SuppressIFF ( SelectionStateFire __instance, ref bool __result ) { try {
         if ( __result || ! FriendlyFire ) return;
         ICombatant target = HUD.SelectionHandler.InfoTarget;
         if ( target == null || ! Combat.LocalPlayerTeam.IsFriendly( target.team ) ) return;
         __result = BattleTech.WeaponFilters.GenericAttack.Filter( __instance.SelectedActor.Weapons, target, false ).Count > 0;
      }                 catch ( Exception ex ) { Error( ex ); } }

      public static void SuppressSafety ( ICombatant target, ref bool __result ) { try {
         if ( __result || ! FriendlyFire ) return;
         if ( target == null || target.team != Combat.LocalPlayerTeam ) return;
         Combat.MessageCenter.PublishMessage( new ActorTargetedMessage( target.GUID ) );
         __result = true;
      }                 catch ( Exception ex ) { Error( ex ); } }

      public static void ToggleFriendlyFire () { try {
         if ( ActiveState == null ) return;
         if ( FriendlyFire != IsCalloutPressed ) {
            FriendlyFire = IsCalloutPressed;
            foreach ( AbstractActor actor in Combat.LocalPlayerTeam.units )
               if ( ! actor.IsDead )
                  actor.VisibilityCache.RebuildCache( Combat.GetAllCombatants() );
            //__instance.ActiveState.ProcessMousePos( CameraControl.Instance.ScreenCenterToGroundPosition );
         }
      }                 catch ( Exception ex ) { Error( ex ); } }
   }
}