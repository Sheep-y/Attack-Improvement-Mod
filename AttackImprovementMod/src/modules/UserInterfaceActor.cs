using BattleTech.UI;
using BattleTech;
using Harmony;
using Localize;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using UnityEngine;

namespace Sheepy.BattleTechMod.AttackImprovementMod {
   using static Mod;
   using static ChassisLocations;
   using static System.Reflection.BindingFlags;

   public class UserInterfaceActor : BattleModModule {

      public override void GameStartsOnce () {
         // Done on game load to be effective in campaign mech bay
         if ( Settings.ShowUnderArmourDamage ) {
            Type ReadoutType = typeof( HUDMechArmorReadout );
            TryRun( Log, () => {
               outlineProp = ReadoutType.GetProperty( "armorOutlineCached", NonPublic | Instance );
               armorProp = ReadoutType.GetProperty( "armorCached", NonPublic | Instance );
               structureProp = ReadoutType.GetProperty( "structureCached", NonPublic | Instance );
               outlineRearProp = ReadoutType.GetProperty( "armorOutlineRearCached", NonPublic | Instance );
               armorRearProp = ReadoutType.GetProperty( "armorRearCached", NonPublic | Instance );
               structureRearProp = ReadoutType.GetProperty( "structureRearCached", NonPublic | Instance );
            } );
            if ( AnyNull( outlineProp, armorProp, structureProp, outlineRearProp, armorRearProp, structureRearProp ) )
               Error( "Cannot find outline, armour, and/or structure colour cache of HUDMechArmorReadout.  Cannot make paper dolls divulge under skin damage." );
            else {
               if ( ! Settings.FixPaperDollRearStructure )
                  Warn( "PaperDollDivulgeUnderskinDamage does not imply FixPaperDollRearStructure. Readout may still be bugged." );
               Patch( ReadoutType, "RefreshMechStructureAndArmor", null, "ShowStructureDamageThroughArmour" );
            }
         }
         if ( Settings.FixPaperDollRearStructure )
            Patch( typeof( HUDMechArmorReadout ), "UpdateMechStructureAndArmor", null, null, "FixRearStructureDisplay" );
      }

      public override void CombatStartsOnce () {
         if ( Settings.ShowNumericInfo ) {
            Patch( typeof( CombatHUDActorDetailsDisplay ), "RefreshInfo", null, "ShowNumericInfo" );
            Patch( typeof( CombatHUDActorInfo ), "RefreshPredictedHeatInfo", null, "RecordRefresh" );
            Patch( typeof( CombatHUDActorInfo ), "RefreshPredictedStabilityInfo", null, "RecordRefresh" );
            // Force heat/stab number refresh
            Patch( typeof( CombatHUDMechTray ), "Update", null, "RefreshHeatAndStab" );
            // Force move/distance number refresh
            Patch( typeof( SelectionStateMove ), "ProcessMousePos", null, "RefreshMoveAndDist" );
            Patch( typeof( SelectionStateJump ), "ProcessMousePos", null, "RefreshMoveAndDist" );
         }
         if ( Settings.FixHeatPreview )
            Patch( typeof( Mech ), "get_AdjustedHeatsinkCapacity", null, "CorrectProjectedHeat" );

         if ( Settings.ShowUnitTonnage )
            Patch( typeof( CombatHUDActorDetailsDisplay ), "RefreshInfo", null, "ShowUnitTonnage" );

         if ( Settings.ShowAmmoInTooltip || Settings.ShowEnemyAmmoInTooltip ) {
            MechTrayArmorHoverToolTipProp = typeof( CombatHUDMechTrayArmorHover ).GetProperty( "ToolTip", NonPublic | Instance );
            if ( MechTrayArmorHoverToolTipProp == null )
               Warn( "Cannot access CombatHUDMechTrayArmorHover.ToolTip, ammo not displayed in paperdoll tooltip." );
            else
               Patch( typeof( CombatHUDMechTrayArmorHover ), "setToolTipInfo", new Type[]{ typeof( Mech ), typeof( ArmorLocation ) }, "OverridePaperDollTooltip", null );
         }

         if ( Settings.ShortPilotHint != null ) {
            PilotStatus = new Dictionary<CombatHUDMWStatus, Pilot>(4);
            Patch( typeof( CombatHUDMWStatus ), "OnPortraitRightClicked", null, "RefreshPilotHint" );
            Patch( typeof( CombatHUDMWStatus ), "ForceUnexpand", null, "RefreshPilotHint" );
            Patch( typeof( CombatHUDMWStatus ), "RefreshPilot", null, "ReplacePilotHint" );
         }
      }

