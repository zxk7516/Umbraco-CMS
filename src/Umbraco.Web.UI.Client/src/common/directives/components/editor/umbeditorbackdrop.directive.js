(function() {
    'use strict';

    function EditorBackdrop() {

        function link(scope, el, attr, ctrl) {

            scope.closeBackdrop = function() {
                if(scope.onCloseBackdrop) {
                    scope.onCloseBackdrop();
                }
            };

        }

        var directive = {
            transclude: true,
            restrict: 'E',
            replace: true,
            templateUrl: 'views/components/editor/umb-editor-backdrop.html',
            scope: {
                onCloseBackdrop: "="
            },
            link: link
        };

        return directive;
    }

    angular.module('umbraco.directives').directive('umbEditorBackdrop', EditorBackdrop);

})();
