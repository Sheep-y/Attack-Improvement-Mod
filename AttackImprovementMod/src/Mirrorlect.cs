using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Sheepy.CSharpUtils {
   using static System.Reflection.BindingFlags;

   public class Mirrorlect {

      public readonly List<Assembly> Assemblies = new List<Assembly>();
      public Action<string> WarningHandler = ( msg ) => Console.WriteLine( msg );

      public Mirrorlect () {
         Assemblies.Add( GetType().Assembly );
      }
      
      public interface IMemberProxy <T> {
         IMemberProxy<T> Of( object subject );
         T Get();
         void Set( T value );
      }

      public abstract class MemberProxy <T> : IMemberProxy <T> {
         public readonly MemberInfo Member;
         protected object subject;
         public MemberProxy ( MemberInfo info ) { Member = info; }
         public IMemberProxy<T> Of( object subject ) { 
            MemberProxy<T> cloned = (MemberProxy<T>) MemberwiseClone();
            cloned.subject = subject;
            return cloned;
         }
         public abstract T Get();
         public abstract void Set( T value );
      }

      public class FieldProxy <T> : MemberProxy <T> {
         public FieldInfo Field { get => (FieldInfo) Member; }
         public FieldProxy ( FieldInfo info ) : base( info ) {}
         public override T Get() { return (T) Field.GetValue( subject ); }
         public override void Set( T value ) { Field.SetValue( subject, value ); }
      }

      class MemberPart {
         public MemberPart Parent;
         public string MemberName;
         public MemberPart[] GenericTypes;
         public MemberPart[] Parameters;
         protected StringBuilder ToString ( StringBuilder buf ) {
            if ( Parent != null ) buf.Append( Parent ).Append( '.' );
            buf.Append( MemberName );
            if ( GenericTypes != null ) {
               buf.Append( '<' );
               for ( int i = 0, len = GenericTypes.Length ; i < len ; i++ ) {
                  if ( i > 0 ) buf.Append( ',' );
                  GenericTypes[ i ].ToString( buf );
               }
               buf.Append( '>' );
            }
            if ( Parameters != null ) {
               buf.Append( '(' );
               for ( int i = 0, len = Parameters.Length ; i < len ; i++ ) {
                  if ( i > 0 ) buf.Append( ',' );
                  Parameters[ i ].ToString( buf );
               }
               buf.Append( ')' );
            }
            return buf;
         }
         public override string ToString () {
            return ToString( new StringBuilder() ).ToString();
         }
      }

      public static Mirrorlect Mirror { get; } = new Mirrorlect();

      public Mirrorlect AddAssembly ( Type typeInAssembly ) { return AddAssembly( typeInAssembly.Assembly ); }
      public Mirrorlect AddAssembly ( Assembly assembly ) { lock( Assemblies ) {
         if ( ! Assemblies.Contains( assembly ) ) Assemblies.Add( assembly );
         return this;
      } }

      public MemberProxy<T> Find<T> ( string syntax ) {
         TextParser state = new TextParser( Regex.Replace( syntax, "\\s+", "" ) );
         MemberPart member = MatchMember( state );
         if ( state.IsEmpty ) return Reflect<T>( member );
         state.Take( '(' );
         throw new NotImplementedException( "Method parameter matching not implemented" );
      }

      private MemberPart MatchMember ( TextParser state ) {
         MemberPart lastMember = null;
         do {
            string fullpart = state.TakeTill( '.', '(' );
            if ( fullpart.Length <= 0 ) break;
            lastMember = new MemberPart(){ MemberName = fullpart, Parent = lastMember };
            if ( state.IsEmpty || state.Next == '(' ) break;
            state.Take( '.' );
         } while ( true );
         if ( lastMember == null ) state.Equals( "Identifier expected" );
         return lastMember;
      }

      private MemberProxy<T> Reflect<T> ( MemberPart member ) {
         if ( member.Parent == null ) throw new NotImplementedException( "Default parameter not implemented" );
         Type type = GetType( member.Parent );
         if ( type == null ) return null;
         MemberInfo[] info = type.GetMember( member.MemberName, Public | Instance | Static );
         if ( info == null || info.Length <= 0 ) info = type.GetMember( member.MemberName, NonPublic | Instance | Static );
         return new FieldProxy<T>( (FieldInfo) info[0] );
      }
 
      private Type GetType ( MemberPart member ) {
         foreach ( Assembly assembly in Assemblies ) {
            Type type = assembly.GetType( member.ToString() );
            if ( type != null ) return type;
         }
         return null;
      }
   }

   public class TextParser {
      public string original, text;
      public TextParser ( string txt ) { original = text = txt; if ( txt == null ) throw new ArgumentNullException(); }

      public char? Next { get => IsEmpty ? null : text?[0]; }
      public int Length { get => text.Length; }
      public bool IsEmpty { get => text.Length <= 0; }
      public string TakeTill ( params char[] chr ) {
         int pos = text.IndexOfAny( chr );
         if ( pos == 0 ) return "";
         return Consume( pos < 0 ? Length : pos );
      }
      public TextParser Take ( char chr ) {
         if ( IsEmpty || text[0] != chr ) Error( $"'{chr}' expected" );
         text = text.Substring( 1 );
         return this;
      }
      public void Error ( string message ) { throw new FormatException( message + " in " + original.Substring( 0, original.Length - text.Length ) + "λ" + text ); }

      private string Consume ( int len ) { // No length check
         string result = text.Substring( 0, len );
         text = text.Substring( len );
         return result;
      }
   }
}
