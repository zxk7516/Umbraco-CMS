(function() {
   'use strict';

   function EditorSubViewContent() {

      var directive = {
         transclude: true,
         restrict: 'E',
         replace: true,
         templateUrl: 'views/components/editor/subview/umb-editor-sub-view-content.html'
      };

      return directive;
   }

   angular.module('umbraco.directives').directive('umbEditorSubViewContent', EditorSubViewContent);

})();
