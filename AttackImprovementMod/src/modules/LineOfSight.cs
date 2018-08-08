using BattleTech.UI;
using BattleTech;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using static System.Reflection.BindingFlags;

namespace Sheepy.BattleTechMod.AttackImprovementMod {
   using static Mod;

   public class LineOfSight : BattleModModule {

      public override void CombatStartsOnce () {
         if ( Settings.FacingMarkerPlayerColors != null || Settings.FacingMarkerEnemyColors != null || Settings.FacingMarkerTargetColors != null ) {
            SetColors = typeof( AttackDirectionIndicator ).GetMethod( "SetColors", NonPublic | Instance );
            if ( SetColors == null ) {
               Warn( "Cannot find AttackDirectionIndicator.SetColors, direction marker colors not patched." );
            } else {
               InitDirectionColors();
               Patch( typeof( AttackDirectionIndicator ), "ShowAttackDirection", "SaveDirectionMarker", "SetDirectionMarker" );
            }
         }

         if ( BattleMod.FoundMod( "com.joelmeador.BTMLColorLOSMod", "BTMLColorLOSMod.BTMLColorLOSMod" ) ) {
            BattleMod.BTML_LOG.Warn( Mod.Name + " detected joelmeador's BTMLColorLOSMod, LOS and arc styling disabled and left in the hands of BTMLColorLOSMod." );
            return;
         }
         InitSettings();

         bool LinesChanged = Settings.LOSIndirectDotted || parsedColor.ContainsKey( Line.Indirect ) ||
                                Settings.LOSMeleeDotted || parsedColor.ContainsKey( Line.Melee ) ||
                                Settings.LOSClearDotted || parsedColor.ContainsKey( Line.Clear ) ||
                           Settings.LOSBlockedPreDotted || parsedColor.ContainsKey( Line.BlockedPre ) ||
                          Settings.LOSBlockedPostDotted || parsedColor.ContainsKey( Line.BlockedPost ) ||
                           ! Settings.LOSNoAttackDotted || parsedColor.ContainsKey( Line.NoAttack );

         Type Indicator = typeof( WeaponRangeIndicators );

         if ( Settings.LOSWidth != 1f || Settings.LOSWidthBlocked != 0.75f || Settings.LOSMarkerBlockedMultiplier != 1f )
            Patch( Indicator, "Init", null, "ResizeLOS" );

         if ( LinesChanged ) {
            Patch( Indicator, "Init", null, "CreateLOSTexture" );
            Patch( Indicator, "DrawLine", NonPublic, "SetupLOS", "CleanupLOS" );
            Patch( Indicator, "ShowLineToTarget", NonPublic, null, "ShowBlockedLOS" );
         }
         if ( LinesChanged || Settings.ArcLinePoints != 18 )
            Patch( Indicator, "getLine" , NonPublic, null, "FixLOSWidth" );

         if ( Settings.ArcLinePoints != 18 ) {
            Patch( Indicator, "GetPointsForArc", Static, "OverrideGetPointsForArc", null );
            Patch( Indicator, "DrawLine", NonPublic, null, "SetIndirectSegments" );
            Patch( typeof( CombatPathLine ), "DrawJumpPath", null, "SetJumpPathSegments" );
         }
      }

      // ============ Marker Colour ============

      private static MethodInfo SetColors;
      private static Color[] OrigDirectionMarkerColors; // [ Active #FFFFFF4B, Inactive #F8441464 ]
      private static Color?[] FacingMarkerPlayerColors, FacingMarkerEnemyColors, FacingMarkerTargetColors;
      
