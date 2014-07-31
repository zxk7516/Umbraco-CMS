
/**
 * @ngdoc controller
 * @name Umbraco.DialogController
 * @function
 * 
 * @description
 * The main application controller
 * 
 */
function DialogController($scope, $rootScope, appState, dialogService) {

    //Scope to handle data from the modal form
    $scope.dialogData = $scope.dialog.dialogData ? $scope.dialog.dialogData : {};
    $scope.dialogOptions = $scope.dialog.dialogOptions ? $scope.dialog.dialogOptions : {};
    if(!$scope.dialogData.selection){
        $scope.dialogData.selection = [];
    }

    $scope.swipeHide = function (e) {
        if (appState.getGlobalState("touchDevice")) {
            var selection = window.getSelection();
            if (selection.type !== "Range") {
                $scope.hide();
            }
        }
    };

    //NOTE: Same as 'close' without any callbacks
    $scope.hide = function () {
        dialogService.close(dialog);
    };

    //basic events for submitting and closing
    $scope.submit = function (data) {
        dialogService.submit($scope.dialogOptions, data);
    };


    $scope.close = function (data) {
        dialogService.close($scope.dialogOptions, data);
    };


    $scope.select = function (item) {
        var i = $scope.dialogData.selection.indexOf(item);
        if (i < 0) {
            $scope.dialogData.selection.push(item);
        } else {
            $scope.dialogData.selection.splice(i, 1);
        }
    };
    
}


//register it
angular.module('umbraco').controller("Umbraco.DialogController", DialogController);