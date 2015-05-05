/**
 * @ngdoc controller
 * @name Umbraco.Editors.DocumentType.EditController
 * @function
 *
 * @description
 * The controller for the content type editor
 */
function DocumentTypeEditController($scope, $rootScope, $routeParams, $log, contentTypeResource, dataTypeResource) {
	$scope.page = {action: [], menu: [] };
	//$rootScope.emptySection = true; 

	contentTypeResource.getById($routeParams.id).then(function(dt){
		$scope.contentType = dt;
	});

	//hacking datatypes and their icons
	dataTypeResource.getAll().then(function(data){

		data = _.groupBy(data, function(dt){ 
			dt.icon = "icon-autofill";

			if(dt.name.indexOf("Dropdown") > -1 || dt.name.indexOf("Checkbox") > -1){
				dt.icon = "icon-bulleted-list";
				return "Lists";
			}

			if(dt.name.indexOf("Grid") > -1 || dt.name.indexOf("List View") > -1){
				dt.icon = "icon-item-arrangement";
				return "Collections";
			}

			if(dt.name.indexOf("picker") > -1){
				dt.icon ="icon-hand-pointer-alt"
				return "Pickers";
			}

			if(dt.name.indexOf("media") > -1 || dt.name.indexOf("Upload") > -1 || dt.name.indexOf("Crop") > -1){
				dt.icon ="icon-picture"
				return "Media";
			}

			return "Fields";				
		});

		$scope.dataTypes = data;
	});

	$scope.actions = [{name: "Structure", cssClass: "list"},{name: "Structure", cssClass: "list"},{name: "Structure", cssClass: "list"}];


	$scope.addTab = function(groups){
		groups.push({groups: [], properties:[]});
	};

	$scope.addProperty = function(properties){
		$scope.dialogModel = {};
		$scope.dialogModel.title = "Add property type";
		$scope.dialogModel.datatypes = $scope.dataTypes;
		$scope.dialogModel.addNew = true;
		$scope.dialogModel.view = "views/documentType/dialogs/property.html";

		$scope.dialogModel.close = function(model){
			properties.push(model.property);
			$scope.dialogModel = null;
		};	
	};

	$scope.toggleGroupSize = function(group){
		if(group.columns !== 12){
			group.columns = 12;
		}else{
			group.columns = 6;
		}
	};

	$scope.changePropertyEditor = function(property){
		$scope.dialogModel = {};
		$scope.dialogModel.title = "Change property type";
		$scope.dialogModel.property = property;
		$scope.dialogModel.dataTypes = $scope.dataTypes;
		$scope.dialogModel.view = "views/documentType/dialogs/property.html";
		$scope.showDialog = true;
		
		$scope.dialogModel.submit = function(dt){
			contentTypeResource.getPropertyTypeScaffold(dt.id)
				.then(function(pt){
					property.config = pt.config;
					property.editor = pt.editor;
					property.view = pt.view;
					$scope.dialogModel = null;
					$scope.showDialog = false;
				});	
		};

		$scope.dialogModel.close = function(model){
			$scope.showDialog = false;
			$scope.dialogModel = null;
		};
	};

	$scope.addItems = function(tab){
		$scope.showDialog = true;
		$scope.dialogModel = {};
		$scope.dialogModel.title = "Add some stuff";
		$scope.dialogModel.dataTypes = $scope.dataTypes;
		$scope.dialogModel.view = "views/documentType/dialogs/property.html";

		var target = tab;
		if(tab.groups && tab.groups.length > 0){
			target = _.last(tab.groups);
		}

		$scope.dialogModel.close = function(model){
			$scope.dialogModel = null;
			$scope.showDialog = false;
		};

		$scope.dialogModel.submit = function(dt){
			contentTypeResource.getPropertyTypeScaffold(dt.id).then(function(pt){
				pt.label = dt.name +" field";
				target.properties.push(pt);
			});
		};

		$scope.dialogModel.addTab = function(){
			var newTab = {name: "New tab", properties:[], groups:[]};
			var index = $scope.contentType.groups.indexOf(tab);
			$scope.contentType.groups.splice(index+1, 0, newTab);
			tab = newTab;
			target = newTab
		};

		$scope.dialogModel.addGroup = function(){
			var newGroup = {name: "New fieldset", properties:[]};
			tab.groups.push(newGroup);
			target = newGroup;
		};
	};


	$scope.addProperty = function(group){
		$log.log("open dialog");

		$scope.dialogModel = {};
		$scope.dialogModel.title = "Add property type";
		$scope.dialogModel.dataTypes = $scope.dataTypes;
		$scope.dialogModel.view = "views/documentType/dialogs/property.html";

		$scope.dialogModel.close = function(model){
			$scope.dialogModel = null;
		};
	};





	$scope.sortableOptionsFieldset = {
		distance: 10,
		revert: true,
		tolerance: "pointer",
		opacity: 0.7,
		scroll:true,
		cursor:"move",
		placeholder: "ui-sortable-placeholder",
		connectWith: ".edt-tabs",
		handle: ".handle",
		zIndex: 6000,
		start: function (e, ui) {
           	ui.placeholder.addClass( ui.item.attr("class") );
        },
        stop: function(e, ui){
         	ui.placeholder.remove();
        }
	};


	$scope.sortableOptionsEditor = {
		distance: 10,
		revert: true,
		tolerance: "pointer",
		connectWith: ".edt-props-sortable",
		opacity: 0.7,
		scroll:true,
		cursor:"move",
		handle: ".handle",
		placeholder: "ui-sortable-placeholder",
		zIndex: 6000
	};

	$scope.sortableOptionsTab = {
		distance: 10,
		revert: true,
		tolerance: "pointer",
		opacity: 0.7,
		scroll:true,
		cursor:"move",
		placeholder: "ui-sortable-placeholder",
		zIndex: 6000,
		handle: ".handle"
	};
            
}

angular.module("umbraco").controller("Umbraco.Editors.DocumentType.EditController", DocumentTypeEditController);
