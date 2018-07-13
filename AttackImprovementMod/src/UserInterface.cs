using System;
using System.Reflection;

namespace Sheepy.AttackImprovementMod {
   using static Mod;
   using static System.Reflection.BindingFlags;
   using BattleTech.UI;
   using System.Collections.Generic;
   using BattleTech;

   public class UserInterface : ModModule {

      public override void InitPatch () {
         ModSettings Settings = Mod.Settings;
         if ( Settings.FixRearReadout ) {
            if ( structureRearCachedProp == null || timeSinceStructureDamagedProp == null )
               Error( "Cannot find HUDMechArmorReadout.structureRearCached and/or HUDMechArmorReadout.timeSinceStructureDamaged, rear readout structure not fixed." );
            else
               Patch( typeof( HUDMechArmorReadout ), "UpdateMechStructureAndArmor", null, "FixRearStructureDisplay" );
         }
         if ( Settings.FixMultiTargetBackout ) {
            if ( targetedCombatant == null )
               Warn( "Cannot find SelectionState.targetedCombatant. MultiTarget backup may triggers target lock sound effect." );
            if ( ClearTargetedActor == null )
               Warn( "Cannot find SelectionStateFireMulti.ClearTargetedActor. MultiTarget backout may be slightly inconsistent." );
            if ( RemoveTargetedCombatant == null )
               Error( "Cannot find RemoveTargetedCombatant(), SelectionStateFireMulti not patched" );
            else if ( weaponTargetIndices == null )
               Error( "Cannot find weaponTargetIndices, SelectionStateFireMulti not patched" );
            else {
               Patch( typeof( CombatSelectionHandler ), "BackOutOneStep", NonPublic, null, "PreventMultiTargetBackout" );
               Patch( typeof( SelectionStateFireMulti ), "get_CanBackOut", "OverrideMultiTargetCanBackout", null );
               Patch( typeof( SelectionStateFireMulti ), "BackOut", "OverrideMultiTargetBackout", null );
               Patch( typeof( SelectionStateFireMulti ), "RemoveTargetedCombatant", NonPublic, "OverrideRemoveTargetedCombatant", null );
            }
         }
         if ( Settings.ShowHeatAndStab ) {
            Patch( typeof( CombatHUDActorDetailsDisplay ), "RefreshInfo", "ShowHeatAndStab", null );
            Patch( typeof( CombatHUDActorInfo ), "RefreshPredictedHeatInfo", null, "RecordRefresh" );
            Patch( typeof( CombatHUDActorInfo ), "RefreshPredictedStabilityInfo", null, "RecordRefresh" );
            Patch( typeof( CombatHUDMechTray ), "Update", NonPublic, null, "RefreshHeatAndStab" );
         }
         if ( Settings.FixNonJumpLosPreview )
            Patch( typeof( Pathing ), "UpdateFreePath", null, "FixMoveDestinationHeight" );
      }

      // ============ Rear Readout ============

      private static PropertyInfo structureRearCachedProp = typeof( HUDMechArmorReadout ).GetProperty( "structureRearCached", NonPublic | Instance );
      private static PropertyInfo timeSinceStructureDamagedProp = typeof( HUDMechArmorReadout ).GetProperty( "timeSinceStructureDamaged", NonPublic | Instance );

      public static void FixRearStructureDisplay ( HUDMechArmorReadout __instance, AttackDirection shownAttackDirection ) { try {
         HUDMechArmorReadout me = __instance;
         float[] timeSinceStructureDamaged = (float[]) timeSinceStructureDamagedProp.GetValue( me, null );
         UnityEngine.Color[] structureRearCached = (UnityEngine.Color[]) structureRearCachedProp.GetValue( me, null );

         float flashPeriod = 1f;
         UnityEngine.Color flashColour = UnityEngine.Color.white;
         if ( HUD != null ) {
            flashPeriod = HBS.LazySingletonBehavior<UIManager>.Instance.UILookAndColorConstants.FlashArmorTime;
            flashColour = HBS.LazySingletonBehavior<UIManager>.Instance.UILookAndColorConstants.ArmorFlash.color;
         }
         Dictionary<ArmorLocation, int> dictionary = null;
         if ( shownAttackDirection != AttackDirection.None && me.UseForCalledShots )
            dictionary = HUD.Combat.HitLocation.GetMechHitTable( shownAttackDirection, false );

         for ( int i = 1 ; i < 8 ; i++ ) { // Skip head
            if ( i == 3 ) continue; // Skip torso
            float structureFlash = UnityEngine.Mathf.Clamp01( 1f - timeSinceStructureDamaged[i] / flashPeriod );
            ArmorLocation rearLocation = HUDMechArmorReadout.GetArmorLocationFromIndex( i, true, me.flipRearDisplay );
            bool isValid = ! me.UseForCalledShots || ( dictionary != null && dictionary.ContainsKey( rearLocation ) && dictionary[ rearLocation ] != 0 );
            bool isHidden = me.UseForCalledShots && ! isValid;
            UnityEngine.Color structureColor = structureRearCached[ i ]; // The first line that has typo in original code
            if ( isHidden )                                             // And the second line
               structureColor = UnityEngine.Color.Lerp( structureColor, UnityEngine.Color.black, me.hiddenColorLerp );
            UIHelpers.SetImageColor( me.StructureRear[ i ], UnityEngine.Color.Lerp( structureColor, flashColour, structureFlash ) );
         }
      }                 catch ( Exception ex ) { Error( ex ); } }

