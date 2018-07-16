using Harmony;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using static System.Reflection.BindingFlags;

namespace Sheepy.BattleTechMod {

   public abstract class ModBase : ModModule {

      protected ModBase ( ) {
         SetupDefault();
      }

      public void Init ( ref Logger log ) {
         currentMod = this;
         TryRun( Setup );
         log = Logger;
         TryRun( log, Startup );
         currentMod = null;
      }

      public void Init () {
         currentMod = this;
         TryRun( Setup );
         TryRun( Startup );
         currentMod = null;
      }

      // Basic mod info for public access
      public string Id { get; protected set; } = "org.example";
      public string Name { get; protected set; } = "Nameless";
      public string Version { get; protected set; } = "Unknown";

      protected string BaseDir;

      private string _LogDir;
      protected string LogDir { 
         get { return _LogDir; }
         set {
            _LogDir = value;
            Logger = new Logger( GetLogFile() );
         }
      }
      internal HarmonyInstance harmony;

      // ============ Setup ============

      internal static ModBase currentMod;

      #pragma warning disable CS0649 // Disable "field never set" warnings since they are set by JsonConvert.
      private class ModInfo { public string Name;  public string Version; }

      // Fill in blanks with Assembly values
      private void SetupDefault () { TryRun( Logger, () => {
         Assembly file = GetType().Assembly;
         Id = GetType().Namespace;
         Name = file.GetName().Name;
         BaseDir = Path.GetDirectoryName( file.Location ) + "/"; 
         string mod_info_file = BaseDir + "mod.json";
         if ( File.Exists( mod_info_file ) ) TryRun( Logger, () => {
            ModInfo info = JsonConvert.DeserializeObject<ModInfo>( File.ReadAllText( mod_info_file ) );
            if ( ! string.IsNullOrEmpty( info.Name ) )
               Name = info.Name;
            if ( ! string.IsNullOrEmpty( info.Version ) )
               Version = info.Version;
         } );
         LogDir = BaseDir; // Create Logger after Name is read from mod.json
      } ); }

      // Override this method to override Namd and Id
      protected virtual void Setup () {
         Logger.Delete();
         Logger.Log( "{2} Loading {0} Version {1} In {3}\r\n", Name, Version, DateTime.Now.ToString( "s" ), BaseDir );
         harmony = HarmonyInstance.Create( Id );
      }

      protected virtual string GetLogFile () {
         return LogDir + "Log_" + Join( string.Empty, new Regex( "\\W+" ).Split( Name ), UppercaseFirst ) + ".txt";
      }

      // Load settings from settings.json, call SanitizeSettings, and create/overwrite it if the content is different.
      protected void LoadSettings <Settings> ( ref Settings settings, Func<Settings,Settings> sanitise = null ) {
         string file = BaseDir + "settings.json", fileText = "";
         Settings config = settings;
         if ( File.Exists( file ) ) TryRun( () => {
            fileText = File.ReadAllText( file );
            if ( fileText.Contains( "\"Name\"" ) && fileText.Contains( "\"DLL\"" ) && fileText.Contains( "\"Settings\"" ) ) TryRun( Logger, () => {
               JObject modInfo = JObject.Parse( fileText );
               if ( modInfo.TryGetValue( "Settings", out JToken embedded ) )
                  fileText = embedded.ToString( Formatting.None );
            } );
            config = JsonConvert.DeserializeObject<Settings>( fileText );
         } );
         if ( sanitise != null )
            TryRun( () => config = sanitise( config ) );
         string sanitised = JsonConvert.SerializeObject( config, Formatting.Indented, new JsonSerializerSettings { ContractResolver = new SkipObsoleteContractResolver() } );
         Logger.Log( "Loaded Settings: " + sanitised );
         if ( sanitised != fileText ) { // Can be triggered by comment or field update, not necessary sanitisation
            Logger.Log( "Updating " + file );
            SaveSettings( sanitised );
         }
         settings = config;
      }

      protected void SaveSettings ( Settings settings_object ) {
         SaveSettings( JsonConvert.SerializeObject( settings_object, Formatting.Indented ) );
      }

      private void SaveSettings ( string settings ) {
         TryRun( Logger, () => File.WriteAllText( BaseDir + "settings.json", settings ) );
      }
   }

   public abstract class ModModule {

      public abstract void Startup();
      public virtual void CombatStarts () { }

      protected ModBase mod { get; private set; }

      // ============ Harmony ============

      public ModModule () {
         if ( this is ModBase modbase )
            mod = modbase;
         else {
            mod = ModBase.currentMod;
            if ( mod == null )
               throw new ApplicationException( "ModModule must be created in ModBase.Setup()" );
            Logger = mod.Logger;
         }
      }
      
