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

   public class HUD : BattleModModule {

      private static HUD instance;

      private static Color?[] NameplateColours = new Color?[ 3 ];
      private static Color?[] FloatingArmorColours = new Color?[ 3 ];

      public override void CombatStartsOnce () {
         instance = this;
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

         if ( Settings.ShowDangerousTerrain ) {
            SidePanelProp = typeof( MoveStatusPreview ).GetProperty( "sidePanel", NonPublic | Instance );
            if ( SidePanelProp == null )
               Warn( "MoveStatusPreview.sidePanel not found, ShowDangerousTerrain not patched." );
            else
               Patch( typeof( MoveStatusPreview ), "DisplayPreviewStatus", null, "AppendDangerousTerrainText" );
         }
         if ( Settings.ShowMeleeTerrain ) { // Considered transpiler but we'll only save one method call. Not worth trouble?
            UpdateStatusMethod = typeof( CombatMovementReticle ).GetMethod( "UpdateStatusPreview", NonPublic | Instance );
            StatusPreviewProp = typeof( CombatMovementReticle ).GetProperty( "StatusPreview", NonPublic | Instance );
            if ( AnyNull( UpdateStatusMethod, StatusPreviewProp ) )
               Warn( "CombatMovementReticle.UpdateStatusPreview or StatusPreview not found, ShowMeleeTerrain not patched." );
            else {
               Patch( typeof( CombatMovementReticle ), "DrawPath", null, "ShowMeleeTerrainText" );
               Patch( typeof( CombatMovementReticle ), "drawJumpPath", null, "ShowDFATerrainText" );
            }
         }
         if ( Settings.SpecialTerrainDotSize != 1 || Settings.NormalTerrainDotSize != 1 )
            Patch( typeof( MovementDotMgr.MovementDot ).GetConstructors()[0], null, "ScaleMovementDot" );
         if ( Settings.BoostTerrainDotColor )
            Patch( typeof( CombatMovementReticle ), "Awake", null, "ColourMovementDot" );

         if ( Settings.FunctionKeySelectPC )
            Combat.MessageCenter.AddSubscriber( MessageCenterMessageType.KeyPressedMessage, KeyPressed );
      }

      public override void CombatStarts () {
         if ( Settings.MovementPreviewRadius > 0 ) {
            MovementConstants con = CombatConstants.MoveConstants;
            con.ExperimentalHexRadius = Settings.MovementPreviewRadius;
            typeof( CombatGameConstants ).GetProperty( "MoveConstants" ).SetValue( CombatConstants, con, null );
         }
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

      // ============ Floating Nameplate ============

      public static bool IsCallout { get; private set; }
      private static List<Action<bool>> CalloutListener;

      public static void HookCalloutToggle ( Action<bool> listener ) {
         if ( CalloutListener == null ) {
            instance.Patch( typeof( CombatSelectionHandler ), "ProcessInput", null, "ToggleCallout" );
            CalloutListener = new List<Action<bool>>();
         }
         if ( ! CalloutListener.Contains( listener ) )
            CalloutListener.Add( listener );
      }

      public static void ToggleCallout () { try {
         if ( IsCallout != IsCalloutPressed ) {
            IsCallout = IsCalloutPressed;
            foreach ( Action<bool> listener in CalloutListener )
               listener( IsCallout );
         }
      }                 catch ( Exception ex ) { Error( ex ); } }

      // ============ Floating Nameplate ============

      public static void ShowPilotWounds ( CombatHUDActorNameDisplay __instance, VisibilityLevel visLevel ) { try {
         AbstractActor actor = __instance.DisplayedCombatant as AbstractActor;
         Pilot pilot = actor?.GetPilot();
         Team team = actor?.team;
         TMPro.TextMeshProUGUI textbox = __instance.PilotNameText;
         if ( AnyNull( pilot, team, textbox ) || pilot.Injuries <= 0 ) return;
         string format = null;
         object[] args = new object[]{ null, pilot.Injuries, pilot.Health - pilot.Injuries, pilot.Health };
         if ( team == Combat.LocalPlayerTeam ) {
            format = Settings.ShowPlayerHealth;
         } else if ( team.IsFriendly( Combat.LocalPlayerTeam ) ) {
            format = Settings.ShowAllyHealth;
         } else if ( visLevel == VisibilityLevel.LOSFull ) {
            format = Settings.ShowEnemyWounds;
            args[2] = args[3] = "?";
         }
         if ( format != null )
            textbox.text = textbox.text + "</uppercase><size=80%>" + Translate( format, args );
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

      // ============ Terrain ============

      private static PropertyInfo SidePanelProp, StatusPreviewProp;
      private static MethodInfo UpdateStatusMethod;

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

      public static void ShowMeleeTerrainText ( CombatMovementReticle __instance, AbstractActor actor, bool isMelee ) {
         if ( isMelee ) ShowTerrainText( __instance, actor, "Melee" );
      }

      public static void ShowDFATerrainText ( CombatMovementReticle __instance, AbstractActor actor, bool isMelee ) {
         if ( isMelee ) ShowTerrainText( __instance, actor, "DFA" );
      }

      public static void ShowTerrainText ( CombatMovementReticle __instance, AbstractActor actor, string action ) { try {
         Pathing pathing = actor.Pathing;
         if ( pathing == null || pathing.CurrentPath.IsNullOrEmpty() ) return;
         UpdateStatusMethod?.Invoke( __instance, new object[]{ actor, pathing.ResultDestination + actor.HighestLOSPosition, pathing.MoveType } );
         ( (MoveStatusPreview) StatusPreviewProp?.GetValue( __instance, null ) ).MoveTypeText.text = Translate( action );
      }                 catch ( Exception ex ) { Error( ex ); } }

      public static void ScaleMovementDot ( MovementDotMgr.DotType type, GameObject ___dotObject ) { try {
         float size = (float) ( type == MovementDotMgr.DotType.Normal ? Settings.NormalTerrainDotSize : Settings.SpecialTerrainDotSize );
         if ( size == 1 ) return;
         Vector3 scale = ___dotObject.transform.localScale;
         scale.x *= size;
         scale.y *= size;
         ___dotObject.transform.localScale = scale;
      }                 catch ( Exception ex ) { Error( ex ); } }

      public static void ColourMovementDot ( GameObject ___forestDotTemplate, GameObject ___waterDotTemplate, GameObject ___roughDotTemplate, GameObject ___roadDotTemplate, GameObject ___specialDotTemplate, GameObject ___dangerousDotTemplate ) { try {
         BrightenGameObject( ___forestDotTemplate );
         BrightenGameObject( ___waterDotTemplate );
         BrightenGameObject( ___roughDotTemplate );
         BrightenGameObject( ___roadDotTemplate );
         BrightenGameObject( ___specialDotTemplate );
         BrightenGameObject( ___dangerousDotTemplate );
      }                 catch ( Exception ex ) { Error( ex ); } }

      private static void BrightenGameObject ( GameObject obj ) {
         MeshRenderer mesh = TryGet( obj?.GetComponents<MeshRenderer>(), 0, null, "MovementDot MeshRenderer" );
         if ( mesh == null ) return;
         Color.RGBToHSV( mesh.sharedMaterial.color, out float H, out float S, out float V );
         mesh.sharedMaterial.color = Color.HSVToRGB( H, 1, 1 );
      }
   }
}