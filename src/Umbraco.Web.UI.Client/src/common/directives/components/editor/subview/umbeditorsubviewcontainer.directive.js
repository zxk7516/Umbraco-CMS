(function() {
   'use strict';

   function EditorSubViewContainer() {

      var directive = {
         transclude: true,
         restrict: 'E',
         replace: true,
         templateUrl: 'views/components/editor/subview/umb-editor-sub-view-container.html'
      };

      return directive;
   }

   angular.module('umbraco.directives').directive('umbEditorSubViewContainer', EditorSubViewContainer);

})();
