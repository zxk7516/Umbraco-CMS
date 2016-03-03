/**
* @ngdoc directive
* @name umbraco.directives.directive:umbNavigation
* @restrict E
**/
function umbNavigationDirective() {
    return {
        restrict: "E",    // restrict to an element
        replace: true,   // replace the html element with the template
        templateUrl: 'views/components/application/umb-navigation.html',
        link: function (scope, element, attr, ctrl) {

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

        }
    };
}

angular.module('umbraco.directives').directive("umbNavigation", umbNavigationDirective);
