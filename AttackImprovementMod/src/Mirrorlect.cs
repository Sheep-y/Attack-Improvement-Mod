using Microsoft.CSharp;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Sheepy.Reflector {
   using static System.Diagnostics.SourceLevels;
   using static System.Reflection.BindingFlags;

   public class Mirrorlect {

      public volatile Action<SourceLevels,string,object[]> Logger = ( level, msg, args ) => {
         //if ( level <= Warning )
            Console.WriteLine( string.Format( msg, args ) );
      };

      // Config
      public volatile bool UseCache = true;

      // Data
      private readonly List<Assembly> Assemblies = new List<Assembly>();
      private readonly Dictionary<Assembly, HashSet<string>> Namespaces = new Dictionary<Assembly, HashSet<string>>();

      // Cache
      private static ReaderWriterLockSlim typeCacheLock   = new ReaderWriterLockSlim( LockRecursionPolicy.NoRecursion ),
                                          parserCacheLock = new ReaderWriterLockSlim( LockRecursionPolicy.NoRecursion );
      private static Dictionary<string,Type> typeCache = new Dictionary<string, Type>();
      private static Dictionary<string,WeakReference> parserCache = new Dictionary<string,WeakReference>();

      // ============ Construcor and Static access ============

      public Mirrorlect () : this( true ) { }

      public Mirrorlect ( bool loadCurrentAssembly ) {
         if ( loadCurrentAssembly ) AddAssembly( GetType().Assembly );
      }

      public static Mirrorlect Instance { get; } = new Mirrorlect();

      // ============ Instance API ============

      public Mirrorlect AddAssembly ( Type typeInAssembly, string[] namespaces = null ) { return AddAssembly( typeInAssembly.Assembly, namespaces ); }
      public Mirrorlect AddAssembly ( Assembly assembly, string[] namespaces = null ) {
         if ( assembly == null ) throw new ArgumentNullException();
         lock ( Assemblies ) {
            if ( ! Assemblies.Contains( assembly ) ) {
               Assemblies.Add( assembly );
               Log( Information, "Added assembly {0}", assembly );
            }
            if ( namespaces == null ) {
               namespaces = assembly.GetTypes().Select( e => e.Namespace ).ToArray();
               Log( Information, "Auto-adding all namespaces in assembly {0}", assembly );
            } else if ( namespaces.Length <= 0 )
               return this;
            else
               Log( Verbose, "Adding {1} namespaces to assembly {0}", assembly, namespaces );
            if ( ! Namespaces.ContainsKey( assembly ) ) Namespaces.Add( assembly, new HashSet<string>() );
            Namespaces[ assembly ].UnionWith( namespaces );
         }
         ClearCache();
         return this;
      }

      public static MemberProxy<T> Reflect<T> ( string member ) { return Instance.Get<T>( member ); }

      public MemberProxy<T> Get<T> ( string member ) {
         string normalised = NormaliseQuery( member );
         if ( CheckParserCache( normalised, out MemberInfo cached ) )
            return InfoToProxy<T>( cached );

         return ParseAndProcess( normalised, "Find", ( text, state ) => {
            MemberPart parsed = MatchMember( state );
            state.MustBeEmpty();
            MemberProxy<T> result = PartToProxy<T>( parsed );
            SaveCache( text, result?.Member );
            return result;
         } );
      }

      public MemberPart Parse<T> ( string member ) {
         return ParseAndProcess( NormaliseQuery( member ), "Parse", ( text, state ) => {
            MemberPart parsed = MatchMember( state );
            state.MustBeEmpty();
            return parsed;
         } );
      }

      // ============ Helpers ============

      public string NormaliseQuery ( string query ) {
         return Regex.Replace( query, "\\s+", "" );
      }

      private R ParseAndProcess<R> ( string input, string actionName, Func<string,TextParser,R> action ) { try {
         Log( ActivityTracing, "{0} {1}", actionName, input );
         TextParser state = new TextParser( input );
         R parsed = action( input, state );
         state.MustBeEmpty();
         return parsed;
      } catch ( Exception ex ) {
         Log( Error, "Cannot {0} {1}: {2}", actionName.ToLower(), input, ex );
         return default(R);
      } }

      private void WriteLock ( ReaderWriterLockSlim rwlock, Action action ) { 
         try {
            rwlock.EnterWriteLock();
            action();
         } finally {
            rwlock.ExitWriteLock();
         }
      }

      // ============ Caching System ============

      public Mirrorlect ClearCache () {
         WriteLock( typeCacheLock, typeCache.Clear );
         WriteLock( parserCacheLock, parserCache.Clear );
         return this;
      }

      private MemberInfo SaveCache ( string input, MemberInfo item ) {
         if ( item == null || ! UseCache ) return item;
         Log( Verbose, "Caching MemberInfo {0}", input );
         WriteLock( parserCacheLock, () => parserCache[ input ] = new WeakReference( item ) );
         return item;
      }

      private Type SaveCache ( string input, Type item ) {
         if ( item == null || ! UseCache ) return item;
         Log( Verbose, "Caching Type {0}", input );
         WriteLock( typeCacheLock, () => typeCache[ input ] = item );
         return item;
      }

      private bool CheckTypeCache ( string input, out Type result ) {
         result = null;
         try {
            typeCacheLock.EnterReadLock();
            typeCache.TryGetValue( input, out result );
         } finally {
            typeCacheLock.ExitReadLock();
         }
         if ( result != null ) Log( ActivityTracing, "Cache Hit Type: {0}", input );
         return result != null;
      }

      private bool CheckParserCache ( string input, out MemberInfo result ) {
         WeakReference pointer = null;
         result = null;
         try {
            parserCacheLock.EnterReadLock();
            if ( ! parserCache.TryGetValue( input, out pointer ) ) return false;
         } finally {
            parserCacheLock.ExitReadLock();
         }
         if ( pointer.Target == null ) return false;
         Log( ActivityTracing, "Cache Hit Member: {0}", input );
         result = (MemberInfo) pointer.Target;
         return result != null;
      }

      // ============ Internal Implementation ============

      private void Log( SourceLevels level, string message, params object[] args ) {
         Action<SourceLevels,string,object[]> actor = Logger;
         if ( actor == null ) return;
         try { actor( level, message, args ); } catch ( Exception ) { }
      }

      private MemberPart MatchMember ( TextParser state ) {
         MemberPart lastMember = null;
         do {
            string fullpart = state.TakeTill( '.', ',', '<', '(' );
            if ( fullpart.Length <= 0 ) break;
            lastMember = new MemberPart(){ MemberName = fullpart, Parent = lastMember };
            if ( state.Next == '<' ) lastMember.GenericTypes = MatchMemberList( '<', state, '>' )?.ToArray();
            if ( state.Next == '(' ) lastMember.Parameters = MatchMemberList( '(', state, ')' )?.ToArray();
            if ( state.IsEmpty || state.Next != '.' ) break;
            state.Advance();
         } while ( true );
         if ( lastMember == null ) state.Error( "Identifier expected" );
         return lastMember;
      }

      private List<MemberPart> MatchMemberList ( char prefix, TextParser state, char postfix ) {
         state.Take( prefix );
         List<MemberPart> result = MatchMemberList( state );
         state.Take( postfix );
         return result;
      }

      private List<MemberPart> MatchMemberList ( TextParser state ) {
         List<MemberPart> result = new List<MemberPart>();
         MemberPart nextMatch;
         do {
            nextMatch = MatchMember( state );
            if ( nextMatch == null ) break;
            result.Add( nextMatch );
            if ( state.Next != ',' ) break;
            state.Advance();
         } while ( true );
         return result.Count <= 0 ? null : result;
      }

      private MemberProxy<T> PartToProxy<T> ( MemberPart member ) {
         Type type = GetType( member.Parent ?? member );
         if ( type == null ) return null;
         MemberInfo[] info = type.GetMember( member.MemberName, Public | BindingFlags.Instance | Static );
         if ( info == null || info.Length <= 0 ) info = type.GetMember( member.MemberName, NonPublic | BindingFlags.Instance | Static );
         if ( info.Length > 1 ) Log( Warning, "Multiple matches ({1}) found for {0}", member, info.Length );
         return InfoToProxy<T>( info[ 0 ] );
      }

      private MemberProxy<T> InfoToProxy<T> ( MemberInfo member ) {
         switch ( member.MemberType ) {
         case MemberTypes.Field:
            return new FieldProxy<T>( (FieldInfo) member );
         case MemberTypes.Property:
            return new PropertyProxy<T>( (PropertyInfo) member );
         case MemberTypes.Method:
            return new MethodProxy<T>( (MethodInfo) member );
         default:
            throw new NotSupportedException( member.MemberType + " not implemented" );
         }
      }

      private Type GetType ( MemberPart member ) {
         string name = member.ToString();
         if ( CheckTypeCache( name, out Type result ) ) return result;
         return SaveCache( name, GetTypeNoCache( name ) );
      }

      private Type GetTypeNoCache ( string name ) {
         if ( name.IndexOf( '.' ) < 0 ) {
            if ( shortTypes.Count == 0 ) BuildTypeMap();
            if ( shortTypes.TryGetValue( name, out Type shortType ) ) return shortType;
         }
         if ( name.IndexOf( '.' ) > 0 )
            foreach ( Assembly assembly in Assemblies ) {
               Type type = assembly.GetType( name );
               if ( type != null ) return type;
            }
         foreach ( Assembly assembly in Assemblies ) {
            if ( ! Namespaces.TryGetValue( assembly, out HashSet<string> spaces ) ) continue;
            foreach ( string space in spaces ) {
               Type type = assembly.GetType( space + "." + name );
               if ( type != null ) return type;
            }
         }
         return null;
      }

      private static Dictionary<string, Type> shortTypes = new Dictionary<string, Type>();

      private static void BuildTypeMap () { lock( shortTypes ) {
         if ( shortTypes.Count > 0 ) return;
         using ( var provider = new CSharpCodeProvider() ) {
            Assembly mscorlib = Assembly.GetAssembly( typeof( int ) );
            foreach ( Type type in mscorlib.GetTypes() ) {
               if ( type.Namespace != "System" ) continue;
               var typeRef = new CodeTypeReference(type);
               var csTypeName = provider.GetTypeOutput(typeRef);
               if ( csTypeName.IndexOf( '.' ) >= 0 ) continue;
               shortTypes.Add( csTypeName, type );
            }
         }
      } }
   }

   public abstract class MemberProxy <T> {
      public readonly MemberInfo Member;
      protected object subject;
      public MemberProxy ( MemberInfo info ) { Member = info; }
      public MemberProxy<T> Of ( object subject ) {
         MemberProxy<T> cloned = (MemberProxy<T>) MemberwiseClone();
         cloned.subject = subject;
         return cloned;
      }
      public T Call ( params object[] parameters ) { return Get( parameters ); }
      public T Get ( params object[] index ) { return GetValue( subject, index ); }
      public void Set ( T value, params object[] index ) { SetValue( subject, value, index ); }
      public abstract T GetValue ( object subject, params object[] index );
      public abstract void SetValue ( object subject, T value, params object[] index );
      public override string ToString () {
         string connector = Member.DeclaringType != null ? "." : "";
         return GetType().Name + "(" + Member.DeclaringType + connector + Member + ")";
      }
   }

   public class FieldProxy <T> : MemberProxy <T> {
      public FieldInfo Field { get => (FieldInfo) Member; }
      public FieldProxy ( FieldInfo info ) : base( info ) {}
      public override T GetValue( object subject, params object[] index ) { return (T) Field.GetValue( subject ); }
      public override void SetValue( object subject, T value, params object[] index ) { Field.SetValue( subject, value ); }
   }

   public class PropertyProxy <T> : MemberProxy <T> {
      public PropertyInfo Property { get => (PropertyInfo) Member; }
      public PropertyProxy ( PropertyInfo info ) : base( info ) {}
      public override T GetValue( object subject, params object[] index ) { return (T) Property.GetValue( subject, index ); }
      public override void SetValue( object subject, T value, params object[] index ) { Property.SetValue( subject, value, index ); }
   }

   public class MethodProxy <T> : MemberProxy <T> {
      public MethodInfo Method { get => (MethodInfo) Member; }
      public MethodProxy ( MethodInfo info ) : base( info ) {}
      public override T GetValue( object subject, params object[] index ) { return (T) Method.Invoke( subject, index ); }
      public override void SetValue( object subject, T value, params object[] index ) { GetValue( subject, index ); }
   }

   public class MemberPart {
      private MemberPart _Parent;
      private string _MemberName, _ToString;
      private MemberPart[] _GenericTypes, _Parameters;

      public MemberPart Parent { get => _Parent; set { ClearToStringCache(); _Parent = value; } } // TODO: If we can fix Parent or MemberName we can simplify the code
      public string MemberName { get => _MemberName; set { ClearToStringCache(); _MemberName = value; } }
      public MemberPart[] GenericTypes { get => _GenericTypes; set { ClearToStringCache(); _GenericTypes = value; } }
      public MemberPart[] Parameters   { get => _Parameters  ; set { ClearToStringCache(); _Parameters = value; } }

      private void ClearToStringCache () { _ToString = null; }

      public StringBuilder ToString ( StringBuilder buffer ) {
         if ( Parent != null ) buffer.Append( Parent ).Append( '.' );
         if ( GenericTypes == null && Parameters == null ) return buffer.Append( _MemberName );
         if ( _ToString != null ) return buffer.Append( _ToString );
         StringBuilder buf = new StringBuilder();
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
         _ToString = buf.ToString();
         return buffer.Append( _ToString );
      }
      public override string ToString () {
         return ToString( new StringBuilder() ).ToString();
      }
   }

   public class TextParser {
      public string original, text;
      //public int pos = 0;
      public TextParser ( string txt ) { original = text = txt; if ( txt == null ) throw new ArgumentNullException(); }

      //public char? Prev { get => original != null && pos > 0 ? original[pos-1] : null; }
      public char? Next { get => IsEmpty ? null : text?[0]; }
      public int Length { get => text.Length; }
      public bool IsEmpty { get => text.Length <= 0; }
      public void MustBeEmpty() { if ( ! IsEmpty ) Unexpected(); }

      public string TakeTill ( params char[] chr ) {
         int pos = text.IndexOfAny( chr );
         if ( pos == 0 ) return string.Empty;
         return Consume( pos < 0 ? Length : pos );
      }
      public TextParser Take ( char chr ) {
         if ( IsEmpty || text[0] != chr ) Error( $"'{chr}' expected" );
         return Advance( 1 );
      }

      public FormatException Error ( string message ) { throw new FormatException( message + " in " + original.Substring( 0, original.Length - text.Length ) + "λ" + text ); }
      public FormatException Unexpected () { throw Error( $"Unexpected '{Next}'" ); }

      public string Consume ( int len ) { // No length check
         string result = text.Substring( 0, len );
         Advance( len );
         return result;
      }
      public TextParser Advance ( int len = 1 ) { // No length check
         text = text.Substring( len );
         //pos += len;
         return this;
      }
   }
}