      private static void InitDirectionColors () {
         FacingMarkerPlayerColors = new Color?[ LOSDirectionCount ];
         FacingMarkerEnemyColors  = new Color?[ LOSDirectionCount ];
         FacingMarkerTargetColors = new Color?[ LOSDirectionCount ];

         string[] player = Settings.FacingMarkerPlayerColors?.Split( ',' ).Select( e => e.Trim() ).ToArray();
         string[] enemy  = Settings.FacingMarkerEnemyColors ?.Split( ',' ).Select( e => e.Trim() ).ToArray();
         string[] active = Settings.FacingMarkerTargetColors?.Split( ',' ).Select( e => e.Trim() ).ToArray();

         for ( int i = 0 ; i < LOSDirectionCount ; i++ ) {
            if ( player != null && player.Length > i )
               FacingMarkerPlayerColors[ i ] = Parse( player[ i ] );
            if ( player != null && enemy.Length > i )
               FacingMarkerEnemyColors [ i ] = Parse( enemy [ i ] );
            if ( active != null && active.Length > i )
               FacingMarkerTargetColors[ i ] = Parse( active[ i ] );
         }
         Info( "Player directional marker = " + Join( ", ", FacingMarkerPlayerColors ) );
         Info( "Enemy  directional marker = " + Join( ", ", FacingMarkerEnemyColors  ) );
         Info( "Target directional marker = " + Join( ", ", FacingMarkerTargetColors ) );
      }

      public static void SaveDirectionMarker ( AttackDirectionIndicator __instance ) {
         if ( OrigDirectionMarkerColors == null )
            OrigDirectionMarkerColors = new Color[]{ __instance.ColorInactive, __instance.ColorActive };
      }

      public static void SetDirectionMarker ( AttackDirectionIndicator __instance, AttackDirection direction ) { try {
         AttackDirectionIndicator me =  __instance;
			if ( me.Owner.IsDead ) return;
         Color orig = me.ColorInactive;
         Color?[] activeColors = __instance.Owner?.team?.IsFriendly( Combat.LocalPlayerTeam ) ?? false ? FacingMarkerPlayerColors : FacingMarkerEnemyColors;
         object[] colors;
         if ( direction != AttackDirection.ToProne && direction != AttackDirection.FromTop ) {
            colors = new object[]{ activeColors?[0] ?? orig, activeColors?[1] ?? orig, activeColors?[2] ?? orig, activeColors?[3] ?? orig };
            if ( direction != AttackDirection.None ) {
               int dirIndex = Math.Max( 0, Math.Min( (int) direction - 1, LOSDirectionCount-1 ) );
               colors[ dirIndex ] = FacingMarkerTargetColors?[ dirIndex ] ?? me.ColorActive;
               //Log( $"Direction {direction}, Index {dirIndex}, Color {colors[ dirIndex ]}" );
            }
         } else {
            FiringPreviewManager.PreviewInfo info = HUD.SelectionHandler.ActiveState.FiringPreview.GetPreviewInfo( me.Owner );
            orig = info.HasLOF ? ( FacingMarkerTargetColors[4] ?? me.ColorActive ) : ( activeColors[4] ?? me.ColorInactive );
            colors = new object[]{ orig, orig, orig, orig };
         }
         SetColors.Invoke( __instance, colors );
      }                 catch ( Exception ex ) { Error( ex ); } }

      // ============ Line change ============

      private static bool losTextureScaled = false;

      public static void ResizeLOS ( WeaponRangeIndicators __instance ) { try {
         WeaponRangeIndicators me = __instance;

         float width = Settings.LOSWidth;
         if ( width > 0f && me.LOSWidthBegin != width ) {
            //Log( "Setting default LOS width to {0}", width );
            // Scale solid line width
            me.LOSWidthBegin = width;
            me.LOSWidthEnd = width;
            // Scale Out of Range line width, when line is solid
            me.LineTemplate.startWidth = width;
            me.LineTemplate.endWidth = width;
            // Scale all dotted lines
            if ( ! losTextureScaled ) {
               Vector2 s = me.MaterialOutOfRange.mainTextureScale;
               s.x /= width;
               me.MaterialOutOfRange.mainTextureScale = s;
            }
         }

         width = Settings.LOSWidthBlocked;
         if ( width > 0f && me.LOSWidthBlocked != width )
            me.LOSWidthBlocked = width;
         //Log( "LOS widths, normal = {0}, post-blocked = {1}", me.LOSWidthBegin, me.LOSWidthBlocked );

         width = Settings.LOSMarkerBlockedMultiplier;
         if ( width != 1f && ! losTextureScaled ) {
            //Log( "Scaling LOS block marker by {0}", width );
            Vector3 zoom = me.CoverTemplate.transform.localScale;
            zoom.x *= width;
            zoom.y *= width;
            me.CoverTemplate.transform.localScale = zoom;
         }
         losTextureScaled = true;
      }                 catch ( Exception ex ) { Error( ex ); } }

