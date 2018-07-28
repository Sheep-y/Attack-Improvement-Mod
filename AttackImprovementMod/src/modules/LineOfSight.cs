using BattleTech.UI;
using BattleTech;
using System;
using System.Linq;
using UnityEngine;
using static System.Reflection.BindingFlags;

namespace Sheepy.BattleTechMod.AttackImprovementMod {
   using static Mod;

   public class LineOfSight : BattleModModule {

      public override void CombatStartsOnce () {
         if ( BattleMod.FoundMod( "com.joelmeador.BTMLColorLOSMod", "BTMLColorLOSMod.BTMLColorLOSMod" ) ) {
            Logger.BTML_LOG.Warn( Mod.Name + " detected joelmeador's BTMLColorLOSMod, LOS and arc styling disabled and left in the hands of BTMLColorLOSMod." );
            return;
         }
         bool SolidLinesChanged = Settings.LOSIndirectDotted || Settings.LOSIndirectColor != null ||
                                     Settings.LOSMeleeDotted || Settings.LOSMeleeColor != null ||
                                     Settings.LOSClearDotted || Settings.LOSClearColor != null ||
                                Settings.LOSBlockedPreDotted || Settings.LOSBlockedPreColor != null ||
                               Settings.LOSBlockedPostDotted || Settings.LOSBlockedPostColor != null ; 
                                  // NoAttackLine is overriden once and leave alone.
         Type Indicator = typeof( WeaponRangeIndicators );

         bool TwoSectionsLOS = Settings.LOSBlockedPreDotted != Settings.LOSBlockedPostDotted || Settings.LOSBlockedPreColor != Settings.LOSBlockedPostColor;

         if ( Settings.LOSWidth != 1f || Settings.LOSWidthBlocked != 0.75f || Settings.LOSMarkerBlockedMultiplier != 1f )
            Patch( Indicator, "Init", null, "ResizeLOS" );
         if ( SolidLinesChanged || Settings.LOSNoAttackColor != null || ! Settings.LOSNoAttackDotted )
            Patch( Indicator, "Init", null, "CreateLOSTexture" );
         if ( Settings.ArcLinePoints != 18 || TwoSectionsLOS )
            Patch( Indicator, "getLine" , NonPublic, null, "RecordLOS" );
         if ( TwoSectionsLOS ) {
            Patch( Indicator, "DrawLine", NonPublic, null, "SetBlockedLOS" );
            Patch( Indicator, "ShowLineToTarget", NonPublic, null, "ShowBlockedLOS" );
         }
         if ( SolidLinesChanged )
            Patch( Indicator, "DrawLine", NonPublic, "SetupLOS", "CleanupLOS" );

         if ( Settings.ArcLinePoints != 18 ) {
            Patch( Indicator, "GetPointsForArc", Static, "OverrideGetPointsForArc", null );
            Patch( Indicator, "DrawLine", NonPublic, null, "SetIndirectSegments" );
            Patch( typeof( CombatPathLine ), "DrawJumpPath", null, "SetJumpPathSegments" );
         }
      }

      // ============ Line change ============

      private static bool losTextureScaled = false;