      private Logger logger;
      protected Logger Logger {
         get { return logger ?? Logger.BTML_LOG; }
         set { logger = value; }
      }

      // ============ Harmony ============

      /* Find and create a HarmonyMethod from this class. method must be public and has unique name. */
      protected HarmonyMethod MakePatch ( string method ) {
         if ( method == null ) return null;
         MethodInfo mi = GetType().GetMethod( method );
         if ( mi == null ) {
            Logger.Error( "Cannot find patch method " + method );
            return null;
         }
         return new HarmonyMethod( mi );
      }

      protected void Patch ( Type patchedClass, string patchedMethod, string prefix, string postfix ) {
         Patch( patchedClass, patchedMethod, Public | Instance, (Type[]) null, prefix, postfix );
      }

      protected void Patch ( Type patchedClass, string patchedMethod, Type parameterType, string prefix, string postfix ) {
         Patch( patchedClass, patchedMethod, Public | Instance, new Type[]{ parameterType }, prefix, postfix );
      }

      protected void Patch ( Type patchedClass, string patchedMethod, Type[] parameterTypes, string prefix, string postfix ) {
         Patch( patchedClass, patchedMethod, Public | Instance, parameterTypes, prefix, postfix );
      }

      protected void Patch ( Type patchedClass, string patchedMethod, BindingFlags flags, string prefix, string postfix ) {
         Patch( patchedClass, patchedMethod, flags, (Type[]) null, prefix, postfix );
      }

      protected void Patch ( Type patchedClass, string patchedMethod, BindingFlags flags, Type parameterType, string prefix, string postfix ) {
         Patch( patchedClass, patchedMethod, flags, new Type[]{ parameterType }, prefix, postfix );
      }

      protected void Patch ( Type patchedClass, string patchedMethod, BindingFlags flags, Type[] parameterTypes, string prefix, string postfix ) {
         MethodInfo patched;
         if ( ( flags & ( Static | Instance  ) ) == 0  ) flags |= Instance;
         if ( ( flags & ( Public | NonPublic ) ) == 0  ) flags |= Public;
         if ( parameterTypes == null )
            patched = patchedClass.GetMethod( patchedMethod, flags );
         else
            patched = patchedClass.GetMethod( patchedMethod, flags, null, parameterTypes, null );
         if ( patched == null ) {
            Logger.Error( "Cannot find {0}.{1}(...) to patch", patchedClass.Name, patchedMethod );
            return;
         }
         Patch( patched, prefix, postfix );
      }

      protected void Patch ( MethodInfo patched, string prefix, string postfix ) {
         if ( patched == null ) {
            Logger.Error( "Method not found. Cannot patch [ {0} : {1} ]", prefix, postfix );
            return;
         }
         HarmonyMethod pre = MakePatch( prefix ), post = MakePatch( postfix );
         if ( pre == null && post == null ) return; // MakePatch would have reported method not found
         if ( mod.harmony == null ) mod.Init();
         mod.harmony.Patch( patched, MakePatch( prefix ), MakePatch( postfix ) );
         Logger.Log( "Patched: {0} {1} [ {2} : {3} ]", patched.DeclaringType, patched, prefix, postfix );
      }

      // ============ UTILS ============
         
      public static string UppercaseFirst ( string s ) {
         if ( string.IsNullOrEmpty( s ) ) return string.Empty;
         return char.ToUpper( s[ 0 ] ) + s.Substring( 1 );
      }

      public static string Join<T> ( string separator, T[] array, Func<T,string> formatter = null ) {
         if ( array == null ) return string.Empty;
         StringBuilder result = new StringBuilder();
         for ( int i = 0, len = array.Length ; i < len ; i++ ) {
            if ( i > 0 ) result.Append( separator );
            result.Append( formatter == null ? array[i]?.ToString() : formatter( array[i] ) );
         }
         return result.ToString();
      }

      public static string NullIfEmpty ( ref string value ) {
         if ( value == null ) return null;
         if ( value.Trim().Length <= 0 ) return value = null;
         return value;
      }

      public static void TryRun ( Action action ) { TryRun( Logger.BTML_LOG, action ); }
      public static void TryRun ( Logger log, Action action ) { try {
         action.Invoke();
      } catch ( Exception ex ) { log.Error( ex ); } }

      public static T TryGet<T> ( T[] array, int index, T fallback = default(T), string errorArrayName = null ) {
         if ( array == null || array.Length <= index ) {
            if ( errorArrayName != null ) Logger.BTML_LOG.Warn( $"{errorArrayName}[{index}] not found, using default {fallback}." );
            return fallback;
         }
         return array[ index ];
      }

