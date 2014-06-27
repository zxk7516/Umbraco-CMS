/**
* @ngdoc directive
* @name umbraco.directives.directive:umbPanel
* @restrict E
**/
angular.module("umbraco.directives.html")
	.directive('detectFold', function($timeout, $log){
		return {
			restrict: 'A',
			link: function (scope, el, attrs) {
				
				var cl = "umb-editor-buttons";

				var state = false,
					parent = $(".tab-content"),
					winHeight = $(window).height();

				if(!parent){
					parent = $(".umb-body");
				}	

				var	calculate = _.throttle(function(){
						if(el && el.is(":visible") && !el.hasClass(cl)){
							//var parent = el.parent();
							var hasOverflow = parent.innerHeight() < parent[0].scrollHeight;
							//var belowFold = (el.offset().top + el.height()) > winHeight;
							if(hasOverflow){
								el.addClass(cl);
							}
						}
						return state;
					}, 1000);

				scope.$watch(calculate, function(newVal, oldVal) {
					if(newVal !== oldVal){
						if(newVal){
							el.addClass(cl);
						}else{
							el.removeClass(cl);
						}	
					}
				});

				$(window).bind("resize", function () {
				   winHeight = $(window).height();
				   el.removeClass(cl);
				   state = false;
				   calculate();
				});

				$('a[data-toggle="tab"]').on('shown', function (e) {
					calculate();
				});
			}
		};
	});