      // ============ Multi-Target ============

      private static bool ReAddStateData = false;

      public static void PreventMultiTargetBackout ( CombatSelectionHandler __instance ) {
         if ( ReAddStateData ) {
            // Re-add self state onto selection stack to prevent next backout from cancelling command
            __instance.NotifyChange( CombatSelectionHandler.SelectionChange.StateData );
         }
      }

      public static bool OverrideMultiTargetCanBackout ( SelectionStateFireMulti __instance, ref bool __result ) {
         __result = __instance.Orders == null && __instance.AllTargetedCombatantsCount > 0;
         return false;
      }

      private static FieldInfo targetedCombatant = typeof( SelectionState ).GetField( "targetedCombatant", NonPublic | Instance );
      private static PropertyInfo weaponTargetIndices = typeof( SelectionStateFireMulti ).GetProperty( "weaponTargetIndices", NonPublic | Instance );
      private static MethodInfo RemoveTargetedCombatant = typeof( SelectionStateFireMulti ).GetMethod( "RemoveTargetedCombatant", NonPublic | Instance );
      private static MethodInfo ClearTargetedActor = typeof( SelectionStateFireMulti ).GetMethod( "ClearTargetedActor", NonPublic | Instance | FlattenHierarchy );
      private static object[] RemoveTargetParams = new object[]{ null, false };

      public static bool OverrideMultiTargetBackout ( SelectionStateFireMulti __instance ) { try {
         var me = __instance;
         var allTargets = me.AllTargetedCombatants;
         int count = allTargets.Count;
         if ( count > 0 ) {
            // Change target to second newest to reset keyboard focus and thus dim cancelled target's LOS
            ICombatant newTarget = count > 1 ? allTargets[ count - 2 ] : null;
            HUD.SelectionHandler.TrySelectTarget( newTarget );
            // Try one of the reflection ways to set new target
            if ( newTarget == null && ClearTargetedActor != null )
               ClearTargetedActor.Invoke( me, null ); // Hide fire button
            else if ( targetedCombatant != null )
               targetedCombatant.SetValue( me, newTarget ); // Skip soft lock sound
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

      public static bool OverrideRemoveTargetedCombatant ( SelectionStateFireMulti __instance, ICombatant target, bool clearedForFiring ) { try {
         var allTargets = __instance.AllTargetedCombatants;
         int index = target == null ? allTargets.Count - 1 : allTargets.IndexOf( target );
         if ( index < 0 ) return false;

         // Fix weaponTargetIndices
         var indice = (Dictionary<Weapon, int>) weaponTargetIndices.GetValue( __instance, null );
         Weapon[] weapons = new Weapon[ indice.Count ];
         indice.Keys.CopyTo( weapons, 0 );
         foreach ( var weapon in weapons ) {
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

      // ============ Heat and Stability ============

      public static bool ShowHeatAndStab ( CombatHUDActorDetailsDisplay __instance ) { try {
         // Only override mechs. Other actors are unimportant to us.
         Mech mech = __instance.DisplayedActor as Mech;
         if ( __instance.DisplayedActor == null || mech == null )
            return true;

         int jets = mech.WorkingJumpjets;
         string line1 = mech.weightClass.ToString(), line2 = null;
         if ( jets > 0 ) line1 += ", " + jets + " JETS";

         int baseHeat = mech.CurrentHeat, newHeat = baseHeat,
            baseStab = (int) mech.CurrentStability, newStab = baseStab;
         if ( __instance.DisplayedActor.team.IsLocalPlayer ) { // Two lines in selection panel
            line1 = "·\n" + line1;
            CombatSelectionHandler selection = HUD?.SelectionHandler;
            newHeat += mech.TempHeat;
            if ( selection != null && selection.SelectedActor == mech ) {
               newHeat += selection.ProjectedHeatForState;
               if ( ! mech.HasMovedThisRound )
                  newHeat += mech.StatCollection.GetValue<int>( "EndMoveHeat" );
               if ( ! mech.HasAppliedHeatSinks )
                  newHeat = Math.Min( Math.Max( 0, newHeat - mech.AdjustedHeatsinkCapacity ), mech.MaxHeat );
               newStab = (int) selection.ProjectedStabilityForState;
            }
         }

         line2 = "Heat " + baseHeat;
         if ( baseHeat == newHeat ) line2 += "/" + mech.MaxHeat; else line2 += " >> " + newHeat;
         line2 += "\nStab " + baseStab;
         if ( baseStab == newStab ) line2 += "/" + mech.MaxStability; else line2 += " >> " + newStab;

         __instance.ActorWeightText.text = line1 + "\n" + line2;
         __instance.JumpJetsHolder.SetActive( false );
         return false;
      }                 catch ( Exception ex ) { return Error( ex ); } }

      private static bool needRefresh = false;
      public static void RecordRefresh () {
         needRefresh = true;
      }

      public static void RefreshHeatAndStab ( CombatHUDMechTray __instance ) {
         if ( !needRefresh ) return;
         __instance?.ActorInfo?.DetailsDisplay?.RefreshInfo();
         needRefresh = false;
      }

      // ============ Pathing ============

      public static void FixMoveDestinationHeight ( Pathing __instance ) {
         __instance.ResultDestination.y = Combat.MapMetaData.GetLerpedHeightAt( __instance.ResultDestination );
      }
   }
}