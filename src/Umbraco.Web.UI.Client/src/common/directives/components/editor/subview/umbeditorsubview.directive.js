(function() {
   'use strict';

   function EditorSubView() {

      var directive = {
         transclude: true,
         restrict: 'E',
         replace: true,
         templateUrl: 'views/components/editor/subview/umb-editor-sub-view.html'
      };

      return directive;
   }

   angular.module('umbraco.directives').directive('umbEditorSubView', EditorSubView);

})();
