/**
* @ngdoc directive
* @name umbraco.directives.directive:umbNavigation
* @restrict E
**/
function umbNavigationDirective(appState, $location) {
    return {
        restrict: "E",    // restrict to an element
        replace: true,   // replace the html element with the template
        templateUrl: 'views/components/application/umb-navigation.html',
        link: function (scope, element, attr, ctrl) {

            scope.showTreeNavigation = appState.getGlobalState("showNavigation");

            scope.searchClick = function() {
                scope.searchOverlay = {
                    view: "search",
                    show: true,
                    submit: function(model) {
                        $location.path(model.selectedItem.editorPath);
                        scope.searchOverlay.show = false;
                        scope.searchOverlay = null;
                    },
                    close: function(oldModel) {
                        scope.searchOverlay.show = false;
                        scope.searchOverlay = null;
                    }
                };
            };

            scope.avatarClick = function(){

                if(scope.helpDialog) {
                    closeHelpDialog();
                }

                if(!scope.userDialog) {
                    scope.userDialog = {
                        view: "user",
                        show: true,
                        close: function(oldModel) {
                            closeUserDialog();
                        }
                    };
                } else {
                    closeUserDialog();
                }

            };

            function closeUserDialog() {
                scope.userDialog.show = false;
                scope.userDialog = null;
            }

            scope.helpClick = function(){

                if(scope.userDialog) {
                    closeUserDialog();
                }

                if(!scope.helpDialog) {
                    scope.helpDialog = {
                        view: "help",
                        show: true,
                        close: function(oldModel) {
                            closeHelpDialog();
                        }
                    };
                } else {
                    closeHelpDialog();
                }

            };


            function closeHelpDialog() {
                scope.helpDialog.show = false;
                scope.helpDialog = null;
            }


            scope.showTree = function () {
                scope.showTreeNavigation = appState.getGlobalState("showNavigation");
                if(scope.showTreeNavigation === true) {
                    appState.setGlobalState("showNavigation", false);
                } else {
                    appState.setGlobalState("showNavigation", true);
                }
			};

        }
    };
}

angular.module('umbraco.directives').directive("umbNavigation", umbNavigationDirective);
