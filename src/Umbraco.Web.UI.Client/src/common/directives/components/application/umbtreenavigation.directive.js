(function() {
    'use strict';

    function TreeNavigation(appState) {

        function link(scope, el, attr, ctrl) {
            scope.showTreeNavigation = false;
            scope.currentSection = appState.getSectionState("currentSection");

            scope.$watch(function(){
               return appState.getGlobalState("showNavigation");
            }, function (newValue) {
                scope.showTreeNavigation = newValue;
            });

        }

        var directive = {
            restrict: 'E',
            replace: true,
            templateUrl: 'views/components/application/umb-tree-navigation.html',
            link: link
        };

        return directive;
    }

    angular.module('umbraco.directives').directive('umbTreeNavigation', TreeNavigation);

})();
