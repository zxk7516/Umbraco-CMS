/**
* @ngdoc directive
* @name umbraco.directives.directive:umbProperty
* @restrict E
**/
angular.module("umbraco.directives")
    .directive('umbProperty', function (umbPropEditorHelper) {
        return {
            scope: {
                property: "="
            },
            transclude: true,
            restrict: 'E',
            replace: true,
            templateUrl: 'views/components/property/umb-property.html',
            link: function(scope) {
                scope.propertyAlias = Umbraco.Sys.ServerVariables.isDebuggingEnabled === true ? scope.property.alias : null;
                scope.showDropdown = false;
                scope.selectedVariation = {};
                scope.variations = [
                    {
                        name: "Danish",
                        master: true,
                        selected: true
                    },
                    {
                        name: "German"
                    },
                    {
                        name: "Spanish"
                    },
                    {
                        name: "Finnish"
                    }
                ];

                function activate() {

                    for(var i = 0; i < scope.variations.length; i++ ) {
                        var variation = scope.variations[i];
                        if(variation.selected) {
                            scope.property.variation = variation;
                        }
                    }
                }

                scope.toggleDropdown = function() {
                    scope.showDropdown = !scope.showDropdown;
                };

                scope.closeDropdown = function() {
                    scope.showDropdown = false;
                };

                scope.editContent = function() {
                    scope.property.variation = null;
                    scope.showDropdown = false;
                };

                scope.syncContent = function(selectedVariation) {
                    for(var i = 0; i < scope.variations.length; i++ ) {
                        var variation = scope.variations[i];
                        variation.selected = false;
                    }
                    selectedVariation.selected = true;
                    scope.property.variation = selectedVariation;
                    scope.showDropdown = false;
                };

                activate();

            },
            //Define a controller for this directive to expose APIs to other directives
            controller: function ($scope, $timeout) {

                var self = this;

                //set the API properties/methods

                self.property = $scope.property;
                self.setPropertyError = function(errorMsg) {
                    $scope.property.propertyErrorMessage = errorMsg;
                };
            }
        };
    });
