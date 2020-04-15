using BattleTech.UI;
using BattleTech;
using Harmony;
using Localize;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Sheepy.BattleTechMod.AttackImprovementMod {
   using static Mod;
   using static ChassisLocations;
   using static System.Reflection.BindingFlags;

   public class UserInterfacePanels : BattleModModule {

      public override void GameStartsOnce () {
         // Done on game load to be effective in campaign mech bay
         Type ReadoutType = typeof( HUDMechArmorReadout );
         if ( Settings.ShowUnderArmourDamage || Settings.FixPaperDollRearStructure )
            TryRun( Log, () => {
               outlineProp = ReadoutType.GetProperty( "armorOutlineCached", NonPublic | Instance );
               armorProp = ReadoutType.GetProperty( "armorCached", NonPublic | Instance );
               structureProp = ReadoutType.GetProperty( "structureCached", NonPublic | Instance );
               outlineRearProp = ReadoutType.GetProperty( "armorOutlineRearCached", NonPublic | Instance );
               armorRearProp = ReadoutType.GetProperty( "armorRearCached", NonPublic | Instance );
               structureRearProp = ReadoutType.GetProperty( "structureRearCached", NonPublic | Instance );
               timeSinceStructureDamagedProp = typeof( HUDMechArmorReadout ).GetProperty( "timeSinceStructureDamaged", NonPublic | Instance );
            } );
         if ( Settings.ShowUnderArmourDamage ) {
            if ( AnyNull( outlineProp, armorProp, structureProp, outlineRearProp, armorRearProp, structureRearProp ) )
               Error( "Cannot find outline, armour, and/or structure colour cache of HUDMechArmorReadout.  Cannot make paper dolls divulge under skin damage." );
            else {
               if ( ! Settings.FixPaperDollRearStructure )
                  Warn( "PaperDollDivulgeUnderskinDamage does not imply FixPaperDollRearStructure. Readout may still be bugged." );
               Patch( ReadoutType, "RefreshMechStructureAndArmor", null, "ShowStructureDamageThroughArmour" );
            }
         }
         if ( Settings.FixPaperDollRearStructure ) {
            if ( structureRearProp == null || timeSinceStructureDamagedProp == null )
               Error( "Cannot find HUDMechArmorReadout.structureRearCached, and/or HUDMechArmorReadout.timeSinceStructureDamaged, paper doll rear structures not fixed." );
            else
               Patch( typeof( HUDMechArmorReadout ), "UpdateMechStructureAndArmor", null, "FixRearStructureDisplay" );
         }
         // Must be placed here to have effect
         if ( Settings.LabelPaperDollSide )
            Patch( typeof( HUDMechArmorReadout ), "Init", null, "AddPaperDollSideLabel" );
      }

      public override void CombatStartsOnce () {
         if ( Settings.ShowNumericInfo ) {
            Patch( typeof( CombatHUDActorDetailsDisplay ), "RefreshInfo", null, "ShowNumericInfo" );
            Patch( typeof( CombatHUDActorInfo ), "RefreshAllInfo" , "RecordTarget", "ShowBuildingInfo" );
            Patch( typeof( CombatHUDActorInfo ), "RefreshPredictedHeatInfo", null, "RecordRefresh" );
            Patch( typeof( CombatHUDActorInfo ), "RefreshPredictedStabilityInfo", null, "RecordRefresh" );
            // Force heat/stab number refresh
            Patch( typeof( CombatHUDMechTray ), "Update", null, "RefreshActorInfo" );
            // Force move/distance number refresh
            Patch( typeof( SelectionStateMove ), "ProcessMousePos", null, "RefreshMoveAndDist" );
            Patch( typeof( SelectionStateJump ), "ProcessMousePos", null, "RefreshMoveAndDist" );
            HeauUpDisplay.HookCalloutToggle( RefreshTargetInfo );
         }
         if ( Settings.FixHeatPreview )
            Patch( typeof( Mech ), "get_AdjustedHeatsinkCapacity", null, "CorrectProjectedHeat" );

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

         if ( Settings.MechTrayGreyActedPilot )
            Patch( typeof( CombatHUDPortrait ), "RefreshDisplayedActor", null, "ColourPilotNames" );
      }

      public override void CombatStarts () {
         if ( Settings.ShowNumericInfo ) {
            TargetDisplay = HUD?.TargetingComputer.ActorInfo.DetailsDisplay;
            // Enlarge target text background
            TargetDisplay.transform.transform.Translate( 34, -12, 0 );
            RectTransform TargetRect = TargetDisplay.transform.GetComponent<RectTransform>();
            TargetRect?.SetSizeWithCurrentAnchors( RectTransform.Axis.Horizontal, 250 );
            TargetRect?.SetSizeWithCurrentAnchors( RectTransform.Axis.Vertical, 90 );
            TargetDisplay.ActorWeightText.enableWordWrapping = false; // Prevent skill names from wrapping
            //LogGuiTree( HUD?.TargetingComputer.ActorInfo );
            // Shift selected actor info
            HUD?.MechTray.ActorInfo.DetailsDisplay.transform.transform.Translate( 0, -15, 0 );
         }
      }

      public override void CombatEnds () {
         PilotStatus?.Clear();
      }

      // ============ Paper Doll ============

      private static PropertyInfo outlineProp, armorProp, structureProp, outlineRearProp, armorRearProp, structureRearProp, timeSinceStructureDamagedProp;
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

      private static UILookAndColorConstants LookAndColor;

      public static void FixRearStructureDisplay ( HUDMechArmorReadout __instance, AttackDirection shownAttackDirection ) { try {
         HUDMechArmorReadout me = __instance;
         float[] timeSinceStructureDamaged = (float[]) timeSinceStructureDamagedProp.GetValue( me, null );
         Color[] structureRear = (Color[]) structureRearProp.GetValue( me, null );

         float flashPeriod = 0f;
         Color flashColour = Color.black;
         if ( LookAndColor == null )
            TryRun( () => LookAndColor = HBS.LazySingletonBehavior<UIManager>.Instance.UILookAndColorConstants );
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
               ArmorLocation rearLocation = HUDMechArmorReadout.GetArmorLocationFromIndex( i, true, me.flipRearDisplay );
               bool isIntact = dictionary.ContainsKey( rearLocation ) && dictionary[ rearLocation ] != 0;
               if ( ! isIntact )                       // And the second typo line
                  structureColor = Color.Lerp( structureColor, Color.black, me.hiddenColorLerp );
            }
            UIHelpers.SetImageColor( me.StructureRear[ i ], Color.Lerp( structureColor, flashColour, structureFlash ) );
         }
      }                 catch ( Exception ex ) { Error( ex ); } }

      private static PropertyInfo MechTrayArmorHoverToolTipProp;

      [ HarmonyPriority( Priority.Low ) ]
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
            if ( mechComponent is Weapon weaponComp && !weaponComp.AmmoCategoryValue.Is_NotSet )
               componentName += "<size=80%> (x" + ( allAmmo = weaponComp.CurrentAmmo ) + ")";
            else if ( mechComponent is AmmunitionBox ammo ) {
               int curr = ammo.CurrentAmmo, max = ammo.AmmoCapacity;
               componentName += "<size=80%> (" + curr + "/" + max + ")";
               if ( curr < max/2 ) componentName = "<#808080>" + componentName;
            }
            if ( mechComponent.DamageLevel >= ComponentDamageLevel.NonFunctional || allAmmo <= 0 )
               ToolTip.DebuffStrings.Add( new Text( componentName ) );
            else
               ToolTip.BuffStrings.Add( new Text( componentName ) );
         }
         return false;
      }                 catch ( Exception ex ) { return Error( ex ); } }

      public static void AddPaperDollSideLabel ( HUDMechArmorReadout __instance ) { try {
         TMPro.TextMeshProUGUI[] labels = __instance.GetComponentsInChildren<TMPro.TextMeshProUGUI>();
         TMPro.TextMeshProUGUI front = labels.FirstOrDefault( e => e.name == "MechTray_FrontText" ), rear = labels?.FirstOrDefault( e => e.name == "MechTray_RearText" );
         front?.SetText( PaperDollSideLabel( front.text, __instance.flipFrontDisplay ) );
         rear?.SetText( PaperDollSideLabel( rear.text, __instance.flipRearDisplay ) );
      }                 catch ( Exception ex ) { Error( ex ); } }

      private static string PaperDollSideLabel ( string text, bool reversed ) {
         if ( reversed ) return "R   " + text + "   L";
         return "L   " + text + "   R";
      }

      // ============ Numeric Info ============

      private static CombatHUDActorDetailsDisplay TargetDisplay;
      private static ICombatant ActorInfoTarget;

      public static void RecordTarget ( CombatHUDActorInfo __instance, ICombatant ___displayedCombatant ) { ActorInfoTarget = ___displayedCombatant; }
      public static void ShowBuildingInfo ( CombatHUDActorInfo __instance ) { try {
         if ( ActorInfoTarget is BattleTech.Building )
            __instance.DetailsDisplay?.gameObject?.SetActive( true );
      }                 catch ( Exception ex ) { Error( ex ); } }

      public static void ShowNumericInfo ( CombatHUDActorDetailsDisplay __instance ) { try {
         ICombatant target = __instance.DisplayedActor ?? ActorInfoTarget;
         if ( target == null ) return;
         string prefix = null, numbers = null, postfix = null;

         StringBuilder text = new StringBuilder( 100 );
         if ( __instance == TargetDisplay && IsCalloutPressed && target.GetPilot() is Pilot pilot ) {
            foreach ( Ability ability in pilot.Abilities ) {
               if ( ability?.Def == null || ! ability.Def.IsPrimaryAbility ) continue;
               text.Append( ability.Def.Description?.Name ).Append( "\n" );
            }
            if ( text.Length <= 0 ) text.Append( "(No skills)" );
            if ( target is Mech mech )
               text.Append( FormatMeter( "Stab", mech.CurrentStability, mech.MaxStability ) );

         } else {
            if ( target is Mech mech ) {
               float heat = mech.CurrentHeat, stab = mech.CurrentStability;
               if ( __instance != TargetDisplay && HUD?.SelectionHandler?.SelectedActor == mech ) {
                  GetPreviewNumbers( mech, ref heat, ref stab, ref postfix );
                  prefix = "";
                  numbers = FormatPrediction( "Heat", mech.CurrentHeat, heat ) + "\n"
                          + FormatPrediction( "Stab", mech.CurrentStability, stab ) + "\n";
               } else
                  numbers = FormatMeter( "Heat", heat, mech.MaxHeat ) + "\n"
                          + FormatMeter( "Stab", stab, mech.MaxStability ) + "\n";
            }
            if ( prefix == null && HUD?.SelectedActor != target )
               prefix = GetTargetNumbers( target ) + "\n";
            text.Append( GetBasicInfo( target ) ).Append( '\n' );
            text.Append( prefix ).Append( numbers ).Append( postfix );
         }
         __instance.ActorWeightText.text = text.ToString();

         __instance.JumpJetsHolder.SetActive( false );
      }                 catch ( Exception ex ) { Error( ex ); } }

      private static object GetBasicInfo ( ICombatant target ) {
         if ( target is Mech mech ) {
            int jets = mech.WorkingJumpjets;
            string weight = mech.weightClass.ToString();
            if ( jets > 0 )
               switch ( mech.weightClass ) {
                  case WeightClass.LIGHT  : weight = "LT"; break;
                  case WeightClass.MEDIUM : weight = "MED"; break;
                  case WeightClass.HEAVY  : weight = "HVY"; break;
                  case WeightClass.ASSAULT: weight = "ASLT"; break;
               }
            string ton = ( (int) mech.tonnage ) + "T " + weight;
            return jets > 0 ? string.Format( "{0}, {1} JETS", ton, jets ) : ton;

         } else if ( target is Vehicle vehicle )
            return ( (int) vehicle.tonnage ) + "T " + vehicle.weightClass;

         else if ( target is Turret turret )
            return turret.TurretDef.Chassis.weightClass;

         return "";
      }

      private static string FormatPrediction ( string label, float from, float to ) {
         return string.Format( "{0} {1:0} >> {2:0;(0)}", label, from, to );
      }

      private static string FormatMeter ( string label, float from, float max ) {
         return string.Format( "{0} {1:0}/{2:0}", label, from, max );
      }

      private static void GetPreviewNumbers ( Mech mech, ref float heat, ref float stab, ref string movement ) {
         CombatSelectionHandler selection = HUD?.SelectionHandler;

         heat += mech.TempHeat;
         try {
            heat += selection.ProjectedHeatForState;
            if ( ! mech.HasMovedThisRound )
               heat += mech.StatCollection.GetValue<int>( "EndMoveHeat" );
            if ( ! mech.HasAppliedHeatSinks )
               heat -= mech.AdjustedHeatsinkCapacity;
            heat = Math.Min( heat, mech.MaxHeat );
            stab = (int) selection.ProjectedStabilityForState;
         } catch ( Exception ex ) { Error( ex ); }

         string moveType = null;
         float spareMove = 0, maxMove = 0;
         try { if ( ActiveState is SelectionStateMove move ) {
            maxMove = move is SelectionStateSprint sprint ? mech.MaxSprintDistance : mech.MaxWalkDistance;
            mech.Pathing.CurrentGrid.GetPathTo( move.PreviewPos, mech.Pathing.CurrentDestination, maxMove, null, out spareMove, out Vector3 ResultDestination, out float lockedAngle, false, 0f, 0f, 0f, true, false );
            spareMove = DeduceLockedAngle( spareMove, mech.Pathing, ref maxMove );
            moveType = move is SelectionStateSprint ? uiManager.UILookAndColorConstants.Tooltip_Sprint : uiManager.UILookAndColorConstants.Tooltip_Move;
         } else if ( ActiveState is SelectionStateJump jump ) {
            maxMove = mech.JumpDistance;
            spareMove = maxMove - Vector3.Distance( jump.PreviewPos, mech.CurrentPosition );
            moveType = uiManager.UILookAndColorConstants.Tooltip_Jump;
         } } catch ( Exception ex ) { Error( ex ); }

         if ( moveType != null )
            movement = FormatMeter( moveType, spareMove, maxMove ) + "\n";
      }

      private static float DeduceLockedAngle ( float spareMove, Pathing pathing, ref float maxMove ) {
         float start = pathing.lockedAngle, end = pathing.ResultAngle;
         if ( start == end ) return spareMove; // Not yet turned
         float left = start - end, right = end - start;
         if ( left < 0 ) left += 360;
         if ( right < 0 ) right += 360;
         float angle = Math.Min( left, right );
         float free = pathing.MoveType == MoveType.Walking ? 45f : 22.5f; // Free turning
         angle -= free;
         if ( angle <= 0 ) return spareMove; // Within free turning
         return spareMove - angle * pathing.MaxCost / ( 180 - free ) / 2; // Reversed from Pathint.GetAngleAvailable
      }

      private static string GetTargetNumbers ( ICombatant target ) { try {
         if ( HUD == null ) return null;
         if ( HUD.SelectedActor != null ) {
            float oldDist = Vector3.Distance( HUD.SelectedActor.CurrentPosition, target.CurrentPosition );
            Vector3 position = default;
            if      ( ActiveState is SelectionStateSprint sprint ) position = sprint.PreviewPos;
            else if ( ActiveState is SelectionStateMove move ) position = move.PreviewPos;
            else if ( ActiveState is SelectionStateJump jump ) position = jump.PreviewPos;
            else return string.Format( "Dist {0:0}", oldDist );
            float newDist = (int) Vector3.Distance( position, target.CurrentPosition );
            return FormatPrediction( "Dist", oldDist, newDist );
         }
         float min = float.MaxValue;
         foreach ( AbstractActor pc in Combat.LocalPlayerTeam.units ) {
            if ( pc.IsDead ) continue;
            float dist = Vector3.Distance( pc.CurrentPosition, target.CurrentPosition );
            if ( dist < min ) min = dist;
         }
         return string.Format( "Dist {0:0}", min );
      }                 catch ( Exception ex ) { Error( ex ); return null; } }

      private static bool needRefresh = false;
      public static void RecordRefresh () {
         needRefresh = true;
      }

      public static void RefreshTargetInfo ( bool isCallout ) { try {
         TargetDisplay?.RefreshInfo();
      }                 catch ( Exception ex ) { Error( ex ); } }

      public static void RefreshActorInfo ( CombatHUDMechTray __instance ) { try {
         if ( ! needRefresh ) return;
         __instance.ActorInfo?.DetailsDisplay?.RefreshInfo();
         needRefresh = false;
      }                 catch ( Exception ex ) { Error( ex ); } }

      public static void RefreshMoveAndDist () { try {
         HUD?.TargetingComputer?.ActorInfo?.DetailsDisplay?.RefreshInfo();
         HUD?.MechTray?.ActorInfo?.DetailsDisplay?.RefreshInfo();
      }                 catch ( Exception ex ) { Error( ex ); } }

      public static void CorrectProjectedHeat ( Mech __instance, ref int __result ) { try {
         if ( __instance.HasMovedThisRound || __instance != HUD?.SelectedActor ) return;
         SelectionState state = HUD?.SelectionHandler?.ActiveState;
         Vector3 position;
         if      ( state is SelectionStateSprint sprint ) position = sprint.PreviewPos;
         else if ( state is SelectionStateMove move ) position = move.PreviewPos;
         else if ( state is SelectionStateJump jump ) position = jump.PreviewPos;
         else return;

         // Get effect list from previewed movement path
         HashSet<DesignMaskDef> masks = new HashSet<DesignMaskDef>();
         List<Effect> ownEffects = null;
         DesignMaskDef local = __instance.occupiedDesignMask, preview = Combat.MapMetaData.GetPriorityDesignMaskAtPos( position );
         if ( preview != null ) masks.Add( preview );
         float here = local?.heatSinkMultiplier ?? 1, there = 1, extra = 0;
         if ( state is SelectionStateMove ) { // If walk or sprint, Check geothermal and radiation field.
            Pathing path = __instance.Pathing;
            masks.UnionWith( CombatHUDStatusPanel.GetStickyMasksForWaypoints( Combat,
               ActorMovementSequence.ExtractWaypointsFromPath( __instance, path.CurrentPath, path.ResultDestination, path.CurrentMeleeTarget, path.MoveType ) ) );
         }

         // Apply heat effect to prediction
         foreach ( DesignMaskDef mask in masks )
            there *= mask.heatSinkMultiplier;
         foreach ( DesignMaskDef mask in masks ) {
            extra += mask.heatPerTurn;
            if ( mask.stickyEffect != null && mask.stickyEffect.effectType == EffectType.StatisticEffect ) {
               StatisticEffectData effect = mask.stickyEffect.statisticData;
               if ( effect.statName != "EndMoveHeat" ) continue;
               if ( ownEffects == null ) {
                  ownEffects = Combat.EffectManager.GetAllEffectsTargeting( __instance );
                  ownEffects.Reverse(); // Put temp effects before permanent effects for quicker positive match
               }
               if ( ownEffects.Any( e => e.EffectData?.Description.Name == mask.stickyEffect.Description.Name ) ) continue; // Skip if sticky effect is already sticking
               switch ( effect.operation ) {
                  case StatCollection.StatOperation.Int_Add:
                     extra += (int) float.Parse( effect.modValue );
                     break;
                  default:
                     Warn( "Unexpected EndMoveHeat operation {0} on stick design mask {1}", effect.operation, mask.Description.Name );
                     break;
               }
            }
         }
         if ( here == there && extra == 0 ) return;
         __result = (int) ( ( ( (float) __result / here ) - extra ) * there );
      }                 catch ( Exception ex ) { Error( ex ); } }

      // ============ Mech Tray ============

      private static Dictionary<CombatHUDMWStatus, Pilot> PilotStatus;

      // Force update on portrait right click and forced unexpansion.
      public static void RefreshPilotHint ( CombatHUDMWStatus __instance ) { try {
         if ( PilotStatus.TryGetValue( __instance, out Pilot pilot ) )
            __instance.RefreshPilot( pilot );
      }                 catch ( Exception ex ) { Error( ex ); } }

      public static void ReplacePilotHint ( CombatHUDMWStatus __instance, Pilot pilot ) { try {
         if ( HUD == null || pilot == null ) return;
         if ( ! PilotStatus.ContainsKey( __instance ) )
            PilotStatus.Add( __instance, pilot );
         if ( ( HUD.SelectedActor != null || ! __instance.IsExpanded ) && ! pilot.IsIncapacitated )
            __instance.InjuriesItem?.ShowExistingIcon( new Text( Settings.ShortPilotHint, new object[]{
               pilot.Injuries, ( pilot.Health - pilot.Injuries ), pilot.Health, pilot.Gunnery, pilot.Piloting, pilot.Guts, pilot.Tactics } ),
               CombatHUDPortrait.GetPilotInjuryColor( pilot, HUD ) );
      }                 catch ( Exception ex ) { Error( ex ); } }

      public static void ColourPilotNames ( CombatHUDPortrait __instance ) { try {
         AbstractActor actor = __instance.DisplayedActor;
         if ( actor == null ) return;
         Color c = Color.white;
         if ( actor.WasEjected || actor.IsDead || ( actor.HasMovedThisRound && actor.HasFiredThisRound ) ) c = Color.grey;
         __instance.PilotName.color = c;
      }                 catch ( Exception ex ) { Error( ex ); } }
   }
}