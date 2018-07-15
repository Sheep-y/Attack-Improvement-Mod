using System;

namespace Sheepy.AttackImprovementMod {
   using BattleTech;
   using BattleTech.UI;
   using Sheepy.BattleTechMod;
   using UnityEngine;
   using static Mod;
   using static System.Reflection.BindingFlags;

   public class LineOfSight : ModModule {

      static ModSettings Settings;

      public override void Startup () {
         Settings = Mod.Settings;
         Type Indicator = typeof( WeaponRangeIndicators );

         // Colours that fail to parse will be changed to empty string
         RangeCheck( "LOSWidthMultiplier", ref Settings.LOSWidthMultiplier, 0.1f, 10f );
         RangeCheck( "LOSWidthBlockedMultiplier", ref Settings.LOSWidthBlockedMultiplier, 0.1f, 20f );
         RangeCheck( "LOSMarkerBlockedMultiplier", ref Settings.LOSMarkerBlockedMultiplier, 0f, 10f );
         RangeCheck( "ArcLineSegments", ref Settings.ArcLinePoints, 1, 1000 );
         Parse( ref Settings.LOSMeleeColor );
         Parse( ref Settings.LOSClearColor );
         Parse( ref Settings.LOSBlockedPreColor );
         Parse( ref Settings.LOSBlockedPostColor );
         Parse( ref Settings.LOSIndirectColor );
         Parse( ref Settings.LOSNoAttackColor );

         bool SolidLinesChanged = Settings.LOSIndirectDotted || Settings.LOSIndirectColor != null ||
                                   ! Settings.LOSMeleeDotted || Settings.LOSMeleeColor != null ||
                                   ! Settings.LOSClearDotted || Settings.LOSClearColor != null ||
                              ! Settings.LOSBlockedPreDotted || Settings.LOSBlockedPreColor != null ||
                             ! Settings.LOSBlockedPostDotted || Settings.LOSBlockedPostColor != null ; 
                                  // NoAttackLine is overriden once and leave alone.

         bool TwoSectionsLOS = Settings.LOSBlockedPreDotted != Settings.LOSBlockedPostDotted || Settings.LOSBlockedPreColor != Settings.LOSBlockedPostColor;

         if ( Settings.LOSWidthMultiplier != 1f || Settings.LOSWidthBlockedMultiplier != 1f || Settings.LOSMarkerBlockedMultiplier != 1f )
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

      private static float newScale = float.NaN;

      public static void ResizeLOS ( WeaponRangeIndicators __instance ) {
         WeaponRangeIndicators me = __instance;
         if ( me.LOSWidthBegin == newScale ) return;
         Log( "Scaling LOS width by {0} and {1}", Settings.LOSWidthMultiplier, Settings.LOSWidthBlockedMultiplier );

         float scale = Settings.LOSWidthMultiplier;
         if ( scale != 1f ) {
            // Scale solid line width
            me.LOSWidthBegin *= scale;
            me.LOSWidthEnd *= scale;
            newScale = me.LOSWidthBegin;
            // Scale Out of Range line width, when line is solid
            me.LineTemplate.startWidth *= scale;
            me.LineTemplate.endWidth *= scale;
            // Scale all dotted lines
            Vector2 s = me.MaterialOutOfRange.mainTextureScale;
            s.x /= scale;
            me.MaterialOutOfRange.mainTextureScale = s;
         }

         scale = Settings.LOSWidthBlockedMultiplier;
         if ( scale != 1f )
            me.LOSWidthBlocked *= scale;

         scale = Settings.LOSMarkerBlockedMultiplier;
         if ( scale != 1f ) {
            Vector3 zoom = me.CoverTemplate.transform.localScale;
            zoom.x *= scale;
            zoom.y *= scale;
            me.CoverTemplate.transform.localScale = zoom;
         }
      }

      private const int Melee = 0, Clear = 1, BlockedPre = 2, BlockedPost = 3, Indirect = 4, NoAttack = 5;
      private enum Line { Melee = 0, Clear = 1, BlockedPre = 2, BlockedPost = 3, Indirect = 4, NoAttack = 5 }

      private static Material Solid, OrigInRangeMat, Dotted, OrigOutOfRangeMat;
      private static Color[] OrigColours;
      private static Material[] Mats;
      private static bool OverwriteNonMeleeLine = false;

      public static void CreateLOSTexture ( WeaponRangeIndicators __instance ) {
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
      }

      private static bool RestoreMat = false;
      private static LineRenderer thisLine;

      public static void RecordLOS ( LineRenderer __result ) {
         thisLine = __result;
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
         //Log( "Mat is " + thisLine.material.name );
         if ( thisLine.material.name.StartsWith( "BlockedPreLOS" ) ) {
            thisLine.material = Mats[ BlockedPost ];
            thisLine.startColor = thisLine.endColor = Mats[ BlockedPost ].color;
            //Log( "Swap to blocked post " + thisLine.material.name );
         }
      }                 catch ( Exception ex ) { Error( ex ); } }

      public static void ShowBlockedLOS () {
         thisLine?.gameObject?.SetActive( true );
      }

      // ============ UTILS ============

      public static Color Parse ( ref string htmlColour ) {
         if ( htmlColour == "" ) htmlColour = null;
         if ( htmlColour == null ) return new Color();
         if ( ColorUtility.TryParseHtmlString( htmlColour, out Color result ) )
            return result;
         Error( "Cannot parse " + htmlColour + " as colour." );
         htmlColour = null;
         return new Color();
      }

      private static void NewMat ( Line line ) {
         string name = line.ToString();
         string color = (string) typeof( ModSettings ).GetField( "LOS" + name + "Color"  ).GetValue( Settings );
         bool dotted  = (bool)   typeof( ModSettings ).GetField( "LOS" + name + "Dotted" ).GetValue( Settings );
         Mats[ (int) line ] = NewMat( name, name != "NoAttack", color, dotted );
      }

      private static Material NewMat ( string name, bool origInRange, string color, bool dotted ) {
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
         if ( name == "BlockedPost" && dotted && Settings.LOSWidthMultiplier != Settings.LOSWidthBlockedMultiplier ) {
            Vector2 s = mat.mainTextureScale;
            s.x *= Settings.LOSWidthMultiplier / Settings.LOSWidthBlockedMultiplier;
            mat.mainTextureScale = s;
         }
         mat.name = name + "LOS";
         Log( "Created {0} {1}, Color {2} = {3}", mat.name, dotted ? "Dotted":"Solid", color, newColour );
         return mat;
      }

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
      private static readonly Vector3[] linePoints = new Vector3[18];

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