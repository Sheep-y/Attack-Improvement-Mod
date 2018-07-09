using System;

namespace Sheepy.AttackImprovementMod {
   using BattleTech;
   using BattleTech.UI;
   using UnityEngine;
   using static System.Reflection.BindingFlags;
   using static Mod;

   public class LineOfSight : ModModule {

      static ModSettings Settings;

      public override void InitPatch () {
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

         bool LineChanged = Settings.LOSIndirectDotted || Settings.LOSIndirectColor != "" ||
                             ! Settings.LOSMeleeDotted || Settings.LOSMeleeColor != "" ||
                             ! Settings.LOSClearDotted || Settings.LOSClearColor != "" ||
                        ! Settings.LOSBlockedPreDotted || Settings.LOSBlockedPreColor != "" ||
                       ! Settings.LOSBlockedPostDotted || Settings.LOSBlockedPostColor != "" ;
         bool PrePostDiff = Settings.LOSBlockedPreDotted != Settings.LOSBlockedPostDotted || Settings.LOSBlockedPreColor != Settings.LOSBlockedPostColor;

         if ( Settings.LOSWidthMultiplier != 1f || Settings.LOSWidthBlockedMultiplier != 1f || Settings.LOSMarkerBlockedMultiplier != 1f )
            Patch( Indicator, "Init", null, "ResizeLOS" );
         if ( LineChanged || Settings.LOSNoAttackColor != "" || ! Settings.LOSNoAttackDotted )
            Patch( Indicator, "Init", null, "CreateNewLOS" );
         if ( Settings.ArcLinePoints != 18 || PrePostDiff )
            Patch( Indicator, "getLine" , NonPublic, null, "RecordLOS" );
         if ( PrePostDiff )
            Patch( Indicator, "DrawLine", NonPublic, null, "SetBlockedLOS" );
         if ( LineChanged )
            Patch( Indicator, "DrawLine", NonPublic, "SetupLOS", "CleanupLOS" );
         if ( Settings.ArcLinePoints != 18 ) {
            Patch( Indicator, "GetPointsForArc", Static, "RecordArcHeight", null );
            Patch( Indicator, "DrawLine", NonPublic, null, "SetIndirectSegments" );
            Patch( typeof( CombatPathLine ), "DrawJumpPath", null, "SetPathSegments" );
         }
      }

      // ============ Line change ============

      private static Material OrigInRangeMat;
      private static Material OrigOutOfRangeMat;
      private static Color OrigLockedColour;
      private static Color OrigClearColour;
      private static Color OrigBlockedColour;

      private static Material MeleeMat;
      private static Material ClearMat;
      private static Material BlockedPreMat;
      private static Material BlockedPostMat;
      private static Material IndirectMat;
      private static Material NoAttackMat;

      public static void ResizeLOS ( WeaponRangeIndicators __instance ) {
         WeaponRangeIndicators me = __instance;
         float scale = Settings.LOSWidthMultiplier;
         if ( scale != 1f ) {
            Log( "Scaling LOS width by " + scale );
            // Scale solid line width
            me.LOSWidthBegin *= scale;
            me.LOSWidthEnd *= scale;
            // Scale Out of Range line width, when line is solid
            me.LineTemplate.startWidth *= scale;
            me.LineTemplate.endWidth *= scale;
            // Scale dotted line, when line is solid
            Vector2 s = me.MaterialOutOfRange.mainTextureScale;
            s.x /= scale;
            me.MaterialOutOfRange.mainTextureScale = s;
         }
         scale = Settings.LOSWidthBlockedMultiplier;
         if ( scale != 1f ) {
            Log( "Scaling Blocked LOS width by " + scale );
            me.LOSWidthBlocked *= scale;
         }
         scale = Settings.LOSMarkerBlockedMultiplier;
         if ( scale != 1f ) {
            Log( "Scaling Blocked LOS Marker size by " + scale );
            Vector3 zoom = me.CoverTemplate.transform.localScale;
            zoom.x *= scale;
            zoom.y *= scale;
            me.CoverTemplate.transform.localScale = zoom;
         }
      }

      public static void CreateNewLOS ( WeaponRangeIndicators __instance ) {
         WeaponRangeIndicators me = __instance;
         OrigInRangeMat = me.MaterialInRange;
         OrigOutOfRangeMat = me.MaterialOutOfRange;
         OrigLockedColour = me.LOSLockedTarget;
         OrigClearColour = me.LOSInRange;
         OrigBlockedColour = me.LOSBlocked;

         MeleeMat = NewMat( "Melee", true, Settings.LOSMeleeColor, Settings.LOSMeleeDotted );
         ClearMat = NewMat( "Clear", true, Settings.LOSClearColor, Settings.LOSClearDotted );
         IndirectMat = NewMat( "Indirect" , true , Settings.LOSIndirectColor, Settings.LOSIndirectDotted );
         NoAttackMat = NewMat( "No Attack", false, Settings.LOSNoAttackColor, Settings.LOSNoAttackDotted );
         BlockedPreMat  = NewMat( "Blocked Pre" , true, Settings.LOSBlockedPreColor , Settings.LOSBlockedPreDotted  );
         BlockedPostMat = NewMat( "Blocked Post", true, Settings.LOSBlockedPostColor, Settings.LOSBlockedPostDotted );
         // Make sure post mat is applied even if pre mat was not modified
         if ( BlockedPostMat != null && BlockedPreMat == null ) {
            BlockedPreMat = new Material( OrigInRangeMat );
            BlockedPreMat.name = "Blocked_Pre_LOS";
         }
      }

