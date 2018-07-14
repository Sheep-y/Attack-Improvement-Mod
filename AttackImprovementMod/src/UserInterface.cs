using System;
using System.Reflection;

namespace Sheepy.AttackImprovementMod {
   using static Mod;
   using static BattleTech.ChassisLocations;
   using static BattleTech.UI.HUDMechArmorReadout;
   using static System.Reflection.BindingFlags;
   using BattleTech;
   using BattleTech.UI;
   using System.Collections.Generic;
   using UnityEngine;

   public class UserInterface : ModModule {

      public override void InitPatch () {
         ModSettings Settings = Mod.Settings;
         if ( Settings.FixPaperDollRearStructure ) {
            if ( structureRearProp == null || timeSinceStructureDamagedProp == null )
               Error( "Cannot find HUDMechArmorReadout.structureRearCached and/or HUDMechArmorReadout.timeSinceStructureDamaged, paper doll rear structures not fixed." );
            else
               Patch( typeof( HUDMechArmorReadout ), "UpdateMechStructureAndArmor", null, "FixRearStructureDisplay" );
         }
         if ( Settings.PaperDollDivulgeUnderskinDamage ) {
            if ( outlineProp == null || armorProp == null || structureProp == null || outlineRearProp == null || armorRearProp == null || structureRearProp == null )
               Error( "Cannot find outline, armour, and/or structure colour cache of HUDMechArmorReadout.  Cannot make paper dolls divulge under skin damage." );
            else {
               if ( ! Settings.FixPaperDollRearStructure )
                  Warn( "PaperDollDivulgeUnderskinDamage does not imply FixPaperDollRearStructure. Readout may still be bugged." );
               Patch( typeof( HUDMechArmorReadout ), "RefreshMechStructureAndArmor", null, "ShowStructureDamageThroughArmour" );
            }
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
               Type MultiTargetType = typeof( SelectionStateFireMulti );
               Patch( typeof( CombatSelectionHandler ), "BackOutOneStep", NonPublic, null, "PreventMultiTargetBackout" );
               Patch( MultiTargetType, "get_CanBackOut", "OverrideMultiTargetCanBackout", null );
               Patch( MultiTargetType, "BackOut", "OverrideMultiTargetBackout", null );
               Patch( MultiTargetType, "RemoveTargetedCombatant", NonPublic, "OverrideRemoveTargetedCombatant", null );
            }
         }
         if ( Settings.ShowHeatAndStab ) {
            Patch( typeof( CombatHUDActorDetailsDisplay ), "RefreshInfo", null, "ShowHeatAndStab" );
            Patch( typeof( CombatHUDActorInfo ), "RefreshPredictedHeatInfo", null, "RecordRefresh" );
            Patch( typeof( CombatHUDActorInfo ), "RefreshPredictedStabilityInfo", null, "RecordRefresh" );
            Patch( typeof( CombatHUDMechTray ), "Update", NonPublic, null, "RefreshHeatAndStab" );
         }
         if ( Settings.ShowUnitTonnage )
            Patch( typeof( CombatHUDActorDetailsDisplay ), "RefreshInfo", null, "ShowUnitTonnage" );
         if ( Settings.FixNonJumpLosPreview )
            Patch( typeof( Pathing ), "UpdateFreePath", null, "FixMoveDestinationHeight" );
      }

      private static UILookAndColorConstants LookAndColor;

      public override void CombatStarts () {
         if ( HUD != null )
            LookAndColor = null;
         else
            LookAndColor = HBS.LazySingletonBehavior<UIManager>.Instance.UILookAndColorConstants;
      }

      // ============ Paper Doll ============

      private static readonly ChassisLocations[] Normal  = new ChassisLocations[]{ Head, LeftArm , LeftTorso , CenterTorso, RightTorso, RightArm, LeftLeg , RightLeg };
      private static readonly ChassisLocations[] Flipped = new ChassisLocations[]{ Head, RightArm, RightTorso, CenterTorso, LeftTorso , LeftArm , RightLeg, LeftLeg  };