      private const int Melee = 0, Clear = 1, BlockedPre = 2, BlockedPost = 3, Indirect = 4, NoAttack = 5;
      internal enum Line { Melee = 0, Clear = 1, BlockedPre = 2, BlockedPost = 3, Indirect = 4, NoAttack = 5 }

      // Original materials and colours
      private static Material Solid, OrigInRangeMat, Dotted, OrigOutOfRangeMat;
      private static Color[] OrigColours;

      // Modded materials
      private static Dictionary<Line,Color?[]> parsedColor; // Exists until Mats are created. Each row and colour may be null.
      private static Material[][] Mats; // Replaces parsedColor. Either whole row is null or whole row is filled.
      internal const int LOSDirectionCount = 5;

      private static void InitSettings () {
         parsedColor = new Dictionary<Line, Color?[]>( LOSDirectionCount );
         foreach ( Line line in (Line[]) Enum.GetValues( typeof( Line ) ) ) {
            FieldInfo colorsField = typeof( ModSettings ).GetField( "LOS" + line + "Colors"  );
            string colorTxt = colorsField.GetValue( Settings )?.ToString();
            List<string> colorList = colorTxt?.Split( ',' ).Select( e => e.Trim() ).ToList();
            if ( colorList == null ) continue;
            Color?[] colors = new Color?[ LOSDirectionCount ];
            for ( int i = 0 ; i < LOSDirectionCount ; i++ )
               if ( colorList.Count > i )
                  colors[ i ] = Parse( colorList[ i ] );
            if ( colors.Any( e => e != null ) )
               parsedColor.Add( line, colors );
         }
      }

      public static void CreateLOSTexture ( WeaponRangeIndicators __instance ) { try {
         WeaponRangeIndicators me = __instance;
         if ( parsedColor != null ) {
            Solid = OrigInRangeMat = me.MaterialInRange;
            Dotted = OrigOutOfRangeMat = me.MaterialOutOfRange;
            OrigColours = new Color[]{ me.LOSInRange, me.LOSOutOfRange, me.LOSUnlockedTarget, me.LOSLockedTarget, me.LOSMultiTargetKBSelection, me.LOSBlocked };

            Mats = new Material[ NoAttack + 1 ][];
            foreach ( Line line in (Line[]) Enum.GetValues( typeof( Line ) ) )
               Mats[ (int) line ] = NewMat( line );

            // Make sure post mat is applied even if pre mat was not modified
            if ( Mats[ BlockedPost ] != null && Mats[ BlockedPre ] == null ) {
               Mats[ BlockedPre ] = new Material[ LOSDirectionCount ];
               for ( int i = 0 ; i < LOSDirectionCount ; i++ )
                  Mats[ BlockedPre ][i] = new Material( OrigInRangeMat ) { name = "BlockedPreLOS"+i };
            }
            parsedColor = null;
         }
      } catch ( Exception ex ) {
         Mats = new Material[ NoAttack + 1 ][]; // Reset all materials
         Error( ex );
      } }

      private static bool RestoreMat = false;
      private static LineRenderer thisLine;

      public static void FixLOSWidth ( LineRenderer __result, WeaponRangeIndicators __instance ) {
         thisLine = __result;
         // Reset line width to default to prevent blocked width from leaking to no attack width.
         thisLine.startWidth = __instance.LOSWidthBegin;
         thisLine.endWidth = __instance.LOSWidthEnd;
      }

      private static int lastDirIndex;

