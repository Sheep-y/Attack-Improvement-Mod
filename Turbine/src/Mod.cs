using BattleTech;
using BattleTech.Data;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;

namespace Sheepy.BattleTechMod.Turbine {
   using static System.Reflection.BindingFlags;

   public class Mod : BattleMod {

      public static void Init () {
         new Mod().Start();
      }

      private static Type dmType = typeof( DataManager );
      private static Dictionary<string, DataManager.DataManagerLoadRequest> foreground = new Dictionary<string, DataManager.DataManagerLoadRequest>();
      private static Dictionary<string, DataManager.DataManagerLoadRequest> background = new Dictionary<string, DataManager.DataManagerLoadRequest>();

      public override void ModStarts () {
         Logger.Delete();
         Logger = Logger.BTML_LOG;
         logger = HBS.Logging.Logger.GetLogger("Data.DataManager");
         Patch( dmType, "Clear", "ClearRequests", null );
         Patch( dmType, "CheckAsyncRequestsComplete", NonPublic, "Override_CheckRequestsComplete", null );
         Patch( dmType, "CheckRequestsComplete", NonPublic, "Override_CheckRequestsComplete", null );
         Patch( dmType, "GraduateBackgroundRequest", NonPublic, "Override_GraduateBackgroundRequest", null );
         // NotifyFileLoaded
         // NotifyFileLoadedAsync
         Patch( dmType, "NotifyFileLoadFailed", NonPublic, "Override_NotifyFileLoadFailed", null );
         Patch( dmType, "ProcessAsyncRequests", "Prefix_ProcessAsyncRequests", null );
         Patch( dmType, "ProcessRequests", "Prefix_ProcessRequests", null );
         Patch( dmType, "RequestResourceAsync_Internal", NonPublic, "Override_RequestResourceAsync_Internal", null );
         Patch( dmType, "RequestResource_Internal", NonPublic, "Override_RequestResource_Internal", null );
         Patch( dmType, "SetLoadRequestWeights", "Override_SetLoadRequestWeights", null );
         Patch( dmType, "UpdateRequestsTimeout", NonPublic, "Prefix_UpdateRequestsTimeout", null );
      }
         
		private static bool Override_CheckRequestsComplete ( ref bool __result ) {
			__result = CheckRequestsComplete();
         if ( __result ) foreground.Clear();
         return false;
		}
		private static bool Override_CheckAsyncRequestsComplete ( ref bool __result ) {
			__result = CheckAsyncRequestsComplete();
         if ( __result ) background.Clear();
         return false;
		}
      private static bool CheckRequestsComplete () { return foreground.Values.All( e => e.IsComplete() ); }
      private static bool CheckAsyncRequestsComplete () { return background.Values.All( e => e.IsComplete() ); }

      private static HBS.Logging.ILog logger;
      private static FieldInfo backgroundRequestsCurrentAllowedWeight = dmType.GetField( "backgroundRequestsCurrentAllowedWeight", NonPublic | Instance );
      private static FieldInfo foregroundRequestsCurrentAllowedWeight = dmType.GetField( "foregroundRequestsCurrentAllowedWeight", NonPublic | Instance );
      private static FieldInfo foregroundRequests = dmType.GetField( "foregroundRequests", NonPublic | Instance );
      private static FieldInfo backgroundRequests = dmType.GetField( "backgroundRequests", NonPublic | Instance );
      private static FieldInfo isLoadingAsync = dmType.GetField( "isLoadingAsync", NonPublic | Instance );
      private static MethodInfo CreateByResourceType = dmType.GetMethod( "CreateByResourceType", NonPublic | Instance );
      private static MethodInfo NotifyFileLoaded = dmType.GetMethod( "NotifyFileLoaded", NonPublic | Instance );
      private static MethodInfo NotifyFileLoadedAsync = dmType.GetMethod( "NotifyFileLoadedAsync", NonPublic | Instance );
      private static MethodInfo SaveCache = dmType.GetMethod( "SaveCache", NonPublic | Instance );

