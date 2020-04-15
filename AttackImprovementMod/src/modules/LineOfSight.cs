﻿using BattleTech.UI;
using BattleTech;
using Harmony;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using static System.Reflection.BindingFlags;

namespace Sheepy.BattleTechMod.AttackImprovementMod {
    using HBS;
    using static Mod;

   public class LineOfSight : BattleModModule {

      private static float HueDeviation, BrightnessDeviation;
      private static bool LinesChanged, LinesAnimated;

      public override void CombatStartsOnce () {
         if ( Settings.FacingMarkerPlayerColors != null || Settings.FacingMarkerEnemyColors != null || Settings.FacingMarkerTargetColors != null ) {
            SetColors = typeof( AttackDirectionIndicator ).GetMethod( "SetColors", NonPublic | Instance );
            if ( SetColors == null ) {
               Warn( "Cannot find AttackDirectionIndicator.SetColors, direction marker colors not patched." );
            } else {
               TryRun( ModLog, InitDirectionColors );
               Patch( typeof( AttackDirectionIndicator ), "ShowAttackDirection", "SaveDirectionMarker", "SetDirectionMarker" );
            }
         }

         Type IndicatorType = typeof( WeaponRangeIndicators );
         if ( Settings.LOSMarkerBlockedMultiplier != 1 )
            Patch( IndicatorType, "Init", null, "ResizeLOS" );

         TryRun( ModLog, InitColours );
         LinesChanged = Settings.LOSIndirectDotted      || Settings.LOSMeleeDotted  || Settings.LOSBlockedPostDotted
                     || Settings.LOSBlockedPreDotted     || Settings.LOSClearDotted || ! Settings.LOSNoAttackDotted
                     || Settings.LOSWidthBlocked != 0.75m || Settings.LOSWidth != 1 || parsedColours.Count > 0;
         LinesAnimated = ( Settings.LOSHueDeviation != 0 && Settings.LOSHueHalfCycleMS > 0 )
                      || ( Settings.LOSBrightnessDeviation != 0 && Settings.LOSBrightnessHalfCycleMS > 0 );

         if ( LinesChanged || LinesAnimated ) {
            Patch( IndicatorType, "Init", null, "CreateLOSTexture" );
            if ( LinesAnimated ) {
               RGBtoHSV = new Dictionary<Color, Vector3>();
               Patch( IndicatorType, "ShowLinesToAllEnemies", "SetupLOSAnimation", null );
               Patch( IndicatorType, "ShowLineToTarget", "SetupLOSAnimation", null );
            }
            Patch( IndicatorType, "DrawLine", "SetupLOS", "SetBlockedLOS" );
            Patch( IndicatorType, "getLine" , null, "FixLOSWidth" );
            Patch( IndicatorType, "ShowLinesToAllEnemies", null, "ShowBlockedLOS" );
            Patch( IndicatorType, "ShowLineToTarget", null, "ShowBlockedLOS" );
         } else
            parsedColours = null;

         if ( Settings.ArcLinePoints != 18 ) {
            Patch( IndicatorType, "DrawLine", null, null, "ModifyArcPoints" );
            Patch( typeof( CombatPathLine ), "DrawJumpPath", null, null, "ModifyArcPoints" );
         }
      }

      // ============ Marker Colour ============

      private static MethodInfo SetColors;
      private static Color[] OrigDirectionMarkerColors; // [ Active #FFFFFF4B, Inactive #F8441464 ]
      private static Color?[] FacingMarkerPlayerColors, FacingMarkerEnemyColors, FacingMarkerTargetColors;

