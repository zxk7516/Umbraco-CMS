
/**
 * @ngdoc controller
 * @name Umbraco.MainController
 * @function
 * 
 * @description
 * The main application controller
 * 
 */
function MainController($scope, $rootScope, $location, $stateParams, $state, $timeout, $http, $log, appState, treeService, notificationsService, dialogService, userService, navigationService, historyService, updateChecker, assetsService, eventsService, umbRequestHelper) {

    //the null is important because we do an explicit bool check on this in the view
    //the avatar is by default the umbraco logo    
    $scope.authenticated = null;
    $scope.avatar = "assets/img/application/logo_white.png";
    $scope.touchDevice = appState.getGlobalState("touchDevice");
    
    $rootScope.$state = $state;
    $rootScope.$stateParams = $stateParams;

    //subscribes to notifications in the notification service
    $scope.notifications = notificationsService.current;
    $scope.$watch('notificationsService.current', function (newVal, oldVal, scope) {
        if (newVal) {
            $scope.notifications = newVal;
        }
    });

    //subscribes to dialogs in the dialog service
    $scope.dialogs = dialogService.current;

    
    $scope.closeDialogs = function (event) {
        //only close dialogs if non-link and non-buttons are clicked
        var el = event.target.nodeName;
        var els = ["INPUT","A","BUTTON"];

        if(els.indexOf(el) >= 0){return;}

        var parents = $(event.target).parents("a,button");
        if(parents.length > 0){
            return;
        }

        //SD: I've updated this so that we don't close the dialog when clicking inside of the dialog
        var nav = $(event.target).parents("#dialog");
        if (nav.length === 1) {
            return;
        }

        eventsService.emit("app.closeDialogs", event);
    };

    


}


//register it
angular.module('umbraco').controller("Umbraco.MainController", MainController);