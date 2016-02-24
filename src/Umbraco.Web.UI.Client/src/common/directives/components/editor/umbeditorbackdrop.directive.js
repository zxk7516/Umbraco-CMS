(function() {
    'use strict';

    function EditorBackdrop() {

        var directive = {
            transclude: true,
            restrict: 'E',
            replace: true,
            templateUrl: 'views/components/editor/umb-editor-backdrop.html'
        };

        return directive;
    }

    angular.module('umbraco.directives').directive('umbEditorBackdrop', EditorBackdrop);

})();