      private static void InitDirectionColors () { // TODO: Refactor
         FacingMarkerPlayerColors = new Color?[ LOSDirectionCount ];
         FacingMarkerEnemyColors  = new Color?[ LOSDirectionCount ];
         FacingMarkerTargetColors = new Color?[ LOSDirectionCount ];

         string[] player = Settings.FacingMarkerPlayerColors?.Split( ',' ).Select( e => e.Trim() ).ToArray();
         string[] enemy  = Settings.FacingMarkerEnemyColors ?.Split( ',' ).Select( e => e.Trim() ).ToArray();
         string[] active = Settings.FacingMarkerTargetColors?.Split( ',' ).Select( e => e.Trim() ).ToArray();

         for ( int i = 0 ; i < LOSDirectionCount ; i++ ) {
            if ( player != null && player.Length > i )
               FacingMarkerPlayerColors[ i ] = ParseColour( player[ i ] );
            if ( enemy  != null && enemy.Length > i )
               FacingMarkerEnemyColors [ i ] = ParseColour( enemy [ i ] );
            if ( active != null && active.Length > i )
               FacingMarkerTargetColors[ i ] = ParseColour( active[ i ] );
         }
         Info( "Player direction marker = {0}", FacingMarkerPlayerColors );
         Info( "Enemy  direction marker = {0}", FacingMarkerEnemyColors  );
         Info( "Target direction marker = {0}", FacingMarkerTargetColors );
      }

      public static void SaveDirectionMarker ( AttackDirectionIndicator __instance ) { try {
         if ( OrigDirectionMarkerColors == null )
            OrigDirectionMarkerColors = new Color[]{ __instance.ColorInactive, __instance.ColorActive };
      }                 catch ( Exception ex ) { Error( ex ); } }

      public static void SetDirectionMarker ( AttackDirectionIndicator __instance, AttackDirection direction ) { try {
         AttackDirectionIndicator me =  __instance;
         if ( me.Owner == null || me.Owner.IsDead || Combat == null ) return;
         Color orig = me.ColorInactive;
         Color?[] activeColors = __instance.Owner?.team?.IsFriendly( Combat?.LocalPlayerTeam ) ?? false ? FacingMarkerPlayerColors : FacingMarkerEnemyColors;
         object[] colors;
         if ( direction != AttackDirection.ToProne && direction != AttackDirection.FromTop ) {
            colors = new object[]{ activeColors?[0] ?? orig, activeColors?[1] ?? orig, activeColors?[2] ?? orig, activeColors?[3] ?? orig };
            if ( direction != AttackDirection.None ) {
               int dirIndex = Math.Max( 0, Math.Min( (int) direction - 1, LOSDirectionCount-1 ) );
               colors[ dirIndex ] = FacingMarkerTargetColors?[ dirIndex ] ?? me.ColorActive;
               //Log( $"Direction {direction}, Index {dirIndex}, Color {colors[ dirIndex ]}" );
            }
         } else {
            if ( ActiveState == null ) return;
            FiringPreviewManager.PreviewInfo info = ActiveState.FiringPreview.GetPreviewInfo( me.Owner );
            orig = info.HasLOF ? ( FacingMarkerTargetColors?[4] ?? me.ColorActive ) : ( activeColors?[4] ?? me.ColorInactive );
            colors = new object[]{ orig, orig, orig, orig };
         }
         SetColors.Invoke( __instance, colors );
      }                 catch ( Exception ex ) { Error( ex ); } }

      // ============ Los Setup ============

      private static bool losTextureScaled = false;

      public static void ResizeLOS ( WeaponRangeIndicators __instance ) { try {
         float size = (float) Settings.LOSMarkerBlockedMultiplier;
         if ( size != 1 && ! losTextureScaled ) {
            //Info( "Scaling LOS block marker by {0}", width );
            Vector3 zoom = __instance.CoverTemplate.transform.localScale;
            zoom.x *= size;
            zoom.y *= size;
            __instance.CoverTemplate.transform.localScale = zoom;
         }
         losTextureScaled = true;
      }                 catch ( Exception ex ) { Error( ex ); } }

      private const int Melee = 0, Clear = 1, BlockedPre = 2, BlockedPost = 3, Indirect = 4, NoAttack = 5;
      internal enum Line { Melee = 0, Clear = 1, BlockedPre = 2, BlockedPost = 3, Indirect = 4, NoAttack = 5 }

      // Original materials and colours
      private static Material Solid, OrigInRangeMat, Dotted, OrigOutOfRangeMat;
      private static Color[] OrigColours;