      private static string GetKey ( DataManager.DataManagerLoadRequest request ) { return GetKey( request.ResourceType, request.ResourceId ); }
      private static string GetKey ( BattleTechResourceType resourceType, string id ) { return (int) resourceType + "_" + id; }

		public static void ClearRequests () {
         foreground.Clear();
         background.Clear();
      }
      
		public static bool Override_GraduateBackgroundRequest ( DataManager __instance, ref bool __result, BattleTechResourceType resourceType, string id ) {
         __result = GraduateBackgroundRequest( __instance, resourceType, id );
         return false;
      }

		private static bool GraduateBackgroundRequest ( DataManager me, BattleTechResourceType resourceType, string id ) {
         string key = GetKey( resourceType, id );
         if ( ! background.TryGetValue( key, out DataManager.DataManagerLoadRequest dataManagerLoadRequest ) )
            return false;
         dataManagerLoadRequest.SetAsync( false );
         dataManagerLoadRequest.ResetRequestState();
         background.Remove( key );
         foreground.Add( key, dataManagerLoadRequest );
         bool wasLoadingAsync = (bool) isLoadingAsync.GetValue( me );
         bool nowLoadingAsync = ! CheckAsyncRequestsComplete();
         if ( nowLoadingAsync != wasLoadingAsync ) {
            isLoadingAsync.SetValue( me, nowLoadingAsync );
            if ( wasLoadingAsync ) {
               SaveCache.Invoke( me, null );
               background.Clear();
               BattleTechGame.MessageCenter.PublishMessage( new DataManagerAsyncLoadCompleteMessage() );
            }
         }
         return true;
      }
      
		public static bool Override_NotifyFileLoadFailed ( DataManager __instance, DataManager.DataManagerLoadRequest request ) {
         string key = GetKey( request );
			if ( foreground.Remove( key ) )
				NotifyFileLoaded.Invoke( __instance, new object[]{ request } );
			else if ( background.Remove( key ) )
				NotifyFileLoadedAsync.Invoke( __instance, new object[]{ request } );
         return false;
		}
      
		public static void Prefix_ProcessAsyncRequests ( DataManager __instance ) {
         List<DataManager.DataManagerLoadRequest> f = (List<DataManager.DataManagerLoadRequest>) backgroundRequests.GetValue( __instance );
         f.Clear();
         f.AddRange( background.Values.Where( e => e.State != DataManager.DataManagerLoadRequest.RequestState.Processing ) );
		}

		public static void Prefix_ProcessRequests ( DataManager __instance ) {
         List<DataManager.DataManagerLoadRequest> f = (List<DataManager.DataManagerLoadRequest>) foregroundRequests.GetValue( __instance );
         f.Clear();
         f.AddRange( foreground.Values );
		}

      public static bool Override_RequestResourceAsync_Internal ( DataManager __instance, BattleTechResourceType resourceType, string identifier, PrewarmRequest prewarm ) {
         if ( string.IsNullOrEmpty( identifier ) ) return false;
         DataManager me = __instance;
         string key = GetKey( resourceType, identifier );
         background.TryGetValue( key, out DataManager.DataManagerLoadRequest dataManagerLoadRequest );
         if ( dataManagerLoadRequest != null ) {
            if ( dataManagerLoadRequest.State == DataManager.DataManagerLoadRequest.RequestState.Complete ) {
               if ( !dataManagerLoadRequest.DependenciesLoaded( (uint) backgroundRequestsCurrentAllowedWeight.GetValue( me ) ) ) {
                  dataManagerLoadRequest.ResetRequestState();
               } else {
                  dataManagerLoadRequest.NotifyLoadComplete();
               }
            } else {
               // Move to top of queue. Not supported by HashTable.
               //backgroundRequest.Remove( dataManagerLoadRequest );
               //backgroundRequest.Insert( 0, dataManagerLoadRequest );
            }
            return false;
         }
         bool isForeground = foreground.ContainsKey( key );
         bool isTemplate = identifier.ToLowerInvariant().Contains("template");
         if ( isForeground || isTemplate ) return false;
         dataManagerLoadRequest = (DataManager.DataManagerLoadRequest) CreateByResourceType.Invoke( me, new object[]{ resourceType, identifier, prewarm } );
         dataManagerLoadRequest.SetAsync( true );
         background.Add( key, dataManagerLoadRequest );
         return false;
      }

