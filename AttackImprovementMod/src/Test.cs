using BattleTech;
using System;
using System.Reflection;
using System.Threading;

namespace Sheepy.AttackImprovementMod {
   /* Colllection of test code that I don't want to cluster the main code */
   class Test {

      static void Main () { // Sometimes I run quick tests as a console app here
         // runReverseRollTest();
         /**/
         foreach ( MemberInfo e in typeof( Team ).GetMembers( BindingFlags.NonPublic | BindingFlags.Instance ) )
            Console.WriteLine( e );
         FieldInfo m1 = typeof( Team ).GetField( "streakBreakingValue", BindingFlags.NonPublic | BindingFlags.Instance );
         Console.WriteLine( m1 ); /**/

         Console.ReadKey();
      }

      internal static void ListRollCorrection () {
         for ( int i = 0 ; i < 20 ; i++ ) {
            float roll = i * 0.05f;
            float rev1 = RollCorrection.ReverseRollCorrection( roll, 0.5f  ), rrev1 = RollCorrection.CorrectRoll( rev1, 0.5f ),
                  rev2 = RollCorrection.ReverseRollCorrection( roll, 1f    ), rrev2 = RollCorrection.CorrectRoll( rev2, 1f ),
                  rev3 = RollCorrection.ReverseRollCorrection( roll, 1.9999f), rrev3 = RollCorrection.CorrectRoll( rev3, 1.9999f );
            Console.WriteLine( string.Format( "{0:0.00} => [Half correction] {1:0.0000}, Re-rev {2:0.0000}   [Full] {3:0.0000}, Re-rev {4:0.0000}   [Double] {5:0.0000}, Re-rev {6:0.0000}",
               new object[]{ roll, rev1, rrev1, rev2, rrev2, rev3, rrev3 } ) );
         }
      }

      internal static void RunReverseRollTest () {
         new Thread( () => CheckReverseNaN(0) ).Start();
         new Thread( () => CheckReverseNaN(1) ).Start();
         new Thread( () => CheckReverseNaN(2) ).Start();
         new Thread( () => CheckReverseNaN(3) ).Start();
         new Thread( () => CheckReverseNaN(4) ).Start();
      }

      private static void CheckReverseNaN ( float count ) {
         float block = 2f/5f, add = count*block, max = 100000;
         for ( int i = 0 ; i <= max ; i++ ) {
            float strength = add + ((float)i)*block/max;
            if ( strength > 1.9999f ) continue;
            for ( int j = 0 ; j <= 10000 ; j++ ) {
               float acc = ((float)j)/10000f, corrected = RollCorrection.ReverseRollCorrection( acc, strength );
               if ( float.IsNaN( corrected ) ) Console.WriteLine( corrected + " = " + acc + ", " + strength );
            }
         }
      }

   }
}
