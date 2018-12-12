using BattleTech.UI;
using BattleTech;
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
         Type MultiTargetType = typeof( SelectionStateFireMulti ), HandlerType = typeof( CombatSelectionHandler ), PanelType = typeof( CombatHUDWeaponPanel );

         if ( Settings.AggressiveMultiTargetAssignment ) {
            SlotSetTargetIndexMethod = typeof( CombatHUDWeaponSlot ).GetMethod( "SetTargetIndex", NonPublic | Instance );
            if ( SlotSetTargetIndexMethod != null ) {
               Patch( PanelType, "OnActorMultiTargeted", "OverrideMultiTargetAssignment", null );
               Patch( PanelType, "OnActorMultiTargetCleared", "OverrideMultiTargetAssignment", null );
            } else
               Warn( "CombatHUDWeaponSlot.SetTargetIndex not found. AggressiveMultiTargetAssignment not patched." );
         }

         if ( Settings.FixMultiTargetBackout ) {
            TryRun( Log, () => {
               weaponTargetIndices = MultiTargetType.GetProperty( "weaponTargetIndices", NonPublic | Instance );
               RemoveTargetedCombatant = MultiTargetType.GetMethod( "RemoveTargetedCombatant", NonPublic | Instance );
               ClearTargetedActor = MultiTargetType.GetMethod( "ClearTargetedActor", NonPublic | Instance | FlattenHierarchy );
            } );

            if ( ClearTargetedActor == null )
               Warn( "Cannot find SelectionStateFireMulti.ClearTargetedActor. MultiTarget backout may be slightly inconsistent." );
            if ( AnyNull<object>( RemoveTargetedCombatant, weaponTargetIndices ) )
               Error( "Cannot find RemoveTargetedCombatant or weaponTargetIndices, SelectionStateFireMulti not patched" );
            else {
               Patch( HandlerType, "BackOutOneStep", null, "PreventMultiTargetBackout" );
               Patch( MultiTargetType, "get_CanBackOut", "OverrideMultiTargetCanBackout", null );
               Patch( MultiTargetType, "BackOut", "OverrideMultiTargetBackout", null );
               Patch( MultiTargetType, "RemoveTargetedCombatant", "OverrideRemoveTargetedCombatant", null );
            }
         }

         if ( Settings.CtrlClickDisableWeapon )
            MultiTargetDisabledWeaponTarget = new Dictionary<Weapon, ICombatant>();
         if ( Settings.ShiftKeyReverseSelection ) {
            SelectNextMethod = HandlerType.GetMethod( "ProcessSelectNext", NonPublic | Instance );
            SelectPrevMethod = HandlerType.GetMethod( "ProcessSelectPrevious", NonPublic | Instance );
            if ( AnyNull( SelectNextMethod, SelectPrevMethod ) ) {
               Warn( "CombatSelectionHandler.ProcessSelectNext and/or ProcessSelectPrevious not found. ShiftKeyReverseSelection not fully patched." );
            } else {
               Patch( HandlerType, "ProcessSelectNext", "CheckReverseNextSelection", null );
               Patch( HandlerType, "ProcessSelectPrevious", "CheckReversePrevSelection", null );
            }
         }
         if ( Settings.CtrlClickDisableWeapon || Settings.ShiftKeyReverseSelection ) {
            WeaponTargetIndicesProp = MultiTargetType.GetProperty( "weaponTargetIndices", NonPublic | Instance );
            if ( WeaponTargetIndicesProp == null )
               Warn( "SelectionStateFireMulti.weaponTargetIndices not found.  Multi-Target weapon shift/ctrl click not patched." );
            else
               Patch( MultiTargetType, "CycleWeapon", "OverrideMultiTargetCycle", null );
         }

         if ( Settings.FixLosPreviewHeight )
            Patch( typeof( Pathing ), "UpdateFreePath", null, "FixMoveDestinationHeight" );

         /*
         if ( Settings.CalloutFriendlyFire ) {
            Patch( HandlerType, "TrySelectTarget", null, "SuppressSafety" );
            CombatUI.HookCalloutToggle( ToggleFriendlyFire );
            Patch( typeof( AbstractActor ), "VisibilityToTargetUnit", "MakeFriendsVisible", null );
            Patch( typeof( CombatGameState ), "get_AllEnemies", "AddFriendsToEnemies", null );
            Patch( typeof( CombatGameState ), "GetAllTabTargets", null, "AddFriendsToTargets" );
            Patch( typeof( SelectionStateFire ), "CalcPossibleTargets", null, "AddFriendsToTargets" );
            Patch( typeof( SelectionStateFire ), "ProcessClickedCombatant", null, "SuppressHudSafety" );
            Patch( typeof( SelectionStateFire ), "get_ValidateInfoTargetAsFireTarget", null, "SuppressIFF" );
         }
         */
      }

      public override void CombatEnds () {
         MultiTargetDisabledWeaponTarget?.Clear();
      }

      // ============ Line of Fire ============

      public static void FixMoveDestinationHeight ( Pathing __instance ) {
         __instance.ResultDestination.y = Combat.MapMetaData.GetLerpedHeightAt( __instance.ResultDestination );
      }

      // ============ Reverse Unit Selection ============

      private static bool IsSelectionReversed;
      private static MethodInfo SelectNextMethod, SelectPrevMethod;

      private static bool IsReverseKeyPressed () {
         return Input.GetKey( KeyCode.LeftShift ) || Input.GetKey( KeyCode.RightShift );
      }

      private static bool IsToggleKeyPressed () {
         return Input.GetKey( KeyCode.LeftControl ) || Input.GetKey( KeyCode.RightControl );
      }

      public static bool CheckReverseNextSelection ( CombatSelectionHandler __instance, ref bool __result ) {
         return CheckReverseSelection( __instance, ref __result, SelectPrevMethod );
      }

      public static bool CheckReversePrevSelection ( CombatSelectionHandler __instance, ref bool __result ) {
         return CheckReverseSelection( __instance, ref __result, SelectNextMethod );
      }

      public static bool CheckReverseSelection ( CombatSelectionHandler __instance, ref bool __result, MethodInfo reverse ) { try {
         if ( ! IsReverseKeyPressed() ) return true; // Shift not pressed. Abort.
         IsSelectionReversed = ! IsSelectionReversed;
         if ( ! IsSelectionReversed ) return true; // In reversed selection. Allow to pass.
         __result = (bool) reverse.Invoke( __instance, null ); // Otherwise call reverse selection.
         return false;
      }                 catch ( Exception ex ) { return Error( ex ); } }

      // ============ Reverse / Toggle Multi-Target Selection ============

      private static PropertyInfo WeaponTargetIndicesProp;
      private static Dictionary<Weapon,ICombatant> MultiTargetDisabledWeaponTarget;

      public static bool OverrideMultiTargetCycle ( SelectionStateFireMulti __instance, ref int __result, Weapon weapon ) { try {
         int newIndex = -2;
         if ( Settings.CtrlClickDisableWeapon && IsToggleKeyPressed() )
            newIndex = FindToogleTargetIndex( __instance, weapon );
         else if ( Settings.ShiftKeyReverseSelection &&  IsReverseKeyPressed() )
            newIndex = FindReverseTargetIndex( __instance, weapon );
         if ( newIndex > -2 ) {
            ( (Dictionary<Weapon, int>) WeaponTargetIndicesProp.GetValue( __instance, null ) )[ weapon ] = newIndex;
            __result = newIndex;
            return false;
         }
         return true;
      }                 catch ( Exception ex ) { return Error( ex ); } }

      private static int FindToogleTargetIndex ( SelectionStateFireMulti state, Weapon weapon ) {
         ICombatant current = state.GetSelectedTarget( weapon );
         if ( current != null ) {
            MultiTargetDisabledWeaponTarget[ weapon ] = current;
            return -1;
         }
         List<ICombatant> targets = state.AllTargetedCombatants;
         MultiTargetDisabledWeaponTarget.TryGetValue( weapon, out ICombatant lastTarget );
         int newIndex = lastTarget == null ? -1 : targets.IndexOf( lastTarget );
         if ( newIndex < 0 ) {
            if ( Settings.AggressiveMultiTargetAssignment )
               newIndex = FindBestTargetForWeapon( weapon, targets );
            if ( newIndex < 0 ) newIndex = 0;
         }
         while ( newIndex < targets.Count && ! weapon.WillFireAtTarget( targets[ newIndex ] ) ) // Find a target we can fire at.
            ++newIndex;
         return newIndex >= targets.Count ? -1 : newIndex;
      }

      private static int FindReverseTargetIndex ( SelectionStateFireMulti state, Weapon weapon ) {
         ICombatant current = state.GetSelectedTarget( weapon );
         List<ICombatant> targets = state.AllTargetedCombatants;
         int index = targets.IndexOf( current ), newIndex = index < 0 ? targets.Count - 1 : index - 1;
         while ( newIndex >= 0 && ! weapon.WillFireAtTarget( targets[ newIndex ] ) ) // Find a target we can fire at.
            --newIndex;
         return newIndex;
      }

      // ============ Multi-Target target selection ============

      private static MethodInfo SlotSetTargetIndexMethod;

      public static bool OverrideMultiTargetAssignment ( CombatHUDWeaponPanel __instance, List<CombatHUDWeaponSlot> ___WeaponSlots ) { try {
         SelectionStateFireMulti multi = ActiveState as SelectionStateFireMulti;
         List<ICombatant> targets = multi?.AllTargetedCombatants;
         if ( targets.IsNullOrEmpty() ) return true;
         foreach ( CombatHUDWeaponSlot slot in ___WeaponSlots ) {
            Weapon w = slot?.DisplayedWeapon;
            int target = FindBestTargetForWeapon( w, targets );
            if ( target >= 0 )
               SlotSetTargetIndexMethod.Invoke( slot, new object[]{ multi.AssignWeaponToTarget( w, targets[ target ] ), false } );
         }
         __instance.RefreshDisplayedWeapons();
         return false;
      }                 catch ( Exception ex ) { return Error( ex ); } }

      private static int FindBestTargetForWeapon ( Weapon w, List<ICombatant> targets )  {
         if ( w == null || ! w.IsEnabled || w.Category == WeaponCategory.Melee || targets.IsNullOrEmpty() ) return -1;
         int result = -1;
         float hitChance = 0;
         for ( int i = 0 ; i < targets.Count ; i++ ) {
            ICombatant target = targets[ i ];
            if ( ! w.WillFireAtTarget( target ) ) continue;
            float newChance = Combat.ToHit.GetToHitChance( w.parent, w, target, w.parent.CurrentPosition, target.CurrentPosition, 1, MeleeAttackType.NotSet, false );
            if ( newChance <= hitChance ) continue;
            result = i;
            hitChance = newChance;
         }
         return result;
      }

      // ============ Multi-Target Backout ============

      private static bool ReAddStateData;

      public static void PreventMultiTargetBackout ( CombatSelectionHandler __instance ) { try {
         if ( ReAddStateData ) {
            // Re-add self state onto selection stack to prevent next backout from cancelling command
            __instance.NotifyChange( CombatSelectionHandler.SelectionChange.StateData );
         }
      }                 catch ( Exception ex ) { Error( ex ); } }

      [ Harmony.HarmonyPriority( Harmony.Priority.Low ) ]
      public static bool OverrideMultiTargetCanBackout ( SelectionStateFireMulti __instance, ref bool __result ) { try {
         __result = __instance.Orders == null && __instance.AllTargetedCombatantsCount > 0;
         return false;
      }                 catch ( Exception ex ) { return Error( ex ); } }

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
               indice[ weapon ] -= -1;
            else if ( indice[ weapon ] == index )
               indice[ weapon ] = -1;
         }
         // End Fix

         allTargets.RemoveAt( index );
         Combat.MessageCenter.PublishMessage( new ActorMultiTargetClearedMessage( index.ToString(), clearedForFiring ) );
         return false;
      }                 catch ( Exception ex ) { return Error( ex ); } }

      // ============ Friendly Fire ============

         /*
      private static bool FriendlyFire; // Set only by ToggleFriendlyFire, which only triggers when CalloutFriendlyFire is on

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

      public static void ToggleFriendlyFire ( bool IsCallout ) { try {
         if ( ActiveState == null ) return;
         FriendlyFire = IsCallout;
         foreach ( AbstractActor actor in Combat.LocalPlayerTeam.units )
            if ( ! actor.IsDead )
               actor.VisibilityCache.RebuildCache( Combat.GetAllCombatants() );
         //__instance.ActiveState.ProcessMousePos( CameraControl.Instance.ScreenCenterToGroundPosition );
      }                 catch ( Exception ex ) { Error( ex ); } }
      */
   }
}