      private static bool IsStructureDamaged ( ref float percent, Mech mech, ChassisLocations location ) {
         if ( float.IsNaN( percent ) ) {
            float hp = mech.GetCurrentStructure( location );
            float max = mech.MechDef.GetChassisLocationDef( location ).InternalStructure;
            percent = hp / max;
         }
         return percent < 1f;
      }

      public static void ShowStructureDamageThroughArmour ( HUDMechArmorReadout __instance ) { try {
         HUDMechArmorReadout me = __instance;
         Mech mech = me.DisplayedMech;
         if ( mech == null ) return;
         ChassisLocations[] location = me.flipFrontDisplay ? Flipped : Normal;
         int[] back = me.flipFrontDisplay != me.flipRearDisplay ? new int[] { 0, 0, 4, 3, 2 } : new int[] { 0, 0, 2, 3, 4 };
         Color clear = Color.clear;
         Color[] armor = (Color[]) armorProp.GetValue( me, null );
         Color[] armorRear = (Color[]) armorRearProp.GetValue( me, null );
         Color[] outline = null, outlineRear = null, structure = null, structureRear = null;

         for ( int i = 0 ; i < 8 ; i++ ) {
            float percent = float.NaN;
            // Front
            if ( armor[i] != clear ) { // Skip check on armour-less locations
               if ( IsStructureDamaged( ref percent, mech, location[ i ] ) ) {
                  if ( structure == null ) { // Lazy reflection access
                     structure = (Color[]) structureProp.GetValue( me, null );
                     outline = (Color[]) outlineProp.GetValue( me, null );
                  }
                  outline[ i ] = structure[ i ] = armor[ i ];
                  if ( me.UseForCalledShots )
                     armor[ i ] = clear;
                  else
                     armor[ i ].a *= 0.5f; // Front has structure over armor, but structure has more blank to fill.
               }
            }
            // Back
            if ( i < 2 || i > 4 ) continue;
            int j = back[ i ];
            if ( armorRear[ j ] != clear ) {
               if ( IsStructureDamaged( ref percent, mech, location[ i ] ) ) { // i is not typo. We want to check same chassis location as front.
                  if ( structureRear == null ) {
                     structureRear = (Color[]) structureRearProp.GetValue( me, null );
                     outlineRear = (Color[]) outlineRearProp.GetValue( me, null );
                  }
                  outlineRear[ j ] = structureRear[ j ] = armorRear[ j ];
                  armorRear[ j ] = clear;
                  /*
                  outlineRear[ j ] = armorRear[ j ];
                  structureRear[ j ] = UIHelpers.getLerpedColorFromArray(  // Get proper structure colour
                     LazySingletonBehavior<UIManager>.Instance.UILookAndColorConstants.StructureColors, 1f - percent, 
                     LazySingletonBehavior<UIManager>.Instance.UILookAndColorConstants.StructureUseStairSteps,
                     LazySingletonBehavior<UIManager>.Instance.UILookAndColorConstants.StructureNumStairSteps );
                  armorRear[ j ].a *= 0.4f; // But can't get a consistent result on both front and rear and called shot
                  */
               }
            }
         }
         /*
         if ( structure == null ) structure = (Color[]) structureProp.GetValue( me, null );
         if ( structureRear == null ) structureRear = (Color[]) structureRearProp.GetValue( me, null );
         Log( Join( ", ", armor, ColorUtility.ToHtmlStringRGBA ) );
         Log( Join( ", ", structure, ColorUtility.ToHtmlStringRGBA ) );
         Log( Join( ", ", armorRear, ColorUtility.ToHtmlStringRGBA ) );
         Log( Join( ", ", structureRear, ColorUtility.ToHtmlStringRGBA ) );
         */
      }                 catch ( Exception ex ) { Error( ex ); } }

