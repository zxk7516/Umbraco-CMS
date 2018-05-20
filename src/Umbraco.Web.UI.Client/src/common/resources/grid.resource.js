/**
  * @ngdoc service
  * @name umbraco.resources.gridResource
  * @description Handles retrieving grid data
  **/
function gridResource($http, umbRequestHelper) {
    return {

        getGridContentTypes: function () {
            return umbRequestHelper.resourcePromise(
                $http.get(
                    umbRequestHelper.getApiUrl(
                        "gridApiBaseUrl",
                        "GetContentTypes")),
                "Failed to get grid content types");
        },

        getScaffold: function (guid) {
            return umbRequestHelper.resourcePromise(
                $http.get(
                    umbRequestHelper.getApiUrl(
                        "gridApiBaseUrl",
                        "GetScaffold", {guid: guid})),
                "Failed to get scaffold");
        }
    };
}

angular.module('umbraco.resources').factory('gridResource', gridResource);
