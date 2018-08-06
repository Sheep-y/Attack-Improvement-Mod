using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace Sheepy.CSUtils {
   public class Logger : IDisposable {
      public Logger ( string file ) : this( file, false ) { }
      public Logger ( string file, bool async ) {
         if ( String.IsNullOrEmpty( file ) ) throw new NullReferenceException();
         LogFile = file.Trim();
         if ( ! async ) return;
         queue = new List<LogEntry>();
         worker = new Thread( WorkerLoop ) { Name = "Logger " + LogFile, Priority = ThreadPriority.BelowNormal };
         worker.Start();
      }

      // ============ Self Prop ============

      private Func<SourceLevels,string> _LevelText = ( level ) => { //return level.ToString() + ": ";
         if ( level <= SourceLevels.Critical ) return "CRIT "; if ( level <= SourceLevels.Error       ) return "ERR  ";
         if ( level <= SourceLevels.Warning  ) return "WARN "; if ( level <= SourceLevels.Information ) return "INFO ";
         if ( level <= SourceLevels.Verbose  ) return "FINE "; return "TRAC ";
      };
      private string _TimeFormat = "hh:mm:ss.ffff ", _Prefix = null, _Postfix = null;
      private bool _IgnoreDuplicateExceptions = true;

      protected struct LogEntry { public DateTime time; public SourceLevels level; public object message; public object[] args; }
      private HashSet<string> exceptions = new HashSet<string>();
      private readonly List<LogEntry> queue;
      private Thread worker;

      // ============ Public Prop ============

      public static string Stacktrace { get { return new StackTrace( true ).ToString(); } }
      public string LogFile { get; private set; }

      // Settings are locked by this.  Worker is locked by queue.
      public volatile SourceLevels LogLevel = SourceLevels.Information;
      public Func<SourceLevels,string> LevelText { get => _LevelText; set { lock( this ) { _LevelText = value; } } }
      public string TimeFormat { get => _TimeFormat; set { lock( this ) { _TimeFormat = value; } } }
      public string Prefix { get => _Prefix; set { lock( this ) { _Prefix = value; } } }
      public string Postfix { get => _Postfix; set { lock( this ) { _Postfix = value; } } }
      public bool IgnoreDuplicateExceptions { get => _IgnoreDuplicateExceptions; set { lock( this ) {
         _IgnoreDuplicateExceptions = value;
         if ( value ) { if ( exceptions == null ) exceptions = new HashSet<string>();
         } else exceptions = null;
      } } }

      // ============ API ============

      public virtual bool Exists () { return File.Exists( LogFile ); }

      public virtual Exception Delete () {
         if ( LogFile == "Mods/BTModLoader.log" || LogFile == "BattleTech_Data/output_log.txt" )
            return new ApplicationException( "Cannot delete BTModLoader.log or BattleTech game log." );
         try {
            File.Delete( LogFile );
            return null;
         } catch ( Exception e ) { return e; }
      }

      public void Log ( SourceLevels level, object message, params object[] args ) {
         if ( ( level & LogLevel ) != level ) return;
         LogEntry entry = new LogEntry(){ time = DateTime.Now, level = level, message = message, args = args };
         if ( queue == null ) lock ( this ) {
            WriteLog( entry );
         } else lock ( queue ) {
            if ( worker == null ) throw new InvalidOperationException( "Logger already disposed." );
            queue.Add( entry );
            Monitor.PulseAll( queue );
         }
      }

      public void Trace ( object message = null, params object[] args ) { Log( SourceLevels.ActivityTracing, message, args ); }
      public void Vocal ( object message = null, params object[] args ) { Log( SourceLevels.Verbose, message, args ); }
      public void Info  ( object message = null, params object[] args ) { Log( SourceLevels.Information, message, args ); }
      public void Warn  ( object message = null, params object[] args ) { Log( SourceLevels.Warning, message, args ); }
      public void Error ( object message = null, params object[] args ) { Log( SourceLevels.Error, message, args ); }

      /* Indent not implemented because it must be tracked by LogEntry and requires a lock 

      public Logger AddIndent ( string indent = "   " ) {
         lock( this ) { Indent += indent; }
         return this;
      }
      public Logger AddIndent ( int charCount ) { return AddIndent( String.Empty.PadRight( charCount ) ); }

      public Logger RemoveIndent ( string indent ) { return RemoveIndent( indent.Length ); }
      public Logger RemoveIndent ( int charCount = 3 ) {
         lock( this ) { 
            if ( Indent.Length <= charCount )
               Indent = String.Empty;
            else
               Indent = Indent.Substr( 0, Indent.Length - charCount );
         }
         return this;
      }

      public Logger ResetIndent () { 
         lock( this ) { Indent = String.Empty; }
         return this;
      }
      */

      // ============ Implementation ============

      private void WorkerLoop () {
         do {
            LogEntry[] entries;
            lock ( queue ) {
               if ( worker == null ) return;
               try {
                  Thread.Sleep( 1000 ); // Throttle write frequency
                  if ( queue.Count <= 0 ) Monitor.Wait( queue );
               } catch ( Exception ) { }
               entries = queue.ToArray();
               queue.Clear();
            }
            WriteLog( entries );
         } while ( true );
      }

      protected virtual void WriteLog ( params LogEntry[] entries ) {
         if ( entries.Length <= 0 ) return;
         StringBuilder buf = new StringBuilder();
         lock ( this ) { // Not expecting settings to change frequently. Lock outside format loop for higher throughput.
            foreach ( LogEntry line in entries ) {
               string txt = line.message?.ToString();
               if ( ! String.IsNullOrEmpty( txt ) ) try {
                  if ( IgnoreDuplicateExceptions && line.message is Exception ex ) {
                     if ( exceptions.Contains( txt ) ) return;
                     exceptions.Add( txt );
                  }
                  if ( ! String.IsNullOrEmpty( TimeFormat ) )
                     buf.Append( line.time.ToString( TimeFormat ) );
                  if ( LevelText != null )
                     buf.Append( LevelText( line.level ) );
                  buf.Append( Prefix );
                  if ( line.args != null && line.args.Length > 0 && txt != null )
                     txt = string.Format( txt, line.args );
                  buf.Append( txt ).Append( Postfix );
               } catch ( Exception ex ) { Console.Error.WriteLine( ex ); }
               buf.Append( Environment.NewLine ); // Null or empty message = insert blank new line
            }
         }
         try {
            File.AppendAllText( LogFile, buf.ToString() );
         } catch ( Exception ex ) {
            Console.Error.WriteLine( ex );
         }
      }

      public void Dispose () {
         if ( queue != null ) lock ( queue ) {
            worker = null;
            Monitor.PulseAll( queue );
         }
      }
   }
}
