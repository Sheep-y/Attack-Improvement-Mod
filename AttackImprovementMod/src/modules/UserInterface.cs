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

   public class UserInterface : BattleModModule {

      private static Color? FloatingArmorColourPlayer;
      private static Color? FloatingArmorColourEnemy;
      private static Color? FloatingArmorColourAlly;

      public override void GameStartsOnce () {
         FloatingArmorColourPlayer = ParseColour( Settings.FloatingArmorColourPlayer );
         FloatingArmorColourEnemy = ParseColour( Settings.FloatingArmorColourEnemy );
         FloatingArmorColourAlly = ParseColour( Settings.FloatingArmorColourAlly );
         if ( FloatingArmorColourPlayer != null || FloatingArmorColourEnemy != null || FloatingArmorColourAlly != null ) {
            BarOwners = new Dictionary<CombatHUDPipBar, ICombatant>();
            Patch( typeof( CombatHUDPipBar ), "ShowValue", new Type[]{ typeof( float ), typeof( Color ), typeof( Color ), typeof( Color ), typeof( bool ) }, "CombatHUDLifeBarPips", null );
            Patch( typeof( CombatHUDNumFlagHex ), "OnActorChanged", "SetPipBarOwner", null );
         }
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
            if ( outlineProp == null || armorProp == null || structureProp == null || outlineRearProp == null || armorRearProp == null || structureRearProp == null )
               Error( "Cannot find outline, armour, and/or structure colour cache of HUDMechArmorReadout.  Cannot make paper dolls divulge under skin damage." );
            else {
               if ( ! Settings.FixPaperDollRearStructure )
                  Warn( "PaperDollDivulgeUnderskinDamage does not imply FixPaperDollRearStructure. Readout may still be bugged." );
               Patch( ReadoutType, "RefreshMechStructureAndArmor", null, "ShowStructureDamageThroughArmour" );
            }
         }
      }

      public override void CombatStartsOnce () {
         if ( Settings.FixPaperDollRearStructure )
            Patch( typeof( HUDMechArmorReadout ), "UpdateMechStructureAndArmor", null, null, "FixRearStructureDisplay" );
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
         if ( Settings.FixLosPreviewHeight )
            Patch( typeof( Pathing ), "UpdateFreePath", null, "FixMoveDestinationHeight" );

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

         if ( Settings.ShowBaseHitchance ) {
            Patch( typeof( CombatHUDWeaponSlot ), "UpdateToolTipsFiring", typeof( ICombatant ), "ShowBaseHitChance", null );
            Patch( typeof( CombatHUDWeaponSlot ), "UpdateToolTipsMelee", typeof( ICombatant ), "ShowBaseMeleeChance", null );
         }
      }

      public override void CombatStarts () {
         if ( Settings.ShowNumericInfo ) {
            targetDisplay = HUD?.TargetingComputer?.ActorInfo?.DetailsDisplay;
            HUD?.MechTray?.ActorInfo?.DetailsDisplay?.transform?.transform?.Translate( 0, -15, 0 );
         }
      }

      public override void CombatEnds () {
         BarOwners?.Clear();
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
               componentName += " (" + ( allAmmo = weaponComp.CurrentAmmo ) + ")";
            else if ( mechComponent is AmmunitionBox ammo )
               componentName += " (" + ammo.CurrentAmmo + "/" + ammo.AmmoCapacity + ")";
            if ( mechComponent.DamageLevel >= ComponentDamageLevel.NonFunctional || allAmmo <= 0 )
               ToolTip.DebuffStrings.Add( new Text( componentName ) );
            else
               ToolTip.BuffStrings.Add( new Text( componentName ) );
         }
         return false;
      }                 catch ( Exception ex ) { return Error( ex ); } }

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
            } else {
               movement = "Move " + ( (int) mech.MaxWalkDistance ) + "/" + ( (int) mech.MaxSprintDistance );
               if ( jets > 0 ) movement += "\nJump " + (int) mech.JumpDistance;
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

      public static MapEncounterLayerDataCell[] GetEncounterCellsAtPosition ( Vector3 position ) {
         MapEncounterLayerDataCell cellAt = Combat?.EncounterLayerData?.GetCellAt( position );
         if ( cellAt == null ) return null;
         return new MapEncounterLayerDataCell[]{ cellAt };
      }

      public static DesignMaskDef GetDesignMasksAtPosition ( Vector3 position ) {
         MapEncounterLayerDataCell[] cells = GetEncounterCellsAtPosition( position );
         if ( cells == null || cells.Length <= 0 ) return null;
         return Combat?.MapMetaData?.GetPriorityDesignMask( cells[0].relatedTerrainCell );
      }

      public static void CorrectProjectedHeat ( Mech __instance, ref float __result ) {
         if ( __instance.HasMovedThisRound ) return;
         SelectionState state = HUD?.SelectionHandler?.ActiveState;
         Vector3 position;
         if      ( state is SelectionStateSprint sprint ) position = sprint.PreviewPos;
         else if ( state is SelectionStateMove move ) position = move.PreviewPos;
         else if ( state is SelectionStateJump jump ) position = jump.PreviewPos;
         else return;
         DesignMaskDef local = __instance.occupiedDesignMask, preview = GetDesignMasksAtPosition( position );
         if ( ReferenceEquals( local, preview ) ) return;
         float here = local?.heatSinkMultiplier ?? 1f, there = preview?.heatSinkMultiplier ?? 1f;
         if ( here == there ) return;
         __result *= there / here;
      }

      // ============ Floating Nameplate ============

      private static Dictionary<CombatHUDPipBar, ICombatant> BarOwners;

      public static void CombatHUDLifeBarPips ( CombatHUDPipBar __instance, ref Color shownColor ) {
         if ( ! ( __instance is CombatHUDLifeBarPips me ) || ! BarOwners.TryGetValue( __instance, out ICombatant owner ) ) return;

         Team team = owner?.team;
         if ( team == null || owner.IsDead ) return;

         if ( FloatingArmorColourPlayer != null && team.IsLocalPlayer ) {
            shownColor = FloatingArmorColourPlayer.GetValueOrDefault();

         } else if ( FloatingArmorColourEnemy != null && team.IsEnemy( BattleTechGame?.Combat?.LocalPlayerTeam ) ) {
            shownColor = FloatingArmorColourEnemy.GetValueOrDefault();

         } else if ( FloatingArmorColourAlly != null && team.IsFriendly( BattleTechGame?.Combat?.LocalPlayerTeam ) ) {
            shownColor = FloatingArmorColourAlly.GetValueOrDefault();
         }
      }

      public static void SetPipBarOwner ( CombatHUDNumFlagHex __instance ) {
         ICombatant owner = __instance.DisplayedCombatant;
         CombatHUDLifeBarPips bar = __instance.ActorInfo.ArmorBar;
         BarOwners[ bar ] = owner;
      }

      // ============ Others ============

      public static void FixMoveDestinationHeight ( Pathing __instance ) {
         __instance.ResultDestination.y = Combat.MapMetaData.GetLerpedHeightAt( __instance.ResultDestination );
      }


      private static Dictionary<CombatHUDMWStatus, Pilot> PilotStatus;

      // Force update on portrait right click and forced unexpansion.
      public static void RefreshPilotHint ( CombatHUDMWStatus __instance ) {
         if ( PilotStatus.TryGetValue( __instance, out Pilot pilot ) )
            __instance.RefreshPilot( pilot );
      }

      public static void ReplacePilotHint ( CombatHUDMWStatus __instance, Pilot pilot ) {
         if ( ! PilotStatus.ContainsKey( __instance ) )
            PilotStatus.Add( __instance, pilot );
         if ( ( HUD.SelectedActor != null || ! __instance.IsExpanded ) && ! pilot.IsIncapacitated )
            __instance.InjuriesItem.ShowExistingIcon( new Text( Settings.ShortPilotHint, new object[]{
               pilot.Injuries, ( pilot.Health - pilot.Injuries ), pilot.Health, pilot.Gunnery, pilot.Piloting, pilot.Guts, pilot.Tactics } ),
               CombatHUDPortrait.GetPilotInjuryColor( pilot, HUD ) );
      }

      // ============ Base Hit Chances ============

      public static void ShowBaseHitChance ( CombatHUDWeaponSlot __instance, ICombatant target ) { try {
         if ( HUD.SelectedActor is Mech mech ) {
            float baseChance = RollModifier.StepHitChance( Combat.ToHit.GetBaseToHitChance( HUD.SelectedActor ) ) * 100;
            __instance.ToolTipHoverElement.BuffStrings.Add( new Text( Translate( "GUNNERY" ) + " " + mech.SkillGunnery + string.Format( " = {0:0.#}%", baseChance ) ) );
         }
      }                 catch ( Exception ex ) { Error( ex ); } }

      public static void ShowBaseMeleeChance ( CombatHUDWeaponSlot __instance, ICombatant target ) { try {
         if ( HUD.SelectedActor is Mech mech ) {
            float baseChance = RollModifier.StepHitChance( Combat.ToHit.GetBaseMeleeToHitChance( mech ) ) * 100;
            __instance.ToolTipHoverElement.BuffStrings.Add( new Text( Translate( "PILOTING" ) + " " + mech.SkillPiloting + string.Format( " = {0:0.#}%", baseChance ) ) );
         }
      }                 catch ( Exception ex ) { Error( ex ); } }
   }
}