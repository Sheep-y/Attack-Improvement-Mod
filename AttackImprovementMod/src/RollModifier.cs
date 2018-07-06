using BattleTech;
using System;

namespace Sheepy.AttackImprovementMod {
   using static Mod;
   using System.Reflection;
   using UnityEngine;

   public class RollModifier : ModModule {

      static float[] diminishingBonus;
      static float[] diminishingPenalty;

      public override void InitPatch () {
         ModSettings Settings = Mod.Settings;
         if ( Settings.BaseHitChanceModifier != 0f )
            Patch( typeof( ToHit ), "GetUMChance", new Type[]{ typeof( float ), typeof( float ) }, "ModifyBaseHitChance", null );

         Settings.HitChanceStep = RangeCheck( "MaxFinalHitChance", Settings.HitChanceStep, 0f, 0.2f );
         Settings.MaxFinalHitChance = RangeCheck( "MaxFinalHitChance", Settings.MaxFinalHitChance, 0.1f, 1f );
         Settings.MinFinalHitChance = RangeCheck( "MinFinalHitChance", Settings.MinFinalHitChance, 0f, 1f );
         if ( Settings.HitChanceStep != 0.05f || Settings.MaxFinalHitChance != 0.95f || Settings.MinFinalHitChance != 0.05f || Settings.DiminishingHitChanceModifier ) {
            if ( ! Settings.DiminishingHitChanceModifier )
               Patch( typeof( ToHit ), "GetUMChance", new Type[]{ typeof( float ), typeof( float ) }, "OverrideHitChance", null );
            else {
               Patch( typeof( ToHit ), "GetUMChance", new Type[]{ typeof( float ), typeof( float ) }, "OverrideHitChanceDiminishing", null );
               diminishingBonus = new float[ Settings.DiminishingBonusMax ];
               diminishingPenalty = new float[ Settings.DiminishingPenaltyMax ];
               for ( int i = 1 ; i <= Settings.DiminishingBonusMax ; i++ )
                  diminishingBonus[ i-1 ] = (float) ( 2.0 - Math.Pow( Settings.DiminishingBonusPowerBase, (double) i / Settings.DiminishingBonusPowerDivisor ) );
               for ( int i = 1 ; i <= Settings.DiminishingPenaltyMax ; i++ )
                  diminishingPenalty[ i-1 ] = (float) Math.Pow( Settings.DiminishingPenaltyPowerBase, (double) i / Settings.DiminishingPenaltyPowerDivisor );
               Log( "Diminishing Bonus\t" + Join( "\t", diminishingBonus ) );
               Log( "Diminishing Penalty\t" + Join( "\t", diminishingPenalty ) );
            }
         }
      }

      public override void CombatStarts () {
         if ( Settings.AllowBonusHitChance ) {
            PropertyInfo res = typeof( CombatGameConstants ).GetProperty( "ResolutionConstants" );
            CombatResolutionConstantsDef conf = Constants.ResolutionConstants;
            conf.AllowTotalNegativeModifier = true;
            res.SetValue( Constants, conf, null );
         }
      }

      public static void ModifyBaseHitChance ( ref float baseChance ) {
         baseChance += Settings.BaseHitChanceModifier;
      }

      public static bool OverrideHitChance ( ToHit __instance, ref float __result, float baseChance, float totalModifiers ) {
         // A pretty intense routine that AI use to evaluate attacks, try catch disabled.
         baseChance = __instance.GetSteppedValue( baseChance, totalModifiers ) + 0.025f;
         if ( Settings.HitChanceStep > 0f )
			   baseChance -= baseChance % Settings.HitChanceStep;
         if      ( baseChance > Settings.MaxFinalHitChance ) baseChance = Settings.MaxFinalHitChance;
         else if ( baseChance < Settings.MinFinalHitChance ) baseChance = Settings.MinFinalHitChance;
         __result = baseChance;
         return false;
      }

      public static bool OverrideHitChanceDiminishing ( ToHit __instance, ref float __result, float baseChance, float totalModifiers ) { try {
         int mod = Mathf.RoundToInt( totalModifiers );
         if ( mod < 0 ) {
            mod = Math.Min( Settings.DiminishingBonusMax, -mod );
            baseChance *= diminishingBonus  [ mod -1 ];
         } else if ( mod > 0 ) {
            mod = Math.Min( Settings.DiminishingPenaltyMax, mod );
            baseChance *= diminishingPenalty[ mod -1 ];
         }
         if ( Settings.HitChanceStep > 0f )
			   baseChance -= baseChance % Settings.HitChanceStep;
         if      ( baseChance > Settings.MaxFinalHitChance ) baseChance = Settings.MaxFinalHitChance;
         else if ( baseChance < Settings.MinFinalHitChance ) baseChance = Settings.MinFinalHitChance;
         __result = baseChance;
         return false;
      }                 catch ( Exception ex ) { return Error( ex ); } }
   }
}