      public static bool Override_RequestResource_Internal ( DataManager __instance, BattleTechResourceType resourceType, string identifier, PrewarmRequest prewarm, bool allowRequestStacking ) {
         if ( string.IsNullOrEmpty( identifier ) ) return false;
         DataManager me = __instance;
         string key = GetKey( resourceType, identifier );
         foreground.TryGetValue( key, out DataManager.DataManagerLoadRequest dataManagerLoadRequest );
         if ( dataManagerLoadRequest != null ) {
            if ( dataManagerLoadRequest.State != DataManager.DataManagerLoadRequest.RequestState.Complete || !dataManagerLoadRequest.DependenciesLoaded( dataManagerLoadRequest.RequestWeight.RequestWeight ) ) {
               if ( allowRequestStacking )
                  dataManagerLoadRequest.IncrementCacheCount();
            } else
               NotifyFileLoaded.Invoke( me, new object[]{ dataManagerLoadRequest } );
            return false;
         }
         bool movedToForeground = GraduateBackgroundRequest( me, resourceType, identifier);
         bool skipLoad = false;
         bool isTemplate = identifier.ToLowerInvariant().Contains("template");
         if ( !movedToForeground && !skipLoad && !isTemplate )
            foreground.Add( key, (DataManager.DataManagerLoadRequest) CreateByResourceType.Invoke( me, new object[]{ resourceType, identifier, prewarm } ) );
         return false;
      }

      public static bool Override_SetLoadRequestWeights ( DataManager __instance, uint foregroundRequestWeight, uint backgroundRequestWeight ) {
			foregroundRequestsCurrentAllowedWeight.SetValue( __instance, foregroundRequestWeight );
			backgroundRequestsCurrentAllowedWeight.SetValue( __instance, backgroundRequestWeight );
			foreach (DataManager.DataManagerLoadRequest dataManagerLoadRequest in foreground.Values )
				if ( foregroundRequestWeight > dataManagerLoadRequest.RequestWeight.AllowedWeight )
					dataManagerLoadRequest.RequestWeight.SetAllowedWeight(foregroundRequestWeight);
			foreach (DataManager.DataManagerLoadRequest dataManagerLoadRequest2 in background.Values )
				if (backgroundRequestWeight > dataManagerLoadRequest2.RequestWeight.AllowedWeight)
					dataManagerLoadRequest2.RequestWeight.SetAllowedWeight(backgroundRequestWeight);
         return false;
      }

      public static void Prefix_UpdateRequestsTimeout ( DataManager __instance ) {
         List<DataManager.DataManagerLoadRequest> f = (List<DataManager.DataManagerLoadRequest>) foregroundRequests.GetValue( __instance );
         f.Clear();
         f.AddRange( foreground.Values.Where( e => e.State == DataManager.DataManagerLoadRequest.RequestState.Processing ) );
         f = (List<DataManager.DataManagerLoadRequest>) backgroundRequests.GetValue( __instance );
         f.Clear();
         f.AddRange( background.Values.Where( e => e.State == DataManager.DataManagerLoadRequest.RequestState.Processing ) );
      }


      // ============ Logging ============

      internal static Logger ModLog = Logger.BTML_LOG;

      public static void Log ( object message ) { ModLog.Log( message ); }
      public static void Log ( string message = "" ) { ModLog.Log( message ); }
      public static void Log ( string message, params object[] args ) { ModLog.Log( message, args ); }
      
      public static void Warn ( object message ) { ModLog.Warn( message ); }
      public static void Warn ( string message ) { ModLog.Warn( message ); }
      public static void Warn ( string message, params object[] args ) { ModLog.Warn( message, args ); }

      public static bool Error ( object message ) { return ModLog.Error( message ); }
      public static void Error ( string message ) { ModLog.Error( message ); }
      public static void Error ( string message, params object[] args ) { ModLog.Error( message, args ); }
   }
}