      // Modded materials
      private static Dictionary<Line,Color?[]> parsedColours; // Exists until Mats are created. Each row and colour may be null.
      private static Dictionary<Color, Vector3> RGBtoHSV; // For animation
      private static LosMaterial[][] Mats; // Replaces parsedColor. Either whole row is null or whole row is filled.
      internal const int LOSDirectionCount = 5;

      // Parse existing colours and leave the rest as null, called at CombatStartOnce
      private static void InitColours () {
         HueDeviation = (float) Settings.LOSHueDeviation;
         BrightnessDeviation = (float) Settings.LOSBrightnessDeviation;
         parsedColours = new Dictionary<Line, Color?[]>( LOSDirectionCount );
         foreach ( Line line in (Line[]) Enum.GetValues( typeof( Line ) ) ) {
            FieldInfo colorsField = typeof( ModSettings ).GetField( "LOS" + line + "Colors"  );
            string colorTxt = colorsField.GetValue( Settings )?.ToString();
            List<string> colorList = colorTxt?.Split( ',' ).Select( e => e.Trim() ).ToList();
            if ( colorList == null ) continue;
            Color?[] colors = new Color?[ LOSDirectionCount ];
            for ( int i = 0 ; i < LOSDirectionCount ; i++ )
               if ( colorList.Count > i )
                  colors[ i ] = ParseColour( colorList[ i ] );
            if ( colors.Any( e => e != null ) )
               parsedColours.Add( line, colors );
         }
      }

      // Fill in all parsedColours leaving no null, called after OrigColours is populated
      private static void FillColours () {
         foreach ( Line line in (Line[]) Enum.GetValues( typeof( Line ) ) ) {
            parsedColours.TryGetValue( line, out Color?[] colours );
            if ( colours == null ) parsedColours[ line ] = colours = new Color?[ LOSDirectionCount ];
            if ( colours[ 0 ] == null ) colours[ 0 ] = OrigColours[ line != Line.NoAttack ? 0 : 1 ];
            for ( int i = 0 ; i < LOSDirectionCount ; i++ ) {
               if ( colours[ i ] == null )
                  colours[ i ] = colours[ i - 1 ];
               if ( RGBtoHSV != null && colours[i].Value is Color c )
                  ToHSV( c ); // Cache HSV for animation
            }
            Info( "LOS {0} = {1}", line, colours );
         }
         if ( LinesAnimated )
            Info( "LOS animation, Hue = {0} @ {1}ms, Brightness = {2} @ {3}ms", HueDeviation, Settings.LOSHueHalfCycleMS, BrightnessDeviation, Settings.LOSBrightnessHalfCycleMS  );
         else
            Info( "LOS is not animated." );
      }

      private static Vector3 ToHSV ( Color c ) {
         if ( RGBtoHSV.TryGetValue( c, out Vector3 hsv ) ) return hsv;
         Color.RGBToHSV( c, out float H, out float S, out float V );
         return RGBtoHSV[ c ] = hsv = new Vector3( H, S, V );
      }

      public static void CreateLOSTexture ( WeaponRangeIndicators __instance ) { try {
         if ( parsedColours == null ) return;
         WeaponRangeIndicators me = __instance;
         Solid = OrigInRangeMat = me.MaterialInRange;
         Dotted = OrigOutOfRangeMat = me.MaterialOutOfRange;
         OrigColours = new Color[]{ me.FinalLOSInRange.color, me.FinalLOSOutOfRange.color, me.FinalLOSUnlockedTarget.color, me.FinalLOSLockedTarget.color, me.FinalLOSMultiTargetKBSelection.color, me.FinalLOSBlocked.color };

         FillColours();
         Mats = new LosMaterial[ NoAttack + 1 ][];
         foreach ( Line line in (Line[]) Enum.GetValues( typeof( Line ) ) )
            Mats[ (int) line ] = NewMat( line );
         parsedColours = null;
      } catch ( Exception ex ) {
         Mats = null;
         Error( ex );
      } }

