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
         if ( level <= Warning ) Console.WriteLine( String.Format( msg, args ) );
      };

      public volatile bool UseCache = true;
      // All public field/prop locked to (this)
      public readonly List<Assembly> Assemblies = new List<Assembly>();
      public readonly Dictionary<Assembly, HashSet<string>> Namespaces = new Dictionary<Assembly, HashSet<string>>();

      // Caches
      private static ReaderWriterLock typeCacheLock = new ReaderWriterLock(), parserCacheLock = new ReaderWriterLock();
      private static Dictionary<string,Type> typeCache = new Dictionary<string, Type>();
      private static Dictionary<string,WeakReference> parserCache = new Dictionary<string,WeakReference>();
      private const int CacheTimeoutMS = 50;

      // ============ Public API ============

      public Mirrorlect () : this( true ) { }

      public Mirrorlect ( bool loadCurrentAssembly ) {
         if ( loadCurrentAssembly ) AddAssembly( GetType().Assembly );
      }

      public static Mirrorlect Instance { get; } = new Mirrorlect();

      public Mirrorlect AddAssembly ( Type typeInAssembly, string[] namespaces = null ) { return AddAssembly( typeInAssembly.Assembly, namespaces ); }
      public Mirrorlect AddAssembly ( Assembly assembly, string[] namespaces = null ) {
         if ( assembly == null ) throw new ArgumentNullException();
         lock ( this ) {
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

      public MemberProxy<T> Get<T> ( string member ) { try {
         string normalised = Regex.Replace( member, "\\s+", "" );
         if ( CheckParserCache( normalised, out MemberInfo cached ) )
            return InfoToProxy<T>( cached );

         Log( ActivityTracing, "Reflecting {0}", normalised );
         TextParser state = new TextParser( normalised );
         MemberPart parsed = MatchMember( state );
         if ( ! state.IsEmpty ) parsed.Parameters = MatchMemberList( '(', state, ')' )?.ToArray();
         MemberProxy<T> result = PartToProxy<T>( parsed );
         SaveCache( normalised, result?.Member );
         return result;
      } catch ( Exception ex ) {
         Log( Error, "Cannot find {0}: {1}", member, ex );
         return null;
      } }

      public Type GetType ( string member ) { try {
         string normalised = Regex.Replace( member, "\\s+", "" );
         if ( CheckTypeCache( normalised, out Type cached ) ) return cached;

         Log( ActivityTracing, "Finding Type {0}", normalised );
         TextParser state = new TextParser( normalised );
         MemberPart parsed = MatchMember( state );
         if ( state.IsEmpty ) return GetType( parsed );
         throw state.Error( $"Unexpected '{state.Next}'" );
      } catch ( Exception ex ) {
         Log( Error, "Cannot find {0}: {1}", member, ex );
         return null;
      } }

      // ============ Caching System ============

      public Mirrorlect ClearCache () {
         try {
            if ( typeCacheLock.IsReaderLockHeld ) typeCacheLock.ReleaseLock();
            typeCacheLock.AcquireWriterLock( int.MaxValue );
            typeCache.Clear();
         } finally {
            typeCacheLock.ReleaseLock();
         }
         try {
            if ( parserCacheLock.IsReaderLockHeld ) parserCacheLock.ReleaseLock();
            parserCacheLock.AcquireWriterLock( int.MaxValue );
            parserCache.Clear();
         } finally {
            parserCacheLock.ReleaseLock();
         }
         return this;
      }

      private MemberInfo SaveCache ( string input, MemberInfo item ) {
         if ( item == null || ! UseCache ) return item;
         Log( Verbose, "Caching MemberInfo {0}", input );
         try {
            parserCacheLock.AcquireWriterLock( CacheTimeoutMS );
            parserCache[ input ] = new WeakReference( item );
         } finally {
            parserCacheLock.ReleaseLock();
         }
         return item;
      }

      private Type SaveCache ( string input, Type item ) {
         if ( item == null || ! UseCache ) return item;
         Log( Verbose, "Caching Type {0}", input );
         try {
            typeCacheLock.AcquireWriterLock( CacheTimeoutMS );
            typeCache[ input ] = item;
         } finally {
            typeCacheLock.ReleaseLock();
         }
         return item;
      }

      private bool CheckTypeCache ( string input, out Type result ) {
         result = null;
         try {
            typeCacheLock.AcquireReaderLock( CacheTimeoutMS );
            typeCache.TryGetValue( input, out result );
         } finally {
            typeCacheLock.ReleaseLock();
         }
         if ( result != null ) Log( ActivityTracing, "Cache Hit Type: {0}", input );
         return result != null;
      }

      private bool CheckParserCache ( string input, out MemberInfo result ) {
         WeakReference pointer = null;
         result = null;
         try {
            parserCacheLock.AcquireReaderLock( CacheTimeoutMS );
            if ( ! parserCache.TryGetValue( input, out pointer ) ) return false;
         } finally {
            parserCacheLock.ReleaseLock();
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
            if ( state.IsEmpty || state.Next != '.' ) break;
            state.Advance();
         } while ( true );
         if ( lastMember == null ) state.Error( "Identifier expected" );
         return lastMember;
      }

      private List<MemberPart> MatchMemberList ( char prefix, TextParser state, char postfix ) {
         state.Take( '(' );
         List<MemberPart> result = MatchMemberList( state );
         state.Take( ')' );
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

   public interface IMemberProxy <T> {
      IMemberProxy<T> Of( object subject );
      T Get( params object[] index );
      T Call( params object[] parameters );
      void Set( T value, params object[] index );
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
      public T Call( params object[] parameters ) { return Get( parameters ); }
      public T Get( params object[] index ) { return GetValue( subject, index ); }
      public void Set( T value, params object[] index ) { SetValue( subject, value, index ); }
      public abstract T GetValue( object subject, params object[] index );
      public abstract void SetValue( object subject, T value, params object[] index );
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

   internal class MemberPart {
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
      public TextParser ( string txt ) { original = text = txt; if ( txt == null ) throw new ArgumentNullException(); }

      public char? Next { get => IsEmpty ? null : text?[0]; }
      public int Length { get => text.Length; }
      public bool IsEmpty { get => text.Length <= 0; }
      public string TakeTill ( params char[] chr ) {
         int pos = text.IndexOfAny( chr );
         if ( pos == 0 ) return String.Empty;
         return Consume( pos < 0 ? Length : pos );
      }
      public TextParser Take ( char chr ) {
         if ( IsEmpty || text[0] != chr ) Error( $"'{chr}' expected" );
         return Advance( 1 );
      }
      public FormatException Error ( string message ) { throw new FormatException( message + " in " + original.Substring( 0, original.Length - text.Length ) + "λ" + text ); }

      public string Consume ( int len ) { // No length check
         string result = text.Substring( 0, len );
         text = text.Substring( len );
         return result;
      }
      public TextParser Advance ( int len = 1 ) { // No length check
         text = text.Substring( len );
         return this;
      }
   }
}