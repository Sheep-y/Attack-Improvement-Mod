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
      private static float currentTimeout = -1;
      private static float currentAsyncTimeout = -1;

      public override void ModStarts () {
         Logger.Delete();
         Logger = Logger.BT_LOG;
         logger = HBS.Logging.Logger.GetLogger( "Data.DataManager" );
         Patch( dmType, "Clear", "ClearRequests", null );
         Patch( dmType, "CheckAsyncRequestsComplete", NonPublic, "Override_CheckRequestsComplete", null );
         Patch( dmType, "CheckRequestsComplete", NonPublic, "Override_CheckRequestsComplete", null );
         Patch( dmType, "GraduateBackgroundRequest", NonPublic, "Override_GraduateBackgroundRequest", null );
         Patch( dmType, "NotifyFileLoaded", NonPublic, "Override_NotifyFileLoaded", null );
         Patch( dmType, "NotifyFileLoadedAsync", NonPublic, "Override_NotifyFileLoadedAsync", null );
         Patch( dmType, "NotifyFileLoadFailed", NonPublic, "Override_NotifyFileLoadFailed", null );
         Patch( dmType, "ProcessAsyncRequests", "Override_ProcessAsyncRequests", null );
         Patch( dmType, "ProcessRequests", "Override_ProcessRequests", null );
         Patch( dmType, "RequestResourceAsync_Internal", NonPublic, "Override_RequestResourceAsync_Internal", null );
         Patch( dmType, "RequestResource_Internal", NonPublic, "Override_RequestResource_Internal", null );
         Patch( dmType, "SetLoadRequestWeights", "Override_SetLoadRequestWeights", null );
         Patch( dmType, "UpdateRequestsTimeout", NonPublic, "Override_UpdateRequestsTimeout", null );
      }
         
		private static bool Override_CheckRequestsComplete ( ref bool __result ) {
			__result = CheckRequestsComplete();
         return false;
		}
		private static bool Override_CheckAsyncRequestsComplete ( ref bool __result ) {
			__result = CheckAsyncRequestsComplete();
         return false;
		}
      private static bool CheckRequestsComplete () { return foreground.Values.All( e => e.IsComplete() ); }
      private static bool CheckAsyncRequestsComplete () { return background.Values.All( e => e.IsComplete() ); }

      private static HBS.Logging.ILog logger;
      private static FieldInfo MessageCenter = dmType.GetField( "MessageCenter", NonPublic | Instance );
      private static FieldInfo backgroundRequestsCurrentAllowedWeight = dmType.GetField( "backgroundRequestsCurrentAllowedWeight", NonPublic | Instance );
      private static FieldInfo foregroundRequestsCurrentAllowedWeight = dmType.GetField( "foregroundRequestsCurrentAllowedWeight", NonPublic | Instance );
      private static FieldInfo prewarmRequests = dmType.GetField( "prewarmRequests", NonPublic | Instance );
      private static FieldInfo isLoading = dmType.GetField( "isLoading", NonPublic | Instance );
      private static FieldInfo isLoadingAsync = dmType.GetField( "isLoadingAsync", NonPublic | Instance );
      private static MethodInfo CreateByResourceType = dmType.GetMethod( "CreateByResourceType", NonPublic | Instance );
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

      public static bool Override_NotifyFileLoaded ( DataManager __instance, DataManager.DataManagerLoadRequest request ) {
         if ( request.Prewarm != null ) {
            List<PrewarmRequest> pre = (List<PrewarmRequest>) prewarmRequests.GetValue( __instance );
            pre.Remove( request.Prewarm );
         }
         if ( CheckRequestsComplete() ) {
            isLoading.SetValue( __instance, false );
            SaveCache.Invoke( __instance, null );
            foreground.Clear();
            MessageCenter center = BattleTechGame?.MessageCenter ?? (MessageCenter) MessageCenter.GetValue( __instance );
            center?.PublishMessage( new DataManagerLoadCompleteMessage() );
         }
         return false;
      }

      // Token: 0x06005F72 RID: 24434 RVA: 0x001D8F40 File Offset: 0x001D7140
      public static bool Override_NotifyFileLoadedAsync ( DataManager __instance, DataManager.DataManagerLoadRequest request ) {
         if ( request.Prewarm != null ) {
            List<PrewarmRequest> pre = (List<PrewarmRequest>) prewarmRequests.GetValue( __instance );
            pre.Remove( request.Prewarm );
         }
         if ( CheckAsyncRequestsComplete() ) {
            isLoadingAsync.SetValue( __instance, false );
            SaveCache.Invoke( __instance, null );
            background.Clear();
            MessageCenter center = BattleTechGame?.MessageCenter ?? (MessageCenter) MessageCenter.GetValue( __instance );
            center?.PublishMessage( new DataManagerAsyncLoadCompleteMessage() );
         }
         return false;
      }

      public static bool Override_NotifyFileLoadFailed ( DataManager __instance, DataManager.DataManagerLoadRequest request ) {
         string key = GetKey( request );
			if ( foreground.Remove( key ) )
				Override_NotifyFileLoaded( __instance, request );
			else if ( background.Remove( key ) )
				Override_NotifyFileLoadedAsync( __instance, request );
         return false;
		}

      public static bool Override_ProcessRequests ( DataManager __instance ) {
         DataManager me = __instance;
         int lightLoad = 0;
         int heavyLoad = 0;
         uint currentAllowedWeight = (uint) foregroundRequestsCurrentAllowedWeight.GetValue( me );
         foreach ( DataManager.DataManagerLoadRequest request in foreground.Values.ToArray() ) {
            if ( lightLoad >= DataManager.MaxConcurrentLoadsLight && heavyLoad >= DataManager.MaxConcurrentLoadsHeavy )
               break;
            request.RequestWeight.SetAllowedWeight( currentAllowedWeight );
            if ( request.State == DataManager.DataManagerLoadRequest.RequestState.Requested ) {
               if ( request.IsMemoryRequest )
                  me.RemoveObjectOfType( request.ResourceId, request.ResourceType );
               if ( request.AlreadyLoaded ) {
                  if ( !request.DependenciesLoaded( currentAllowedWeight ) ) {
                     DataManager.ILoadDependencies dependencyLoader = request.TryGetLoadDependencies();
                     if ( dependencyLoader != null ) {
                        request.RequestWeight.SetAllowedWeight( currentAllowedWeight );
                        dependencyLoader.RequestDependencies( me, () => {
                           if ( dependencyLoader.DependenciesLoaded( request.RequestWeight.AllowedWeight ) )
                              request.NotifyLoadComplete();
                        }, request );
                        if ( request.RequestWeight.RequestWeight == 10u ) {
                           if ( DataManager.MaxConcurrentLoadsLight > 0 )
                              lightLoad++;
                        } else if ( DataManager.MaxConcurrentLoadsHeavy > 0 )
                           heavyLoad++;
                        isLoading.SetValue( me, true );
                        me.ResetRequestsTimeout();
                     }
                  } else
                     request.NotifyLoadComplete();
               } else {
                  if ( lightLoad >= DataManager.MaxConcurrentLoadsLight && heavyLoad >= DataManager.MaxConcurrentLoadsHeavy )
                     break;
                  if ( ! request.ManifestEntryValid ) {
                     logger.LogError( string.Format( "LoadRequest for {0} of type {1} has an invalid manifest entry. Any requests for this object will fail.", request.ResourceId, request.ResourceType ) );
                     request.NotifyLoadFailed();
                  } else if ( !request.RequestWeight.RequestAllowed ) {
                     request.NotifyLoadComplete();
                  } else {
                     if ( request.RequestWeight.RequestWeight == 10u ) {
                        if ( DataManager.MaxConcurrentLoadsLight > 0 )
                           lightLoad++;
                     } else if ( DataManager.MaxConcurrentLoadsHeavy > 0 )
                        heavyLoad++;
                     isLoading.SetValue( me, true );
                     request.Load();
                     me.ResetRequestsTimeout();
                  }
               }
            }
         }
         return false;
      }

      public static bool Override_ProcessAsyncRequests ( DataManager __instance ) {
         DataManager me = __instance;
         uint currentAllowedWeight = (uint) backgroundRequestsCurrentAllowedWeight.GetValue( me );
         foreach ( DataManager.DataManagerLoadRequest request in background.Values ) {
            request.RequestWeight.SetAllowedWeight( currentAllowedWeight );
            DataManager.DataManagerLoadRequest.RequestState state = request.State;
            if ( state == DataManager.DataManagerLoadRequest.RequestState.Processing ) return false;
            if ( state == DataManager.DataManagerLoadRequest.RequestState.RequestedAsync ) {
               if ( request.IsMemoryRequest )
                  me.RemoveObjectOfType( request.ResourceId, request.ResourceType );
               if ( request.AlreadyLoaded ) {
                  if ( !request.DependenciesLoaded( currentAllowedWeight ) ) {
                     DataManager.ILoadDependencies dependencyLoader = request.TryGetLoadDependencies();
                     if ( dependencyLoader != null ) {
                        request.RequestWeight.SetAllowedWeight( currentAllowedWeight );
                        dependencyLoader.RequestDependencies( me, () => {
                           if ( dependencyLoader.DependenciesLoaded( request.RequestWeight.AllowedWeight ) )
                              request.NotifyLoadComplete();
                        }, request );
                        isLoadingAsync.SetValue( me, true );
                        me.ResetAsyncRequestsTimeout();
                     }
                  } else
                     request.NotifyLoadComplete();
               } else if ( !request.ManifestEntryValid ) {
                  logger.LogError( string.Format( "LoadRequest for {0} of type {1} has an invalid manifest entry. Any requests for this object will fail.", request.ResourceId, request.ResourceType ) );
                  request.NotifyLoadFailed();
               } else if ( !request.RequestWeight.RequestAllowed ) {
                  request.NotifyLoadComplete();
               } else {
                  isLoadingAsync.SetValue( me, true );
                  request.Load();
                  me.ResetAsyncRequestsTimeout();
               }
               return false;
            }
         }
         return false;
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
               Override_NotifyFileLoaded( me, dataManagerLoadRequest );
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

      public static bool Override_UpdateRequestsTimeout ( DataManager __instance, float deltaTime ) {
         DataManager me = __instance;
         if ( currentTimeout >= 0f ) {
            if ( foreground.Values.Any( IsProcessing ) ) {
               DataManager.DataManagerLoadRequest[] list = foreground.Values.Where( IsProcessing ).ToArray();
               currentTimeout += deltaTime;
               if ( currentTimeout > list.Count() * 0.2f ) {
                  foreach ( DataManager.DataManagerLoadRequest dataManagerLoadRequest in list ) {
                     logger.LogWarning( string.Format( "DataManager Request for {0} has taken too long. Cancelling request. Your load will probably fail", dataManagerLoadRequest.ResourceId ) );
                     dataManagerLoadRequest.NotifyLoadFailed();
                  }
                  currentTimeout = -1f;
               }
            }
         }
         if ( currentAsyncTimeout >= 0f && background.Count > 0 ) {
            currentAsyncTimeout += deltaTime;
            if ( currentAsyncTimeout > 20f ) {
               DataManager.DataManagerLoadRequest dataManagerLoadRequest = background.Values.First( IsProcessing );
               if ( dataManagerLoadRequest != null ) {
                  logger.LogWarning( string.Format( "DataManager ASYNC Request for {0} has taken too long. Cancelling request. Your load will probably fail", dataManagerLoadRequest.ResourceId ) );
                  dataManagerLoadRequest.NotifyLoadFailed();
               }
               currentAsyncTimeout = -1f;
            }
         }
         return false;
      }

      private static bool IsProcessing ( DataManager.DataManagerLoadRequest e ) {
         return e.State == DataManager.DataManagerLoadRequest.RequestState.Processing;
      }

      // ============ Logging ============

      internal static Logger ModLog = Logger.BT_LOG;

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