      private static LosMaterial[] NewMat ( Line line ) {
         string name = line.ToString();
         Color?[] colors = parsedColours[ line ];
         bool dotted  = (bool) typeof( ModSettings ).GetField( "LOS" + name + "Dotted" ).GetValue( Settings );
         //Log( "LOS " + line + " = " + Join( ",", colors ) );
         LosMaterial[] lineMats = new LosMaterial[ LOSDirectionCount ];
         for ( int i = 0 ; i < LOSDirectionCount ; i++ ) {
            string matName = name + "LOS" + i;
            float width = (float) ( i != BlockedPost ? Settings.LOSWidth : Settings.LOSWidthBlocked );
            Color colour = colors[i] ?? OrigColours[ i != NoAttack ? 0 : 1 ];
            lineMats[ i ] = LinesAnimated
               ? new LosAnimated( colour, dotted, width, matName )
               : new LosMaterial( colour, dotted, width, matName );
         }
         return lineMats;
      }

      // ============ Los Material Swap ============

      private static float HueLerp, BrightnessLerp;
      private static int typeIndex, dirIndex;
      private static LineRenderer lineA, lineB;

      public static void FixLOSWidth ( LineRenderer __result ) {
         if ( lineA == null ) lineA = __result; else lineB = __result;
      }

      public static void SetupLOSAnimation () {
         int tick = Environment.TickCount & int.MaxValue;
         HueLerp = HueDeviation * Math.Abs( TickToCycle( tick, Settings.LOSHueHalfCycleMS ) );
         BrightnessLerp = TickToCycle( tick, Settings.LOSBrightnessHalfCycleMS );
      }

      public static void SetupLOS ( WeaponRangeIndicators __instance, Vector3 position, AbstractActor selectedActor, ICombatant target, bool usingMultifire, bool isMelee ) { try {
         if ( Mats == null ) return;
         WeaponRangeIndicators me = __instance;
         lineA = lineB = null;
         if ( LinesAnimated ) BrightnessLerp = AdvanceBrightness( 0.7f );

         typeIndex = dirIndex = 0;
         if ( target is Mech || target is Vehicle ) {
            bool canSee = selectedActor.HasLOSToTargetUnit( target );
            dirIndex = canSee ? Math.Max( 0, Math.Min( (int) Combat.HitLocation.GetAttackDirection( position, target ) - 1, LOSDirectionCount-1 ) ) : 0;
         }

         Mats[ NoAttack ][ dirIndex ].ApplyOutOfRange( me );
         if ( isMelee )
            typeIndex = Melee;
         else {
            FiringPreviewManager.PreviewInfo info = ActiveState.FiringPreview.GetPreviewInfo( target );
            if ( info.HasLOF )
               typeIndex = info.LOFLevel == LineOfFireLevel.LOFClear ? Clear : BlockedPre;
            else
               typeIndex = Indirect;
         }
         Mats[ typeIndex ][ dirIndex ].Apply( me, usingMultifire );
      }                 catch ( Exception ex ) { Error( ex ); } }

      // Return -1 to 1. Negative means heading towards zero, zero and positive means heading towards 1.
      private static float TickToCycle ( int tick, int cycle ) {
         if ( cycle == 0 ) return 0;
         return ( tick % ( cycle * 2 ) - cycle ) / (float) cycle;
      }

      private static float AdvanceBrightness ( float deviate ) {
         float value = BrightnessLerp + deviate;
         if ( value > 1 ) value -= 2;
         return value;
      }

      public static void SetBlockedLOS ( WeaponRangeIndicators __instance, bool usingMultifire ) { try {
         if ( lineB == null ) return;
         LosMaterial mat = Mats[ BlockedPost ][ dirIndex ];
         lineB.material = mat.GetMaterial();
         lineB.startColor = lineB.endColor = lineB.material.color;
         lineB.startWidth = lineB.endWidth = mat.Width;
         lineB.gameObject?.SetActive( true );
      }                 catch ( Exception ex ) { Error( ex ); } }

