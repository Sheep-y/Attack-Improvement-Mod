using BattleTech.UI;
using BattleTech;
using Localize;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Sheepy.BattleTechMod.AttackImprovementMod {
   using static Mod;
   using static System.Reflection.BindingFlags;

   public class UserInterface : BattleModModule {

      private static Color?[] NameplateColours = new Color?[ 3 ];
      private static Color?[] FloatingArmorColours = new Color?[ 3 ];

      public override void CombatStartsOnce () {
         NameplateColours = ParseColours( Settings.NameplateColourPlayer, Settings.NameplateColourEnemy, Settings.NameplateColourAlly );
         if ( Settings.ShowEnemyWounds != null || Settings.ShowAllyHealth != null || Settings.ShowPlayerHealth != null ) {
            Patch( typeof( CombatHUDActorNameDisplay ), "RefreshInfo", typeof( VisibilityLevel ), null, "ShowPilotWounds" );
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
         if ( Settings.ShowDangerousTerrain ) {
            SidePanelProp = typeof( MoveStatusPreview ).GetProperty( "sidePanel", NonPublic | Instance );
            if ( SidePanelProp == null )
               Warn( "MoveStatusPreview.sidePanel not found, ShowDangerousTerrain not patched." );
            else
               Patch( typeof( MoveStatusPreview ), "DisplayPreviewStatus", null, "AppendDangerousTerrainText" );
         }
         if ( Settings.ShowMeleeTerrain ) { // Rewrite with transpiler
            UpdateStatusMethod= typeof( CombatMovementReticle ).GetMethod( "UpdateStatusPreview", NonPublic | Instance );
            if ( SidePanelProp == null )
               Warn( "MoveStatusPreview.sidePanel not found, ShowDangerousTerrain not patched." );
            else
               Patch( typeof( CombatMovementReticle ), "DrawPath", null, "ShowMeleeTerrainText" );
         }

         if ( Settings.AltKeyFriendlyFire ) {
            Patch( typeof( AbstractActor ), "VisibilityToTargetUnit", "MakeFriendsVisible", null );
            Patch( typeof( CombatGameState ), "get_AllEnemies", "AddFriendsToEnemies", null );
            Patch( typeof( CombatGameState ), "GetAllTabTargets", null, "AddFriendsToTargets" );
            Patch( typeof( SelectionStateFire ), "CalcPossibleTargets", null, "AddFriendsToTargets" );
            Patch( typeof( SelectionStateFire ), "ProcessClickedCombatant", null, "SuppressHudSafety" );
            Patch( typeof( SelectionStateFire ), "get_ValidateInfoTargetAsFireTarget", null, "SuppressIFF" );
            Patch( typeof( CombatSelectionHandler ), "TrySelectTarget", null, "SuppressSafety" );
            Patch( typeof( CombatSelectionHandler ), "ProcessInput", "ToggleFriendlyFire", null );
         }

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
         bool AltPressed = Input.GetKey( KeyCode.LeftAlt ) || Input.GetKey( KeyCode.RightAlt );
         if ( FriendlyFire != AltPressed ) {
            FriendlyFire = AltPressed;
            foreach ( AbstractActor actor in Combat.LocalPlayerTeam.units )
               if ( ! actor.IsDead )
                  actor.VisibilityCache.RebuildCache( Combat.GetAllCombatants() );
            //__instance.ActiveState.ProcessMousePos( CameraControl.Instance.ScreenCenterToGroundPosition );
         }
      }                 catch ( Exception ex ) { Error( ex ); } }

      // ============ Floating Nameplate ============

      public static void ShowPilotWounds ( CombatHUDActorNameDisplay __instance, VisibilityLevel visLevel ) { try {
         AbstractActor actor = __instance.DisplayedCombatant as AbstractActor;
         Pilot pilot = actor?.GetPilot();
         Team team = actor?.team;
         if ( pilot == null || team == null ) return;
         string format = null;
         object[] args = new object[]{ __instance.PilotNameText.text, pilot.Injuries, pilot.Health - pilot.Injuries, pilot.Health };
         if ( team == Combat.LocalPlayerTeam ) {
            format = Settings.ShowPlayerHealth;
         } else if ( team.IsFriendly( Combat.LocalPlayerTeam ) ) {
            format = Settings.ShowAllyHealth;
         } else if ( visLevel == VisibilityLevel.LOSFull ) {
            format = Settings.ShowEnemyWounds;
            args = new object[]{ __instance.PilotNameText.text, pilot.Injuries, "?", "?" };
         }
         if ( format == null || pilot.Injuries <= 0 ) return;
         __instance.PilotNameText.SetText( args[0] + "</uppercase><size=80%>" + Translate( format, args ) );
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
         if ( team == null || owner.IsDead || Colours == null || Colours.Length < 3 ) return null;

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

      public static void CombatHUDLifeBarPips ( CombatHUDPipBar __instance, ref Color shownColor ) {
         if ( ! ( __instance is CombatHUDLifeBarPips me ) || ! BarOwners.TryGetValue( __instance, out ICombatant owner ) ) return;
         Color? colour = GetTeamColour( owner, FloatingArmorColours );
         if ( colour != null )
            shownColor = colour.GetValueOrDefault();
      }

      // ============ Others ============

      public static void FixMoveDestinationHeight ( Pathing __instance ) {
         __instance.ResultDestination.y = Combat.MapMetaData.GetLerpedHeightAt( __instance.ResultDestination );
      }

      private static PropertyInfo SidePanelProp;

      public static void AppendDangerousTerrainText ( MoveStatusPreview __instance, AbstractActor actor, Vector3 worldPos ) { try {
         MapTerrainDataCell cell = Combat.EncounterLayerData.GetCellAt( worldPos ).relatedTerrainCell;
         bool isLandingZone = SplatMapInfo.IsDropshipLandingZone( cell.terrainMask ), 
              isDangerous = SplatMapInfo.IsDangerousLocation( cell.terrainMask );
         if ( ! isLandingZone && ! isDangerous ) return;
         DesignMaskDef mask = Combat.MapMetaData.GetPriorityDesignMask( cell );
         if ( mask == null ) return;
         string title = mask.Description.Name, text = mask.Description.Details;
         CombatUIConstantsDef desc = Combat.Constants.CombatUIConstants;
         if ( isDangerous ) {
            title += " <#FF0000>(" + desc.DangerousLocationDesc.Name + ")";
            text += " <#FF0000>" + desc.DangerousLocationDesc.Details;
            if ( isLandingZone ) text += " " + desc.DrophipLocationDesc.Details;
         } else {
            title += " <#FF0000>(" + desc.DrophipLocationDesc.Name + ")";
            text += " <#FF0000>" + desc.DrophipLocationDesc.Details;
         }
         CombatHUDInfoSidePanel sidePanel = (CombatHUDInfoSidePanel) SidePanelProp?.GetValue( __instance, null );
         sidePanel?.ForceShowSingleFrame( new Text( title ), new Text( text ), null, false );
      }                 catch ( Exception ex ) { Error( ex ); } }

      private static MethodInfo UpdateStatusMethod;

      public static void ShowMeleeTerrainText ( CombatMovementReticle __instance, AbstractActor actor, bool isMelee, bool isTargetLocked, Vector3 mousePos, bool isBadTutorialPath ) { try {
         if ( ! isMelee ) return;
         Pathing pathing = actor.Pathing;
         if ( pathing == null || pathing.CurrentPath.IsNullOrEmpty() ) return;
         UpdateStatusMethod.Invoke( __instance, new object[]{ actor, pathing.ResultDestination + actor.HighestLOSPosition, pathing.MoveType } );
         // TODO: Update status preview text to melee
      }                 catch ( Exception ex ) { Error( ex ); } }
   }
}