      public static void SetupLOS ( WeaponRangeIndicators __instance, Vector3 position, AbstractActor selectedActor, ICombatant target, bool usingMultifire, bool isMelee ) { try {
         WeaponRangeIndicators me = __instance;
         int dirIndex = Math.Max( 0, Math.Min( (int) Combat.HitLocation.GetAttackDirection( position, target ) - 1, LOSDirectionCount-1 ) );
         if ( dirIndex != lastDirIndex && Mats[ NoAttack ] != null ) {
            me.MaterialOutOfRange = Mats[ NoAttack ][ dirIndex ];
            me.LOSOutOfRange = Mats[ NoAttack ][ dirIndex ].color;
         }
         lastDirIndex = dirIndex;
         if ( isMelee )
            SwapMat( me, Melee, dirIndex, ref me.LOSLockedTarget, false );
         else {
            FiringPreviewManager.PreviewInfo info = HUD.SelectionHandler.ActiveState.FiringPreview.GetPreviewInfo( target );
            if ( info.HasLOF )
               if ( info.LOFLevel == LineOfFireLevel.LOFClear )
                  SwapMat( me, Clear, dirIndex, ref me.LOSInRange, usingMultifire );
               else {
                  if ( SwapMat( me, BlockedPre, dirIndex, ref me.LOSInRange, usingMultifire ) )
                     me.LOSBlocked = Mats[ BlockedPre ][ dirIndex ].color;
               }
            else
               SwapMat( me, Indirect, dirIndex, ref me.LOSInRange, usingMultifire );
         }
      }                 catch ( Exception ex ) { Error( ex ); } }

      public static void CleanupLOS ( WeaponRangeIndicators __instance, bool usingMultifire ) {
         //Log( "Mat = {0}, Width = {1}, Color = {2}", thisLine.material.name, thisLine.startWidth, thisLine.startColor );
         if ( thisLine.material.name.StartsWith( "BlockedPreLOS" ) ) {
            thisLine.material = Mats[ BlockedPost ][ lastDirIndex ];
            thisLine.startColor = thisLine.endColor = Mats[ BlockedPost ][ lastDirIndex ].color;
            //Log( "Swap to blocked post {0}, Width = {1}, Color = {2}", thisLine.material.name, thisLine.startWidth, thisLine.startColor );
         }
         if ( RestoreMat ) {
            WeaponRangeIndicators me = __instance;
            me.MaterialInRange = OrigInRangeMat;
            me.LOSInRange = OrigColours[0];
            if ( usingMultifire ) {
               me.LOSUnlockedTarget = OrigColours[2];
               me.LOSLockedTarget = OrigColours[3];
               me.LOSMultiTargetKBSelection = OrigColours[4];
            }
            me.LOSBlocked = OrigColours[5];
            RestoreMat = false;
         }
      }

      // Make sure Blocked LOS is displayed in single target mode.
      public static void ShowBlockedLOS () {
         thisLine?.gameObject?.SetActive( true );
      }

      // ============ UTILS ============

      public static Color? Parse ( string htmlColour ) {
         if ( htmlColour == "" || htmlColour == null ) return null;
         if ( ColorUtility.TryParseHtmlString( htmlColour, out Color result ) )
            return result;
         Error( "Cannot parse \"" + htmlColour + "\" as colour." );
         return null;
      }

      private static Material[] NewMat ( Line line ) {
         string name = line.ToString();
         parsedColor.TryGetValue( line, out Color?[] colors );
         bool dotted  = (bool) typeof( ModSettings ).GetField( "LOS" + name + "Dotted" ).GetValue( Settings );
         if ( colors == null ) {
            if ( dotted == ( name.StartsWith( "NoAttack" ) ) ) return null;
            colors = new Color?[ LOSDirectionCount ];
         }
         //Log( "NewMat " + line + " = " + Join( ",", colors ) );
         Material[] lineMats = new Material[ LOSDirectionCount ];
         for ( int i = 0 ; i < LOSDirectionCount ; i++ )
            lineMats[ i ] = NewMat( name + "LOS" + i, name.StartsWith( "NoAttack" ), colors[i], i > 0 ? colors[i-1] : null, dotted );
         return lineMats;
      }