      private static bool RestoreMat = false;
      private static LineRenderer thisLine;
      private static float thisArcHeight;

      public static void RecordLOS ( LineRenderer __result ) {
         thisLine = __result;
      }      

      public static void SetupLOS ( WeaponRangeIndicators __instance, ICombatant target, bool usingMultifire, bool isMelee ) { try {
         WeaponRangeIndicators me = __instance;
         if ( isMelee )
            SwapMat( me, MeleeMat, ref me.LOSLockedTarget );
         else if ( IndirectMat != null || ClearMat != null || BlockedPreMat != null || BlockedPostMat != null ) {
            FiringPreviewManager.PreviewInfo info = HUD.SelectionHandler.ActiveState.FiringPreview.GetPreviewInfo( target );
            if ( info.HasLOF )
               if ( info.LOFLevel == LineOfFireLevel.LOFClear )
                  SwapMat( me, ClearMat, ref me.LOSInRange );
               else {
                  SwapMat( me, BlockedPreMat, ref me.LOSInRange );
                  SwapMat( me, BlockedPreMat, ref me.LOSBlocked );
               }
            else
               SwapMat( me, IndirectMat, ref me.LOSInRange );
         }
      } catch ( Exception ex ) { Log( ex ); } }

      public static void CleanupLOS ( WeaponRangeIndicators __instance ) {
         if ( RestoreMat ) {
            __instance.MaterialInRange = OrigInRangeMat;
            __instance.LOSLockedTarget = OrigLockedColour;
            __instance.LOSInRange = OrigClearColour;
            __instance.LOSBlocked = OrigBlockedColour;
            RestoreMat = false;
         }
      }

      public static void SetBlockedLOS () { try {
         if ( thisLine.material.name.StartsWith( "Blocked_Pre_LOS" ) ) {
            thisLine.material = BlockedPostMat;
            thisLine.startColor = thisLine.endColor = BlockedPostMat.color;
         }
      } catch ( Exception ex ) { Log( ex ); } }

      public static void RecordArcHeight ( float minArcHeight ) {
         thisArcHeight = minArcHeight;
      }

      public static void SetIndirectSegments () {
         if ( thisLine.positionCount == 18 ) SetArc( thisLine );
      }

      public static void SetPathSegments ( CombatPathLine __instance ) {
         SetArc( __instance.line );
      }

      // ============ UTILS ============

      public static Color Parse ( ref string htmlColour ) {
         if ( htmlColour == "" ) return new Color();
         if ( ColorUtility.TryParseHtmlString( htmlColour, out Color result ) )
            return result;
         Error( "Cannot parse " + htmlColour + " as colour." );
         htmlColour = "";
         return new Color();
      }

      private static Material NewMat ( string name, bool origInRange, string color, bool dotted ) {
         if ( dotted != origInRange && color == "" ) return null;
         Material mat = new Material( dotted ? OrigOutOfRangeMat : OrigInRangeMat );
         Color newColour = Parse( ref color );
         if ( color == "" ) // Restore original colour since dotted/solid is reversed
            newColour = origInRange ? OrigInRangeMat.color : OrigOutOfRangeMat.color;
         mat.color = newColour;
         // Blocked Post scale need to be override if normal width is not same as blocked width
         if ( name == "Blocked Post" && dotted && Settings.LOSWidthMultiplier != Settings.LOSWidthBlockedMultiplier ) {
            Vector2 s = mat.mainTextureScale;
            s.x *= Settings.LOSWidthMultiplier / Settings.LOSWidthBlockedMultiplier;
            mat.mainTextureScale = s;
         }
         mat.name = name.Replace( ' ', '_' ) + "_LOS";
         Log( "{0} {1}, Color {2} = {3}", mat.name, dotted ? "Dotted":"Solid", color, newColour );
         return mat;
      }

      private static void SwapMat ( WeaponRangeIndicators __instance, Material newMat, ref Color lineColor ) {
         if ( newMat == null ) return;
         __instance.MaterialInRange = newMat;
         lineColor = newMat.color;
         RestoreMat = true;
      }

      private static void SetArc ( LineRenderer line ) {
         // Unfortunately re-calculate the points is the simplest course of mod
         line.positionCount = Settings.ArcLinePoints;
         line.SetPositions( WeaponRangeIndicators.GetPointsForArc( Settings.ArcLinePoints, thisArcHeight, line.GetPosition( 0 ), line.GetPosition( 17 ) ) );
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