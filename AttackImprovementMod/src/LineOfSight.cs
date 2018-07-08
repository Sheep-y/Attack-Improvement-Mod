using System;

namespace Sheepy.AttackImprovementMod {
   using BattleTech;
   using BattleTech.UI;
   using System.Reflection;
   using UnityEngine;
   using static Mod;

   public class LineOfSight : ModModule {

      static ModSettings Settings;

      public override void InitPatch () {
         Settings = Mod.Settings;

         // Colours that fail to parse will be changed to empty string
         RangeCheck( "LOSWidthMultiplier", Settings.LOSWidthMultiplier, 0.1f, 10f );
         Parse( ref Settings.LOSMeleeColor );
         Parse( ref Settings.LOSClearColor );
         Parse( ref Settings.LOSBlockedColor );
         Parse( ref Settings.LOSIndirectColor );
         Parse( ref Settings.LOSNoAttackColor );

         bool LineChanged = Settings.LOSIndirectDotted || Settings.LOSMeleeColor != "" || Settings.LOSClearColor != "" || Settings.LOSBlockedColor != "" || Settings.LOSIndirectColor != "" || Settings.LOSNoAttackColor != "";
         if ( Settings.LOSWidthMultiplier != 1f || LineChanged )
            Patch( typeof( WeaponRangeIndicators ), "Init", null, "CreateNewTargettingLines" );
         if ( LineChanged )
            Patch( typeof( WeaponRangeIndicators ), "DrawLine", BindingFlags.NonPublic, "SetupTargettingLine", "CleanupTargettingLine" );
      }

      public static Color Parse ( ref string htmlColour ) {
         if ( htmlColour == "" ) return new Color();
         if ( ColorUtility.TryParseHtmlString( htmlColour, out Color result ) )
            return result;
         Error( "Cannot parse " + htmlColour + " as colour." );
         htmlColour = "";
         return new Color();
      }

      private static float OrigWidth = float.NaN;
      private static Material OrigInRangeMat;
      //private static Material OrigNoAttackMat;
      private static Color OrigLockedColour;
      private static Color OrigClearColour;
      private static Color OrigBlockedColour;
      //private static Color OrigNoAttackColour;

      private static Material MeleeMat;
      private static Material ClearMat;
      private static Material BlockedMat;
      private static Material IndirectMat;
      //private static Material NoAttackMat;

      public static void CreateNewTargettingLines ( WeaponRangeIndicators __instance ) {
         if ( ! float.IsNaN( OrigWidth ) ) return;
         WeaponRangeIndicators me = __instance;
         OrigWidth = me.LOSWidthBegin;

         if ( Settings.LOSWidthMultiplier != 1f ) {
            Log( "Scaling LOS" );
            float scale = Mod.Settings.LOSWidthMultiplier;
            me.LOSWidthBegin *= scale;
            me.LOSWidthEnd *= scale;
            me.LOSWidthBlocked *= scale;
         }

         OrigInRangeMat = me.MaterialInRange;
         //OrigNoAttackMat = me.MaterialOutOfRange;
         OrigLockedColour = me.LOSLockedTarget;
         OrigClearColour = me.LOSInRange;
         OrigBlockedColour = me.LOSBlocked;
         //OrigNoAttackColour = me.LOSOutOfRange;

         //Settings.LOSMeleeColor = Settings.LOSClearColor = Settings.LOSBlockedColor = Settings.LOSIndirectColor = "#F0FF";
         NewMat( me, "Melee", Settings.LOSMeleeColor, ref MeleeMat );
         NewMat( me, "Clear", Settings.LOSClearColor, ref ClearMat );
         NewMat( me, "Blocked", Settings.LOSBlockedColor, ref BlockedMat );
         if ( Settings.LOSIndirectColor != "" || Settings.LOSIndirectDotted ) {
            if ( Settings.LOSIndirectDotted ) {
               IndirectMat = new Material( me.MaterialOutOfRange );
               IndirectMat.SetColor( "_Color", me.LOSInRange );
               Log( "Indirect LOS is made dotted" );
            } else
               IndirectMat = new Material( me.MaterialInRange );
            if ( Settings.LOSIndirectColor != "" ) {
               IndirectMat.SetColor( "_Color", Parse( ref Settings.LOSIndirectColor ) );
               Log( "Indirect LOS Color " + Settings.LOSIndirectColor + " = " + IndirectMat.GetColor( "_Color" ) );
            }
         }
      }

      private static void NewMat ( WeaponRangeIndicators me, string name, string color, ref Material newMat ) {
         if ( color != "" ) {
            newMat = new Material( me.MaterialInRange );
            newMat.SetColor( "_Color", Parse( ref color ) );
            Log( name + " LOS Color " + color + " = " + newMat.GetColor( "_Color" ) );
         }
      }

      private static bool RestoreInRange = false;
      //private static bool RestoreNoAttack = false;

      public static void SetupTargettingLine ( WeaponRangeIndicators __instance, ICombatant target, bool usingMultifire, bool isMelee ) { try {
         if ( isMelee )
            SwapMat( __instance, MeleeMat, ref __instance.LOSLockedTarget );
         else if ( usingMultifire )
            return;
         else if ( IndirectMat != null || ClearMat != null || BlockedMat != null ) {
            FiringPreviewManager.PreviewInfo info = HUD.SelectionHandler.ActiveState.FiringPreview.GetPreviewInfo( target );
            if ( info.HasLOF )
               if ( info.LOFLevel == LineOfFireLevel.LOFClear )
                  SwapMat( __instance, ClearMat, ref __instance.LOSInRange );
               else
                  SwapMat( __instance, BlockedMat, ref __instance.LOSBlocked );
            else
               SwapMat( __instance, IndirectMat, ref __instance.LOSInRange );
         }
      } catch ( Exception ex ) { Log( ex ); } }

      private static void SwapMat ( WeaponRangeIndicators __instance, Material newMat, ref Color lineColor ) {
         if ( newMat == null ) return;
         __instance.MaterialInRange = newMat;
         lineColor = newMat.GetColor( "_Color" );
         RestoreInRange = true;
      }

      public static void CleanupTargettingLine ( WeaponRangeIndicators __instance ) {
         if ( RestoreInRange ) {
            __instance.MaterialInRange = OrigInRangeMat;
            __instance.LOSLockedTarget = OrigLockedColour;
            __instance.LOSInRange = OrigClearColour;
            __instance.LOSBlocked = OrigBlockedColour;
            RestoreInRange = false;
         }
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