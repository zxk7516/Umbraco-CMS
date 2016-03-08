(function() {
   'use strict';

   function EditorSubViewHeader() {

      var directive = {
         transclude: true,
         restrict: 'E',
         replace: true,
         templateUrl: 'views/components/editor/subview/umb-editor-sub-view-header.html'
      };

      return directive;
   }

   angular.module('umbraco.directives').directive('umbEditorSubViewHeader', EditorSubViewHeader);

})();
