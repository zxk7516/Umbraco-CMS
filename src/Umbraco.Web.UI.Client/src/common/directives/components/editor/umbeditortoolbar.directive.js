(function() {
    'use strict';

    function EditorToolbar() {

        var directive = {
            restrict: 'E',
            replace: true,
            transclude: true,
            templateUrl: 'views/components/editor/umb-editor-toolbar.html'
        };

        return directive;

    }

    angular.module('umbraco.directives').directive('umbEditorToolbar', EditorToolbar);

})();
