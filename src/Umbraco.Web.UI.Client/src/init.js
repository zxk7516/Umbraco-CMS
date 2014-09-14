/** Executed when the application starts, binds to events and set global state */

angular.module("umbraco")
    .run(function (userService, $log, $rootScope, $templateCache, appState, editorState, fileManager, assetsService, eventsService, securityRetryQueue, updateChecker, historyService, treeService) {


        var loginDialog;

        //This sets the default jquery ajax headers to include our csrf token, we
        // need to user the beforeSend method because our token changes per user/login so
        // it cannot be static
        $.ajaxSetup({
            beforeSend: function (xhr) {
                xhr.setRequestHeader("X-XSRF-TOKEN", $cookies["XSRF-TOKEN"]);
            }
        }); 
        

        //listens for global app erros and displays notifications
        eventsService.on("app.error", function(err) {
            notificationsService.error(err.headline, err.message);
        });

        // Listens for authentication and checks if our required assets are loaded, if/once they are we'll broadcast a ready event
        eventsService.on("app.authenticated", function(evt, data) {
            //before we can set the application as ready, we need to load user assets
            assetsService._loadInitAssets().then(function() {
                appState.setGlobalState("isReady", true);

                //send the ready event
                eventsService.emit("app.ready", data);
            });
        });

        //this launches the user login in case the user is not authenticated on app init
        eventsService.on("app.notAuthenticated", function(evt, data) {
            $rootScope.authenticated = false;
            $rootScope.user = undefined;

            if(!loginDialog){
                loginDialog = dialogService.open({
                    template: 'views/common/dialogs/login.html',
                    modalClass: "login-overlay",
                    animation: "slide-in-left",
                    show: true,
                    callback: function(data){
                       loginDialog = undefined;
                    },
                    dialogData: {
                        isTimedOut: data.isTimedOut
                    }
                });
            }
        });


        //when the app is ready/user is logged in, setup the data
        eventsService.on("app.ready", function (evt, data) {
            
            updateChecker.check().then(function(update){
                if(update && update !== "null"){
                    if(update.type !== "None"){
                        var notification = {
                               headline: "Update available",
                               message: "Click to download",
                               sticky: true,
                               type: "info",
                               url: update.url
                        };
                        notificationsService.add(notification);
                    }
                }
            });

            //if the user has changed we need to redirect to the root so they don't try to continue editing the
            //last item in the URL (NOTE: the user id can equal zero, so we cannot just do !data.lastUserId since that will resolve to true)
            if (data.lastUserId !== undefined && data.lastUserId !== null && data.lastUserId !== data.user.id) {
                $location.path("/").search("");
                historyService.removeAll();
                treeService.clearCache();
            }

            if($scope.user.emailHash){
                $timeout(function () { 
                               
                    //yes this is wrong.. 
                    $("#avatar-img").fadeTo(1000, 0, function () {
                        $timeout(function () {
                            //this can be null if they time out
                            if ($scope.user && $scope.user.emailHash) {
                                $scope.avatar = "http://www.gravatar.com/avatar/" + $scope.user.emailHash + ".jpg?s=64&d=mm";
                            }
                        });
                        $("#avatar-img").fadeTo(1000, 1);
                    });
                    
                  }, 3000);  
            }
        });
    



        // Register a handler for when an item is added to the retry queue
        securityRetryQueue.onItemAddedCallbacks.push(function (retryItem) {
            if (securityRetryQueue.hasMore()) {
                eventsService.emit("app.notAuthenticated");
            }
        });

        $rootScope.$on("$stateChangeStart", function(event, toState, toParams, fromState, fromParams){
              var s = toState;

              if(toParams.section){
                  $rootScope.locationTitle = toParams.section + " - " + $location.$$host;
              }
              else {
                  $rootScope.locationTitle = "Umbraco - " + $location.$$host;
              }
              
              //reset the editorState on each successful route chage
              editorState.reset();

              //reset the file manager on each route change, the file collection is only relavent
              // when working in an editor and submitting data to the server.
              //This ensures that memory remains clear of any files and that the editors don't have to manually clear the files.
              fileManager.clearFiles();
        });


        $rootScope.$on('$routeChangeError', function(event, current, previous, rejection) {
            event.preventDefault();

            if(rejection.search){
                $location.path(rejection.path).search(rejection.search);
            }else{
                $location.path(rejection.path);
            }
        });


        // For debug mode, always clear template cache to cut down on 
        // dev frustration and chrome cache on templates 
        if(Umbraco.Sys.ServerVariables.isDebuggingEnabled){
            $rootScope.$on('$viewContentLoaded', function() {
              $templateCache.removeAll();
            });
        }
        

        //initialize the application state with a user or with a not-authenticated state
        userService.getCurrentUser().then(function(data){
            eventsService.emit("app.authenticated", data.user);
        }, function(){
            eventsService.emit("app.notAuthenticated");
        });

        
        //check for touch device, add to global appState
        //var touchDevice = ("ontouchstart" in window || window.touch || window.navigator.msMaxTouchPoints === 5 || window.DocumentTouch && document instanceof DocumentTouch);
        var touchDevice =  /android|webos|iphone|ipad|ipod|blackberry|iemobile|touch/i.test(navigator.userAgent.toLowerCase());
        appState.setGlobalState("touchDevice", touchDevice);
});