      public override void CombatStarts () {
         if ( Settings.ShowNumericInfo ) {
            targetDisplay = HUD?.TargetingComputer?.ActorInfo?.DetailsDisplay;
            HUD?.MechTray?.ActorInfo?.DetailsDisplay?.transform?.transform?.Translate( 0, -15, 0 );
         }
      }

      public override void CombatEnds () {
         PilotStatus?.Clear();
      }

      // ============ Paper Doll ============

      private static PropertyInfo outlineProp, armorProp, structureProp, outlineRearProp, armorRearProp, structureRearProp;
      private static readonly ChassisLocations[] Normal  = new ChassisLocations[]{ Head, LeftArm , LeftTorso , CenterTorso, RightTorso, RightArm, LeftLeg , RightLeg };
      private static readonly ChassisLocations[] Flipped = new ChassisLocations[]{ Head, RightArm, RightTorso, CenterTorso, LeftTorso , LeftArm , RightLeg, LeftLeg  };

      private static bool IsStructureDamaged ( ref float percent, Mech mech, MechDef mechDef, ChassisLocations location ) {
         if ( float.IsNaN( percent ) ) {
            float hp, maxHp;
            if ( mech != null ) {
               hp = mech.GetCurrentStructure( location );
               maxHp = mech.GetMaxStructure( location );
            } else { // if ( mechDef != null )
               hp = mechDef.GetLocationLoadoutDef( location ).CurrentInternalStructure;
               maxHp = mechDef.GetChassisLocationDef( location ).InternalStructure;
            }
            percent = hp / maxHp;
         }
         return percent < 1f;
      }

      public static void ShowStructureDamageThroughArmour ( HUDMechArmorReadout __instance ) { try {
         HUDMechArmorReadout me = __instance;
         Mech mech = me.DisplayedMech;
         MechDef mechDef = mech?.MechDef ?? me.DisplayedMechDef;
         if ( mech == null && mechDef == null ) return;
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
               if ( IsStructureDamaged( ref percent, mech, mechDef, location[ i ] ) ) {
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
               if ( IsStructureDamaged( ref percent, mech, mechDef, location[ i ] ) ) { // i is not typo. We want to check same chassis location as front.
                  if ( structureRear == null ) {
                     structureRear = (Color[]) structureRearProp.GetValue( me, null );
                     outlineRear = (Color[]) outlineRearProp.GetValue( me, null );
                  }
                  outlineRear[ j ] = structureRear[ j ] = armorRear[ j ];
                  armorRear[ j ] = clear;
               }
            }
         }
      }                 catch ( Exception ex ) { Error( ex ); } }

      public static IEnumerable<CodeInstruction> FixRearStructureDisplay ( IEnumerable<CodeInstruction> input ) {
         List<CodeInstruction> result = new List<CodeInstruction>( 100 );
         int count12 = 0, count13 = 0, last12 = -1, last13 = -1;
         foreach ( CodeInstruction code in input ) {
            if ( code.opcode.Name == "ldloc.s" && code.operand != null && code.operand is LocalBuilder local ) {
               if ( local.LocalIndex == 12 ) {
                  count12++;
                  last12 = result.Count();
               } else if ( local.LocalIndex == 13 ) {
                  count13++;
                  last13 = result.Count();
               }
            }
            result.Add( code );
         }
         if ( count12 == count13 + 2 )
            result[ last13 ] = result[ last12 ];
         else
            Warn( "Cannot find correct flags to transpile. FixRearStructureDisplay not applied." );
         return result;
      }

      private static PropertyInfo MechTrayArmorHoverToolTipProp;

      [ Harmony.HarmonyPriority( Harmony.Priority.Low ) ]
      public static bool OverridePaperDollTooltip ( CombatHUDMechTrayArmorHover __instance, Mech mech, ArmorLocation location ) { try {
         if ( ! FriendOrFoe( mech, Settings.ShowAmmoInTooltip, Settings.ShowEnemyAmmoInTooltip ) ) return true;
         CombatHUDMechTrayArmorHover me = __instance;
         CombatHUDTooltipHoverElement ToolTip = (CombatHUDTooltipHoverElement) MechTrayArmorHoverToolTipProp.GetValue( me, null );
         ToolTip.BuffStrings.Clear();
         ToolTip.DebuffStrings.Clear();
         ToolTip.BasicString = Mech.GetLongArmorLocation(location);
         foreach ( MechComponent mechComponent in mech.GetComponentsForLocation( MechStructureRules.GetChassisLocationFromArmorLocation( location ), ComponentType.NotSet ) ) {
            string componentName = mechComponent.UIName.ToString();
            int allAmmo = 1;
            if ( mechComponent is Weapon weaponComp && weaponComp.AmmoCategory != AmmoCategory.NotSet )
               componentName += " (x" + ( allAmmo = weaponComp.CurrentAmmo ) + ")";
            else if ( mechComponent is AmmunitionBox ammo )
               componentName += " (" + ammo.CurrentAmmo + "/" + ammo.AmmoCapacity + ")";
            if ( mechComponent.DamageLevel >= ComponentDamageLevel.NonFunctional || allAmmo <= 0 )
               ToolTip.DebuffStrings.Add( new Text( componentName ) );
            else
               ToolTip.BuffStrings.Add( new Text( componentName ) );
         }
         return false;
      }                 catch ( Exception ex ) { return Error( ex ); } }