      public static void ResizeLOS ( WeaponRangeIndicators __instance ) { try {
         WeaponRangeIndicators me = __instance;

         float width = Settings.LOSWidth;
         if ( width > 0f && me.LOSWidthBegin != width ) {
            Log( "Setting default LOS width to {0}", width );
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
         Log( "LOS widths, normal = {0}, post-blocked = {1}", me.LOSWidthBegin, me.LOSWidthBlocked );

         width = Settings.LOSMarkerBlockedMultiplier;
         if ( width != 1f && ! losTextureScaled ) {
            Log( "Scaling LOS block marker by {0}", width );
            Vector3 zoom = me.CoverTemplate.transform.localScale;
            zoom.x *= width;
            zoom.y *= width;
            me.CoverTemplate.transform.localScale = zoom;
         }
         losTextureScaled = true;
      }                 catch ( Exception ex ) { Error( ex ); } }

      private const int Melee = 0, Clear = 1, BlockedPre = 2, BlockedPost = 3, Indirect = 4, NoAttack = 5;
      private enum Line { Melee = 0, Clear = 1, BlockedPre = 2, BlockedPost = 3, Indirect = 4, NoAttack = 5 }

      private static Material Solid, OrigInRangeMat, Dotted, OrigOutOfRangeMat;
      private static Color[] OrigColours;
      private static Material[] Mats;
      private static bool OverwriteNonMeleeLine = false;

      public static void CreateLOSTexture ( WeaponRangeIndicators __instance ) { try {
         WeaponRangeIndicators me = __instance;
         if ( Solid == null ) {
            Solid = OrigInRangeMat = me.MaterialInRange;
            Dotted = OrigOutOfRangeMat = me.MaterialOutOfRange;
            OrigColours = new Color[]{ me.LOSInRange, me.LOSOutOfRange, me.LOSUnlockedTarget, me.LOSLockedTarget, me.LOSMultiTargetKBSelection, me.LOSBlocked };

            Mats = new Material[ NoAttack + 1 ];
            foreach ( Line line in (Line[]) Enum.GetValues( typeof( Line ) ) )
               NewMat( line );

            // Make sure post mat is applied even if pre mat was not modified
            if ( Mats[ BlockedPost ] != null && Mats[ BlockedPre ] == null )
               Mats[ BlockedPre ] = new Material( OrigInRangeMat ) { name = "BlockedPreLOS" };

            OverwriteNonMeleeLine = Mats[ Indirect ] != null || Mats[ Clear ] != null || Mats[ BlockedPre ] != null;
         }
         if ( Mats[ NoAttack ] != null ) {
            me.MaterialOutOfRange = Mats[ NoAttack ];
            me.LOSOutOfRange = Mats[ NoAttack ].color;
         }
      } catch ( Exception ex ) {
         Mats = new Material[ NoAttack + 1 ]; // Reset all materials
         Error( ex );
      } }

      private static bool RestoreMat = false;
      private static LineRenderer thisLine;

      public static void RecordLOS ( LineRenderer __result, WeaponRangeIndicators __instance ) {
         thisLine = __result;
         // Reset line width to default to prevent blocked width from leaking to no attack width.
         thisLine.startWidth = __instance.LOSWidthBegin;
         thisLine.endWidth = __instance.LOSWidthEnd;
      }

      public static void SetupLOS ( WeaponRangeIndicators __instance, ICombatant target, bool usingMultifire, bool isMelee ) { try {
         WeaponRangeIndicators me = __instance;
         if ( isMelee )
            SwapMat( me, Melee, ref me.LOSLockedTarget, false );
         else if ( OverwriteNonMeleeLine ) {
            FiringPreviewManager.PreviewInfo info = HUD.SelectionHandler.ActiveState.FiringPreview.GetPreviewInfo( target );
            if ( info.HasLOF )
               if ( info.LOFLevel == LineOfFireLevel.LOFClear )
                  SwapMat( me, Clear, ref me.LOSInRange, usingMultifire );
               else {
                  SwapMat( me, BlockedPre, ref me.LOSInRange, usingMultifire );
                  me.LOSBlocked = Mats[ BlockedPre ].color;
               }
            else
               SwapMat( me, Indirect, ref me.LOSInRange, usingMultifire );
         }
      }                 catch ( Exception ex ) { Error( ex ); } }

      public static void CleanupLOS ( WeaponRangeIndicators __instance, bool usingMultifire ) {
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

      public static void SetBlockedLOS () { try {
         //Log( "Mat = {0}, Width = {1}, Color = {2}", thisLine.material.name, thisLine.startWidth, thisLine.startColor );
         if ( thisLine.material.name.StartsWith( "BlockedPreLOS" ) ) {
            thisLine.material = Mats[ BlockedPost ];
            thisLine.startColor = thisLine.endColor = Mats[ BlockedPost ].color;
            //Log( "Swap to blocked post {0}, Width = {1}, Color = {2}", thisLine.material.name, thisLine.startWidth, thisLine.startColor );
         }
      }                 catch ( Exception ex ) { Error( ex ); } }

      // Make sure Blocked LOS is displayed in single target mode.
      public static void ShowBlockedLOS () {
         thisLine?.gameObject?.SetActive( true );
      }

      // ============ UTILS ============

      public static Color Parse ( ref string htmlColour ) {
         if ( htmlColour == "" ) htmlColour = null;
         if ( htmlColour == null ) return new Color();
         if ( ColorUtility.TryParseHtmlString( htmlColour, out Color result ) )
            return result;
         Error( "Cannot parse \"" + htmlColour + "\" as colour." );
         htmlColour = null;
         return new Color();
      }

      private static void NewMat ( Line line ) {
         string name = line.ToString();
         string color = (string) typeof( ModSettings ).GetField( "LOS" + name + "Color"  ).GetValue( Settings );
         bool dotted  = (bool)   typeof( ModSettings ).GetField( "LOS" + name + "Dotted" ).GetValue( Settings );
         Mats[ (int) line ] = NewMat( name, name != "NoAttack", color, dotted );
      }

      private static Material NewMat ( string name, bool origInRange, string color, bool dotted ) { try {
         Material mat = new Material( dotted ? Dotted : Solid );
         Color newColour;
         if ( color != null )
            newColour = Parse( ref color );
         else
            newColour = origInRange ? OrigInRangeMat.color : OrigOutOfRangeMat.color; // Restore original colour if dotted/solid is reversed
         if ( dotted != origInRange && newColour == mat.color )
            return null; // Nothing changed. Skip.

         mat.color = newColour;
         // Blocked Post scale need to be override if normal width is not same as blocked width
         float width = Settings.LOSWidthBlocked, origWidth = Settings.LOSWidth <= 0 ? 1 : Settings.LOSWidth;
         if ( name == "BlockedPost" && dotted && origWidth != width && width > 0 ) {
            Vector2 s = mat.mainTextureScale;
            s.x *= origWidth / width;
            mat.mainTextureScale = s;
         }
         mat.name = name + "LOS";
         Log( "Created {0} {1}, Color {2} = {3}", mat.name, dotted ? "Dotted":"Solid", color, newColour );
         return mat;
      }                 catch ( Exception ex ) { Error( ex ); return null; } }

      private static void SwapMat ( WeaponRangeIndicators __instance, int matIndex, ref Color lineColor, bool IsMultifire ) {
         Material newMat = Mats[ matIndex ];
         if ( newMat != null ) {
            WeaponRangeIndicators me = __instance;
            me.MaterialInRange = newMat;
            lineColor = newMat.color;
            if ( IsMultifire ) {
               me.LOSUnlockedTarget = me.LOSLockedTarget = me.LOSMultiTargetKBSelection = lineColor;
               me.LOSUnlockedTarget.a *= 0.8f;
            }
            //Log( "Swapped to " + matIndex + " " + newMat.name );
            RestoreMat = true;
         }
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