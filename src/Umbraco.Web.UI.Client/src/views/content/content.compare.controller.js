/**
 * @ngdoc controller
 * @name Umbraco.Editors.Content.CompareController
 * @function
 * 
 * @description
 * The controller for the content variant comparing
 */
function ContentCompareController($scope, $routeParams, $q, $timeout, $window, appState, contentResource, entityResource, navigationService, notificationsService, angularHelper, serverValidationManager, contentEditingHelper, treeService, fileManager, formHelper, umbRequestHelper, keyboardService, umbModelMapper, editorState, $http, $location) {

    //setup scope vars
    $scope.ids = $routeParams.id.split(",");

}

angular.module("umbraco").controller("Umbraco.Editors.Content.CompareController", ContentCompareController);
