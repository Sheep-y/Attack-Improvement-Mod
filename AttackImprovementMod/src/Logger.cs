using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace Sheepy.CSUtils {
   public class Logger : IDisposable {
      public Logger ( string file ) : this( file, 1000 ) { }
      public Logger ( string file, int writeDelay ) {
         if ( String.IsNullOrEmpty( file ) ) throw new NullReferenceException();
         LogFile = file.Trim();
         if ( ! writeDelay < 0 ) return;
         this.writeDelay = writeDelay;
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

      // Worker states locked by queue which is private.
      private HashSet<string> exceptions;
      private readonly List<LogEntry> queue;
      private Thread worker;
      private int writeDelay;

      // ============ Public Prop ============

      public static string Stacktrace { get { return new StackTrace( true ).ToString(); } }
      public string LogFile { get; private set; }

      // Settings are locked by this.
      public volatile SourceLevels LogLevel = SourceLevels.Information;
      public Func<SourceLevels,string> LevelText { get => _LevelText; set { lock( this ) { _LevelText = value; } } }
      public string TimeFormat { get => _TimeFormat; set { lock( this ) { _TimeFormat = value; } } }
      public string Prefix { get => _Prefix; set { lock( this ) { _Prefix = value; } } }
      public string Postfix { get => _Postfix; set { lock( this ) { _Postfix = value; } } }
      public bool IgnoreDuplicateExceptions { get => _IgnoreDuplicateExceptions; set { lock( this ) {
         _IgnoreDuplicateExceptions = value;
         if ( ! value ) exceptions = null;
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
            Monitor.Pulse( queue );
         }
      }

      public void Trace ( object message = null, params object[] args ) { Log( SourceLevels.ActivityTracing, message, args ); }
      public void Vocal ( object message = null, params object[] args ) { Log( SourceLevels.Verbose, message, args ); }
      public void Info  ( object message = null, params object[] args ) { Log( SourceLevels.Information, message, args ); }
      public void Warn  ( object message = null, params object[] args ) { Log( SourceLevels.Warning, message, args ); }
      public void Error ( object message = null, params object[] args ) { Log( SourceLevels.Error, message, args ); }

      // ============ Implementation ============

      private void WorkerLoop () {
         do {
            int delay = 0;
            lock ( queue ) {
               if ( worker == null ) return;
               try {
                  if ( queue.Count <= 0 ) Monitor.Wait( queue );
               } catch ( Exception ) { }
               delay = writeDelay;
            }
            if ( delay > 0 ) try {
               Thread.Sleep( writeDelay );
            } catch ( Exception ) { }
            LogEntry[] entries;
            lock ( queue ) {
               entries = queue.ToArray();
               queue.Clear();
            }
            if ( entries.Length > 0 )
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
                  if ( line.message is Exception ex && IgnoreDuplicateExceptions ) {
                     if ( exceptions == null ) exceptions = new HashSet<string>();
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
            writeDelay = 0; // Flush log immediately
            Monitor.Pulse( queue );
         }
      }
   }
}