      private static PropertyInfo outlineProp = typeof( HUDMechArmorReadout ).GetProperty( "armorOutlineCached", NonPublic | Instance );
      private static PropertyInfo armorProp = typeof( HUDMechArmorReadout ).GetProperty( "armorCached", NonPublic | Instance );
      private static PropertyInfo structureProp = typeof( HUDMechArmorReadout ).GetProperty( "structureCached", NonPublic | Instance );
      private static PropertyInfo outlineRearProp = typeof( HUDMechArmorReadout ).GetProperty( "armorOutlineRearCached", NonPublic | Instance );
      private static PropertyInfo armorRearProp = typeof( HUDMechArmorReadout ).GetProperty( "armorRearCached", NonPublic | Instance );
      private static PropertyInfo structureRearProp = typeof( HUDMechArmorReadout ).GetProperty( "structureRearCached", NonPublic | Instance );
      private static PropertyInfo timeSinceStructureDamagedProp = typeof( HUDMechArmorReadout ).GetProperty( "timeSinceStructureDamaged", NonPublic | Instance );

      public static void FixRearStructureDisplay ( HUDMechArmorReadout __instance, AttackDirection shownAttackDirection ) { try {
         HUDMechArmorReadout me = __instance;
         float[] timeSinceStructureDamaged = (float[]) timeSinceStructureDamagedProp.GetValue( me, null );
         Color[] structureRear = (Color[]) structureRearProp.GetValue( me, null );

         float flashPeriod = 1f;
         Color flashColour = Color.white;
         if ( LookAndColor != null ) {
            flashPeriod = LookAndColor.FlashArmorTime;
            flashColour = LookAndColor.ArmorFlash.color;
         }
         Dictionary<ArmorLocation, int> dictionary = null;
         bool mayDisableParts = shownAttackDirection != AttackDirection.None && me.UseForCalledShots;
         if ( mayDisableParts )
            dictionary = HUD.Combat.HitLocation.GetMechHitTable( shownAttackDirection, false );

         for ( int i = 0 ; i < 8 ; i++ ) {
            float structureFlash = Mathf.Clamp01( 1f - timeSinceStructureDamaged[i] / flashPeriod );
            Color structureColor = structureRear[ i ]; // The first line that has typo in original code
            if ( mayDisableParts ) {
               ArmorLocation rearLocation = GetArmorLocationFromIndex( i, true, me.flipRearDisplay );
               bool isIntact = dictionary.ContainsKey( rearLocation ) && dictionary[ rearLocation ] != 0;
               if ( ! isIntact )                       // And the second typo line
                  structureColor = Color.Lerp( structureColor, Color.black, me.hiddenColorLerp );
            }
            UIHelpers.SetImageColor( me.StructureRear[ i ], Color.Lerp( structureColor, flashColour, structureFlash ) );
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
      private static readonly object[] RemoveTargetParams = new object[]{ null, false };

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

      public static void ShowHeatAndStab ( CombatHUDActorDetailsDisplay __instance ) { try {
         // Only override mechs. Other actors are unimportant to us.
         if ( !( __instance.DisplayedActor is Mech mech ) ) return;

         int jets = mech.WorkingJumpjets;
         string line1 = mech.weightClass.ToString(), line2 = null;
         if ( jets > 0 ) line1 += ", " + jets + " JETS";

         int baseHeat = mech.CurrentHeat, newHeat = baseHeat,
            baseStab = (int) mech.CurrentStability, newStab = baseStab;
         if ( mech == HUD.SelectedActor ) { // Two lines in selection panel
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
      }                 catch ( Exception ex ) { Error( ex ); } }

      public static void ShowUnitTonnage ( CombatHUDActorDetailsDisplay __instance ) { try {
         string from = null, to = null;

         if ( __instance.DisplayedActor is Mech mech ) {
            from = mech.weightClass.ToString();
            to = mech.tonnage.ToString();
         } else if ( __instance.DisplayedActor is Vehicle vehicle ) {
            from = vehicle.weightClass.ToString();
            to = vehicle.tonnage.ToString();
         } else
            return;

         switch ( from ) {
         case "LIGHT"   : to += "t LT"; break;
         case "MEDIUM"  : to += "t MED"; break;
         case "HEAVY"   : to += "t HVY"; break;
         case "ASSAULT" : to += "t AST"; break;
         }

         __instance.ActorWeightText.text = __instance.ActorWeightText.text.Replace( from, to );
      }                 catch ( Exception ex ) { Error( ex ); } }

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