      // ============ Heat and Stability ============

      private static CombatHUDActorDetailsDisplay targetDisplay = null;

      public static void ShowNumericInfo ( CombatHUDActorDetailsDisplay __instance ) { try {
         // Only override mechs. Other actors are unimportant to us.
         if ( !( __instance.DisplayedActor is Mech mech ) ) return;

         int jets = mech.WorkingJumpjets;
         StringBuilder text = new StringBuilder( 100 );
         text.Append( mech.weightClass );
         if ( jets > 0 ) text.Append( ", " ).Append( jets ).Append( " JETS" );
         text.Append( '\n' );

         CombatSelectionHandler selection = HUD?.SelectionHandler;
         int baseHeat = mech.CurrentHeat, newHeat = baseHeat,
             baseStab = (int) mech.CurrentStability, newStab = baseStab;
         string movement = "";
         if ( selection != null && selection.SelectedActor == mech && __instance != targetDisplay ) { // Show predictions in selection panel
            newHeat += mech.TempHeat;

            if ( selection.SelectedActor == mech ) try {
               newHeat += selection.ProjectedHeatForState;
               if ( ! mech.HasMovedThisRound )
                  newHeat += mech.StatCollection.GetValue<int>( "EndMoveHeat" );
               if ( ! mech.HasAppliedHeatSinks )
                  newHeat -= mech.AdjustedHeatsinkCapacity;
               newHeat = Math.Min( newHeat, mech.MaxHeat );
               newStab = (int) selection.ProjectedStabilityForState;
            } catch ( Exception ex ) { Error( ex ); }

            try { if ( selection.ActiveState is SelectionStateMove move ) {
               float maxCost = move is SelectionStateSprint sprint ? mech.MaxSprintDistance : mech.MaxWalkDistance;
               mech.Pathing.CurrentGrid.GetPathTo( move.PreviewPos, mech.Pathing.CurrentDestination, maxCost, null, out float costLeft, out Vector3 ResultDestination, out float lockedAngle, false, 0f, 0f, 0f, true, false );
               movement = move is SelectionStateSprint ? "SPRINT " : "MOVE ";
               movement += (int) costLeft + "/" + (int) maxCost;
            } else if ( selection.ActiveState is SelectionStateJump jump ) {
               float maxCost = mech.JumpDistance, cost = Vector3.Distance( jump.PreviewPos, mech.CurrentPosition );
               movement = "JUMP " + (int) ( maxCost - cost ) + "/" + (int) maxCost;
            } } catch ( Exception ex ) { Error( ex ); }

         } else if ( selection != null ) try {  // Target panel or non-selection. Show min/max numbers and distance.
            Vector3? position;
            if      ( selection.ActiveState is SelectionStateSprint sprint ) position = sprint.PreviewPos;
            else if ( selection.ActiveState is SelectionStateMove move ) position = move.PreviewPos;
            else if ( selection.ActiveState is SelectionStateJump jump ) position = jump.PreviewPos;
            else position = HUD.SelectedActor?.CurrentPosition;
            if ( position != null && HUD.SelectedActor != null ) {
               int baseDist = (int) Vector3.Distance( HUD.SelectedActor.CurrentPosition, mech.CurrentPosition ),
                   newDist = (int) Vector3.Distance( position.GetValueOrDefault(), mech.CurrentPosition );
               text.Append( "Dist " ).Append( baseDist );
               if ( baseDist != newDist )
                  text.Append( " >> " ).Append( newDist );
               text.Append( '\n' );
            }
         } catch ( Exception ex ) { Error( ex ); }

         text.Append( "Heat " ).Append( baseHeat );
         if ( baseHeat == newHeat )
            text.Append( '/' ).Append( mech.MaxHeat );
         else {
            text.Append( " >> " );
            if ( newHeat < 0 ) text.Append( '(' ).Append( -newHeat ).Append( ')' );
            else text.Append( newHeat );
         }
         text.Append( '\n' );

         text.Append( "Stab " ).Append( baseStab );
         if ( baseStab == newStab )
            text.Append( '/' ).Append( mech.MaxStability );
         else
            text.Append( " >> " ).Append( newStab );
         text.Append( '\n' );

         text.Append( movement );

         __instance.ActorWeightText.text = text.ToString();
         __instance.JumpJetsHolder.SetActive( false );
      }                 catch ( Exception ex ) { Error( ex ); } }