      // Make sure Blocked LOS is displayed in single target mode.
      public static void ShowBlockedLOS () { try {
         lineB?.gameObject?.SetActive( true );
      }                 catch ( NullReferenceException ) {
      }                 catch ( Exception ex ) { Error( ex ); } }

      // ============ Arcs ============

      public static IEnumerable<CodeInstruction> ModifyArcPoints ( IEnumerable<CodeInstruction> input ) {
         return ReplaceIL( input,
            ( code ) => code.opcode.Name == "ldc.i4.s" && code.operand != null && code.operand.Equals( (sbyte) 18 ),
            ( code ) => { code.operand = (sbyte) Settings.ArcLinePoints; return code; },
            2, "SetIndirectSegments", ModLog );
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

      public class LosMaterial {
         protected readonly Material Material;
         protected readonly Color Color;
         public readonly float Width;

         public LosMaterial ( Color color, bool dotted, float width, string name ) {
            Color = color;
            Width = width;
            Material = new Material( dotted ? Dotted : Solid ) { name = name, color = Color };
            if ( dotted && width != 1 ) {
               Vector2 s = Material.mainTextureScale;
               s.x *= 1 / width;
               Material.mainTextureScale = s;
            }
         }

         public LosMaterial Apply ( WeaponRangeIndicators me, bool IsMultifire ) {
            me.LOSWidthBegin = Width;
            me.LOSWidthEnd = Width;
            me.LineTemplate.startWidth = Width;
            me.LineTemplate.endWidth = Width;
            me.MaterialInRange = GetMaterial();
            UILookAndColorConstants colorConstants = LazySingletonBehavior<UIManager>.Instance.UILookAndColorConstants;
            colorConstants.LOSLockedTarget.color = me.MaterialInRange.color;
            LazySingletonBehavior<UIManager>.Instance.UILookAndColorConstants.LOSInRange.color = me.MaterialInRange.color;
                colorConstants.LOSLockedTarget.color = colorConstants.LOSInRange.color = me.MaterialInRange.color;
            if ( IsMultifire ) {
                    colorConstants.LOSUnlockedTarget.color = colorConstants.LOSLockedTarget.color = colorConstants.LOSMultiTargetKBSelection.color = me.MaterialInRange.color;
                    colorConstants.LOSUnlockedTarget.color.a *= 0.8f;
            }
            return this;
         }

         public void ApplyOutOfRange ( WeaponRangeIndicators me ) {
            me.MaterialOutOfRange = GetMaterial();
                LazySingletonBehavior<UIManager>.Instance.UILookAndColorConstants.LOSOutOfRange.color = me.MaterialOutOfRange.color;
         }

         public virtual Material GetMaterial () {
            return Material;
         }
      }

      public class LosAnimated : LosMaterial {
         public LosAnimated ( Color color, bool dotted, float width, string name )
                : base( color, dotted, width, name ) { }

         public override Material GetMaterial () {
            Vector3 HSV = ToHSV( Color ); // x = H, y = S, z = V
            float S = HSV.y, H = S > 0 ? ShiftHue( HSV.x, HueLerp ) : HSV.x, V = ShiftBrightness( HSV.z, BrightnessLerp, BrightnessDeviation );
            //Verbo( "Hue {0} => {1} ({5}), Sat {4}, Val {2} => {3} ({6})", HSV.x, H, HSV.z, V, S, HueLerp, ValueLerp );
            Material.color = Color.HSVToRGB( H, S, V );
            return Material;
         }

         private static float ShiftHue ( float val, float time ) {
            val += time;
            while ( val > 1 ) val -= 1;
            while ( val < 0 ) val += 1;
            return val;
         }

         private static float ShiftBrightness ( float val, float time, float devi ) {
            float max = val + devi, min = val - devi;
            if ( max > 1 ) {
               max = 1;
               min = 1 - devi - devi;
            } else if ( min < 0 ) {
               min = 0;
               max = min + devi + devi;
            }
            return Mathf.Lerp( min, max, Math.Abs( time ) );
         }
      }
   }
}