      public static V TryGet<T,V> ( Dictionary<T, V> map, T key, V fallback = default(V), string errorDictName = null ) {
         if ( map == null || ! map.ContainsKey( key ) ) {
            if ( errorDictName != null ) Logger.BTML_LOG.Warn( $"{errorDictName}[{key}] not found, using default {fallback}." );
            return fallback;
         }
         return map[ key ];
      }

      public static T ValueCheck<T> ( ref T value, T fallback = default(T), Func<T,bool> validate = null ) {
         if ( value == null ) value = fallback;
         else if ( validate != null && ! validate( value ) ) value = fallback;
         return value;
      }

      public static int RangeCheck ( string name, ref int val, int min, int max ) {
         float v = val;
         RangeCheck( name, ref v, min, min, max, max );
         return val = Mathf.RoundToInt( v );
      }

      public static float RangeCheck ( string name, ref float val, float min, float max ) {
         return RangeCheck( name, ref val, min, min, max, max );
      }

      public static float RangeCheck ( string name, ref float val, float shownMin, float realMin, float realMax, float shownMax ) {
         if ( realMin > realMax || shownMin > shownMax )
            Logger.BTML_LOG.Error( "Incorrect range check params on " + name );
         float orig = val;
         if ( val < realMin )
            val = realMin;
         else if ( val > realMax )
            val = realMax;
         if ( orig < shownMin && orig > shownMax ) {
            string message = "Warning: " + name + " must be ";
            if ( shownMin > float.MinValue )
               if ( shownMax < float.MaxValue )
                  message += " between " + shownMin + " and " + shownMax;
               else
                  message += " >= " + shownMin;
            else
               message += " <= " + shownMin;
            Logger.BTML_LOG.Log( message + ". Setting to " + val );
         }
         return val;
      }
   }

   public class Logger {

      public static readonly Logger BTML_LOG = new Logger( "Mods/BTModLoader.log" );

      public Logger ( string file ) {
         if ( String.IsNullOrEmpty( file ) ) throw new NullReferenceException();
         LogFile = file;
      }

      public string LogFile { get; protected set; }

      public bool IgnoreDuplicateExceptions = true;
      public Dictionary<string, int> exceptions = new Dictionary<string, int>();

      public bool Exists () {
         return File.Exists( LogFile );
      }

      public Exception Delete () {
         Exception result = null;
         try {
            File.Delete( LogFile );
         } catch ( Exception e ) { result = e; }
         return result;
      }

      public void Log ( object message ) {
         string txt = message.ToString();
         if ( message is Exception ex ) {
            if ( exceptions.ContainsKey( txt ) ) {
               exceptions[ txt ]++;
               if ( IgnoreDuplicateExceptions )
                  return;
            } else
               exceptions.Add( txt, 1 );
         }
         Log( txt ); 
      }
      public void Log ( string message, params object[] args ) { Log( Format( message, args ) ); }
      public void Log ( string message ) { WriteLog( message + "\r\n" ); }

      public void Warn ( object message ) { Warn( message.ToString() ); }
      public void Warn ( string message ) { Log( "Warning: " + message ); }
      public void Warn ( string message, params object[] args ) {
         message = Format( message, args );
         HBS.Logging.Logger.GetLogger( "Mods" ).LogWarning( "[AttackImprovementMod] " + message );
         Log( "Warning: " + message );
      }

      public bool Error ( object message ) { 
         if ( message is Exception )
            Log( message );
         else
            Error( message.ToString() );
         return true;
      }
      public void Error ( string message ) { Log( "Error: " + message ); }
      public void Error ( string message, params object[] args ) {
         message = Format( message, args );
         Log( "Error: " + message ); 
      }

      protected void WriteLog ( string message ) {
         try {
            File.AppendAllText( LogFile, message );
         } catch ( Exception ex ) {
            Console.WriteLine( message );
            Console.Error.WriteLine( ex );
         }
      }

      protected static string Format ( string message, params object[] args ) {
         try {
            if ( args != null && args.Length > 0 )
               return string.Format( message, args );
         } catch ( Exception ) {}
         return message;
      }
   }

   public class Setting : Attribute {
      public string Section;
      public string Comment;

      public Setting() { /* An empty setting */ }
      public Setting( string comment ) {
         Comment = comment;
      }
      public Setting( string section, string comment ) {
         Section = section;
         Comment = comment;
      }
   }

   public class SkipObsoleteContractResolver : DefaultContractResolver {
      protected override List<MemberInfo> GetSerializableMembers( Type type ) {
         return base.GetSerializableMembers( type ).Where( ( member ) =>
            member.GetCustomAttributes( typeof( ObsoleteAttribute ), true ).Length <= 0
         ).ToList();
      }
   }
}