      public static void ShowUnitTonnage ( CombatHUDActorDetailsDisplay __instance ) { try {
         CombatHUDActorDetailsDisplay me = __instance;
         TMPro.TextMeshProUGUI label = me.ActorWeightText;
         string from = null, to = null;

         if ( me.DisplayedActor is Mech mech ) {
            from = mech.weightClass.ToString();
            to = mech.tonnage.ToString();
            if ( mech.WorkingJumpjets <= 0 )
               to += "t " + from;
            else switch ( from ) {
               case "LIGHT"   : to += "t LT"; break;
               case "MEDIUM"  : to += "t MED"; break;
               case "HEAVY"   : to += "t HVY"; break;
               case "ASSAULT" : to += "t AST"; break;
            }
         } else if ( me.DisplayedActor is Vehicle vehicle ) {
            from = vehicle.weightClass.ToString();
            to = vehicle.tonnage.ToString();
            if ( label.text.Contains( to ) ) return; // Already added by Extended Info, which has a generic name and may have false positive with usual detection
            to += " TONS\n" + from; // Otherwise mimic the style for consistency
         } else
            return;

         label.text = label.text.Replace( from, to );
      }                 catch ( Exception ex ) { Error( ex ); } }

      private static bool needRefresh = false;
      public static void RecordRefresh () {
         needRefresh = true;
      }

      public static void RefreshHeatAndStab ( CombatHUDMechTray __instance ) {
         if ( ! needRefresh ) return;
         __instance?.ActorInfo?.DetailsDisplay?.RefreshInfo();
         needRefresh = false;
      }

      public static void RefreshMoveAndDist () {
         HUD?.TargetingComputer?.ActorInfo?.DetailsDisplay?.RefreshInfo();
         HUD?.MechTray?.ActorInfo?.DetailsDisplay?.RefreshInfo();
      }

      public static void CorrectProjectedHeat ( Mech __instance, ref float __result ) { try {
         if ( __instance.HasMovedThisRound ) return;
         SelectionState state = HUD?.SelectionHandler?.ActiveState;
         Vector3 position;
         if      ( state is SelectionStateSprint sprint ) position = sprint.PreviewPos;
         else if ( state is SelectionStateMove move ) position = move.PreviewPos;
         else if ( state is SelectionStateJump jump ) position = jump.PreviewPos;
         else return;
         HashSet<DesignMaskDef> masks = new HashSet<DesignMaskDef>();
         DesignMaskDef local = __instance.occupiedDesignMask, preview = Combat.MapMetaData.GetPriorityDesignMaskAtPos( position );
         if ( preview != null ) masks.Add( preview );
         float here = local?.heatSinkMultiplier ?? 1, there = 1, extra = 0;
         if ( state is SelectionStateMove ) { // If walk or sprint, Check geothermal and radiation field.
            Pathing path = __instance.Pathing;
            masks.UnionWith( CombatHUDStatusPanel.GetStickyMasksForWaypoints( Combat,
               ActorMovementSequence.ExtractWaypointsFromPath( __instance, path.CurrentPath, path.ResultDestination, path.CurrentMeleeTarget, path.MoveType ) ) );
         }
         foreach ( DesignMaskDef mask in masks ) {
            there *= mask.heatSinkMultiplier;
            extra += mask.heatPerTurn;
         }
         if ( here == there && extra == 0 ) return;
         __result = ( ( __result / here ) + extra ) * there;
      }                 catch ( Exception ex ) { Error( ex ); } }

      // ============ Mech Tray ============

      private static Dictionary<CombatHUDMWStatus, Pilot> PilotStatus;

      // Force update on portrait right click and forced unexpansion.
      public static void RefreshPilotHint ( CombatHUDMWStatus __instance ) {
         if ( PilotStatus.TryGetValue( __instance, out Pilot pilot ) )
            __instance.RefreshPilot( pilot );
      }

      public static void ReplacePilotHint ( CombatHUDMWStatus __instance, Pilot pilot ) { try {
         if ( ! PilotStatus.ContainsKey( __instance ) )
            PilotStatus.Add( __instance, pilot );
         if ( ( HUD.SelectedActor != null || ! __instance.IsExpanded ) && ! pilot.IsIncapacitated )
            __instance.InjuriesItem.ShowExistingIcon( new Text( Settings.ShortPilotHint, new object[]{
               pilot.Injuries, ( pilot.Health - pilot.Injuries ), pilot.Health, pilot.Gunnery, pilot.Piloting, pilot.Guts, pilot.Tactics } ),
               CombatHUDPortrait.GetPilotInjuryColor( pilot, HUD ) );
      }                 catch ( Exception ex ) { Error( ex ); } }
   }
}