      private static Material NewMat ( string name, bool origInRange, Color? color, Color? fallback, bool dotted ) { try {
         Color newColour;
         if ( color != null )
            newColour = color.GetValueOrDefault();
         else if ( fallback != null )
            newColour = fallback.GetValueOrDefault(); // Use last colour if null
         else
            newColour = origInRange ? OrigInRangeMat.color : OrigOutOfRangeMat.color; // Restore original colour if dotted/solid is reversed
         Material newMat = new Material( dotted ? Dotted : Solid ) { name = name, color = newColour };

         // Blocked Post scale need to be override if normal width is not same as blocked width
         float width = Settings.LOSWidthBlocked, origWidth = Settings.LOSWidth <= 0 ? 1 : Settings.LOSWidth;
         if ( name.StartsWith( "BlockedPost" ) && dotted && origWidth != width ) {
            Vector2 s = newMat.mainTextureScale;
            s.x *= origWidth / width;
            newMat.mainTextureScale = s;
         }
         //Log( "Created {0} {1}, Color {2} = {3}", newMat.name, dotted ? "Dotted":"Solid", color, newColour );
         return newMat;
      }                 catch ( Exception ex ) { Error( ex ); return null; } }

      private static bool SwapMat ( WeaponRangeIndicators __instance, int matIndex, int dirIndex, ref Color lineColor, bool IsMultifire ) {
         Material newMat = Mats[ matIndex ]?[ dirIndex ];
         if ( newMat == null ) return false;
         WeaponRangeIndicators me = __instance;
         me.MaterialInRange = newMat;
         lineColor = newMat.color;
         if ( IsMultifire ) {
            me.LOSUnlockedTarget = me.LOSLockedTarget = me.LOSMultiTargetKBSelection = lineColor;
            me.LOSUnlockedTarget.a *= 0.8f;
         }
         //Log( $"Swapped to {matIndex} {dirIndex} {newMat.name}" );
         return RestoreMat = true;
      }

      // ============ ARCS ============

      private static float thisArcHeight;
      private static readonly Vector3[] linePoints = new Vector3[18]; // Must be at least 18 for game to copy points, which we will override

      [ Harmony.HarmonyPriority( Harmony.Priority.Low ) ]
      public static bool OverrideGetPointsForArc ( ref Vector3[] __result, int numPoints, float minArcHeight, Vector3 begin, Vector3 end ) {
         if ( numPoints == 2 || numPoints == 18 ) {
            thisArcHeight = minArcHeight;
            linePoints[0] = begin;
            linePoints[1] = end;
            __result = linePoints; // Skip all calculations
            return false;
         }
         return true;
      }

      public static void SetIndirectSegments () {
         if ( thisLine.positionCount == 18 ) SetArc( thisLine );
      }

      public static void SetJumpPathSegments ( CombatPathLine __instance ) {
         SetArc( __instance.line );
      }

      private static void SetArc ( LineRenderer line ) {
         // Unfortunately re-calculate the points is the simplest course of mod
         line.positionCount = Settings.ArcLinePoints;
         line.SetPositions( WeaponRangeIndicators.GetPointsForArc( Settings.ArcLinePoints, thisArcHeight, linePoints[ 0 ], linePoints[ 1 ] ) );
      }

      /*
      LOSInRange = RGBA(1.000, 0.157, 0.157, 1.000) #FF2828FF
      LOSOutOfRange = RGBA(1.000, 1.000, 1.000, 0.275) #FFFFFF46
      LOSUnlockedTarget = RGBA(0.757, 0.004, 0.004, 0.666) #C00000AA
      LOSLockedTarget = RGBA(0.853, 0.004, 0.004, 1.000) #DA0000FF
      LOSMultiTargetKBSelection = RGBA(1.000, 0.322, 0.128, 1.000) #FF5221FF
      LOSBlocked = RGBA(0.853, 0.000, 0.000, 0.753) #DA0000C0
      LOSWidthBegin = 1
      LOSWidthEnd = 0.75
      LOSWidthBlocked = 0.4
      LOSWidthFacingTargetMultiplier = 2.5f
      */
   }
}