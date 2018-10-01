using BattleTech;
using Harmony;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Sheepy.BattleTechMod.AttackImprovementMod {
   using static Mod;
   using static System.Reflection.BindingFlags;

   public class Melee : BattleModModule {

      public override void CombatStartsOnce () {
         Type PathingType = typeof( Pathing );
         if ( Settings.UnlockMeleePositioning )
            Patch( PathingType, "GetMeleeDestsForTarget", typeof( AbstractActor ), null, null, "UnlockMeleeDests" );

         if ( Settings.MaxMeleeVerticalOffsetByClass != null )
            if ( InitMaxVerticalOffset() ) {
               Patch( PathingType, "GetMeleeDestsForTarget", "SetMeleeTarget", "ClearMeleeTarget" );
               Patch( PathingType, "GetPathNodesForPoints", null, "CheckMeleeVerticalOffset" );
               Patch( typeof( JumpPathing ), "GetDFADestsForTarget", new Type[]{ typeof( AbstractActor ), typeof( List<AbstractActor> ) }, "SetDFATarget", "ClearMeleeTarget" );
               Patch( typeof( JumpPathing ), "GetPathNodesForPoints", null, "CheckMeleeVerticalOffset" );
            }
      }

      public override void CombatStarts () {
         if ( Settings.IncreaseMeleePositionChoice || Settings.IncreaseDFAPositionChoice || MaxMeleeVerticalOffsetByClass != null ) {
            MovementConstants con = CombatConstants.MoveConstants;
            if ( Settings.IncreaseMeleePositionChoice )
               con.NumMeleeDestinationChoices = 6;
            if ( Settings.IncreaseDFAPositionChoice )
               con.NumDFADestinationChoices = 6;
            if ( MaxMeleeVerticalOffsetByClass != null )
               con.MaxMeleeVerticalOffset = 1000;
            typeof( CombatGameConstants ).GetProperty( "MoveConstants" ).SetValue( CombatConstants, con, null );
         }
      }

      [ HarmonyPriority( Priority.Low ) ]
      public static IEnumerable<CodeInstruction> UnlockMeleeDests ( IEnumerable<CodeInstruction> input ) {
         return ReplaceIL( input,
            ( code ) => code.opcode.Name == "ldc.r4" && code.operand != null && code.operand.Equals( 10f ),
            ( code ) => { code.operand = 0f; return code; },
            1, "UnlockMeleePositioning", ModLog
            );
      }

      // ============ Vertical Offset ============

      private static float[] MaxMeleeVerticalOffsetByClass;
      private static AbstractActor thisMeleeAttacker, thisMeleeTarget;
      private static PropertyInfo JumpMechProp;

      private bool InitMaxVerticalOffset () {
         MaxMeleeVerticalOffsetByClass = null;
         JumpMechProp = typeof( JumpPathing ).GetProperty( "Mech", NonPublic | Instance );
         if ( JumpMechProp == null ) {
            Warn( "Can't find JumpPathing.Mech. MaxMeleeVerticalOffsetByClass not patched." );
            return false;
         }
         List<float> list = new List<float>();
         foreach ( string e in Settings.MaxMeleeVerticalOffsetByClass.Split( ',' ) ) try {
            if ( list.Count >= 4 ) break;
            float offset = float.Parse( e.Trim() );
            if ( offset < 0 || float.IsNaN( offset ) || float.IsInfinity( offset ) ) throw new ArgumentOutOfRangeException();
            list.Add( offset );
         } catch ( Exception ex ) {
            Warn( "Can't parse \'{0}\' in MaxMeleeVerticalOffsetByClass as a positive number: {1}", e, ex );
            list.Add( list.Count > 0 ? list[ list.Count-1 ] : 8 );
         }
         if ( list.Count <= 0 ) return false;
         while ( list.Count < 4 )
            list.Add( list[ list.Count-1 ] );
         if ( ! list.Exists( e => e != 4 ) ) return false;
         MaxMeleeVerticalOffsetByClass = list.ToArray();
         return true;
      }

      [ HarmonyPriority( Priority.High ) ]
      public static void SetMeleeTarget ( Pathing __instance, AbstractActor target ) {
         thisMeleeAttacker = __instance.OwningActor;
         thisMeleeTarget = target;
      }

      [ HarmonyPriority( Priority.High ) ]
      public static void SetDFATarget ( JumpPathing __instance, AbstractActor target ) {
         thisMeleeAttacker = JumpMechProp.GetValue( __instance, null ) as AbstractActor;
         thisMeleeTarget = target;
      }

      public static void ClearMeleeTarget () {
         thisMeleeAttacker = thisMeleeTarget = null;
      }

      // Set the game's MaxMeleeVerticalOffset to very high, then filter nodes at GetPathNodesForPoints
      public static void CheckMeleeVerticalOffset ( List<PathNode> __result ) { try {
         if ( thisMeleeTarget == null || __result.IsNullOrEmpty() ) return;
         //Verbo( "Checking {0} offsets", __result.Count );
         float targetY = thisMeleeTarget.CurrentPosition.y, maxY = 0;
         WeightClass lowerClass = 0;
			for (int i = __result.Count - 1 ; i >= 0 ; i-- ) {
            float attackerY = __result[ i ].Position.y;
            if ( attackerY > targetY )
               lowerClass = thisMeleeTarget is Mech mech ? mech.weightClass : WeightClass.LIGHT;
            else if ( targetY > attackerY )
               lowerClass = thisMeleeAttacker is Mech mech ? mech.weightClass : WeightClass.LIGHT;
            else
               continue;
            switch ( lowerClass ) {
               case WeightClass.LIGHT  : maxY = MaxMeleeVerticalOffsetByClass[0]; break;
               case WeightClass.MEDIUM : maxY = MaxMeleeVerticalOffsetByClass[1]; break;
               case WeightClass.HEAVY  : maxY = MaxMeleeVerticalOffsetByClass[2]; break;
               case WeightClass.ASSAULT: maxY = MaxMeleeVerticalOffsetByClass[3]; break;
            }
            //Verbo( "Offset {0}: class {1}, maxY {2}, diff {3}, attacker {4}, target {5}", i, lowerClass, maxY, attackerY - targetY, attackerY, targetY );
            if ( Math.Abs( attackerY - targetY ) > maxY )
               __result.RemoveAt( i );
         }
      }                 catch ( Exception ex ) { Error( ex ); } }
   }
}