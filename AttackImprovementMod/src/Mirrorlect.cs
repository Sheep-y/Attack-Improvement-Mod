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

      public MemberProxy<T> Get<T> ( string syntax ) { try {
         string normalised = Regex.Replace( syntax, "\\s+", "" );
         MemberInfo cached = CheckParserCache( normalised );
         if ( cached != null ) return InfoToProxy<T>( cached );

         TextParser state = new TextParser( normalised );
         MemberPart member = MatchMember( state );
         if ( ! state.IsEmpty ) {
            state.Take( '(' );
            throw new NotImplementedException( "Method parameter matching not implemented" );
         }
         MemberProxy<T> result = PartToProxy<T>( member );
         SaveCache( normalised, result.Member );
         return result;
      } catch ( Exception ex ) {
         Log( Error, "Cannot find {0}: {1}", syntax, ex );
         return null;
      } }

      public Type GetType ( string syntax ) { try {
         string normalised = Regex.Replace( syntax, "\\s+", "" );
         Type cached = CheckTypeCache( normalised );
         if ( cached != null ) return cached;

         TextParser state = new TextParser( normalised );
         MemberPart member = MatchMember( state );
         if ( state.IsEmpty ) return GetType( member );
         throw state.Error( $"Unexpected '{state.Next}'" );
      } catch ( Exception ex ) {
         Log( Error, "Cannot find {0}: {1}", syntax, ex );
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

      public MemberInfo SaveCache ( string input, MemberInfo item ) {
         if ( item == null || ! UseCache ) return item;
         Log( Verbose, "Caching parser result {0}", input );
         try {
            parserCacheLock.AcquireWriterLock( CacheTimeoutMS );
            parserCache[ input ] = new WeakReference( item );
         } finally {
            parserCacheLock.ReleaseLock();
         }
         return item;
      }

      public Type SaveCache ( string input, Type item ) {
         if ( item == null || ! UseCache ) return item;
         Log( Verbose, "Caching type result {0}", input );
         try {
            typeCacheLock.AcquireWriterLock( CacheTimeoutMS );
            typeCache[ input ] = item;
         } finally {
            typeCacheLock.ReleaseLock();
         }
         return item;
      }

      private Type CheckTypeCache ( string input ) {
         Type result = null;
         try {
            typeCacheLock.AcquireReaderLock( CacheTimeoutMS );
            typeCache.TryGetValue( input, out result );
         } finally {
            typeCacheLock.ReleaseLock();
         }
         if ( result != null ) Log( ActivityTracing, "Cache Hit Type: {0}", input );
         return result;
      }

      private MemberInfo CheckParserCache ( string input ) {
         WeakReference pointer = null;
         MemberInfo result = null;
         try {
            parserCacheLock.AcquireReaderLock( CacheTimeoutMS );
            if ( ! parserCache.TryGetValue( input, out pointer ) ) return null;
         } finally {
            parserCacheLock.ReleaseLock();
         }
         if ( pointer.Target == null ) return null;
         if ( result != null ) Log( ActivityTracing, "Cache Hit Member: {0}", input );
         return (MemberInfo) pointer.Target;
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
            string fullpart = state.TakeTill( '.', '(' );
            if ( fullpart.Length <= 0 ) break;
            lastMember = new MemberPart(){ MemberName = fullpart, Parent = lastMember };
            if ( state.IsEmpty || state.Next == '(' ) break;
            state.Take( '.' );
         } while ( true );
         if ( lastMember == null ) state.Error( "Identifier expected" );
         return lastMember;
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
         Type result = CheckTypeCache( name );
         if ( result != null ) return result;

         result = GetTypeNoCache( name );
         if ( result != null && UseCache ) lock( typeCache ) {
            typeCache.Add( name, result );
         }
         return result;
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
      public abstract T Get( params object[] index );
      public abstract T Call( params object[] parameters );
      public abstract void Set( T value, params object[] index );
      public override string ToString () {
         string connector = Member.DeclaringType != null ? "." : "";
         return GetType().Name + "(" + Member.DeclaringType + connector + Member + ")";
      }
   }

   public class FieldProxy <T> : MemberProxy <T> {
      public FieldInfo Field { get => (FieldInfo) Member; }
      public FieldProxy ( FieldInfo info ) : base( info ) {}
      public override T Get( params object[] index ) { return (T) Field.GetValue( subject ); }
      public override T Call( params object[] parameters ) { return Get( parameters ); }
      public override void Set( T value, params object[] index ) { Field.SetValue( subject, value ); }
   }

   public class PropertyProxy <T> : MemberProxy <T> {
      public PropertyInfo Property { get => (PropertyInfo) Member; }
      public PropertyProxy ( PropertyInfo info ) : base( info ) {}
      public override T Get( params object[] index ) { return (T) Property.GetValue( subject, index ); }
      public override T Call( params object[] parameters ) { return Get( parameters ); }
      public override void Set( T value, params object[] index ) { Property.SetValue( subject, value, index ); }
   }

   public class MethodProxy <T> : MemberProxy <T> {
      public MethodInfo Method { get => (MethodInfo) Member; }
      public MethodProxy ( MethodInfo info ) : base( info ) {}
      public override T Get( params object[] index ) { return Call( index ); }
      public override T Call( params object[] parameters ) {
         return (T) Method.Invoke( subject, parameters );
      }
      public override void Set( T value, params object[] index ) { Call( index ); }
   }

   internal class MemberPart {
      public MemberPart Parent;
      public string MemberName;
      public MemberPart[] GenericTypes;
      public MemberPart[] Parameters;

      public StringBuilder ToString ( StringBuilder buf ) {
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
      public FormatException Error ( string message ) { throw new FormatException( message + " in " + original.Substring( 0, original.Length - text.Length ) + "λ" + text ); }

      private string Consume ( int len ) { // No length check
         string result = text.Substring( 0, len );
         text = text.Substring( len );
         return result;
      }
   }
}
