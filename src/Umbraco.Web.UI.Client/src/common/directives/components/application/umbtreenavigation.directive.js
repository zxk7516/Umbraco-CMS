(function() {
    'use strict';

    function TreeNavigation(appState) {

        function link(scope, el, attr, ctrl) {

            scope.currentSection = appState.getSectionState("currentSection");

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
