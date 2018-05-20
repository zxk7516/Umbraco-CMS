/**
  * @ngdoc service
  * @name umbraco.resources.gridResource
  * @description Handles retrieving grid data
  **/
function gridResource($http, umbRequestHelper) {
    return {

        getCultures: function () {
            return umbRequestHelper.resourcePromise(
                $http.get(
                    umbRequestHelper.getApiUrl(
                        "gridApiBaseUrl",
                        "GetContentTypes")),
                "Failed to get grid content types");
        }
    };
}

angular.module('umbraco.resources').factory('gridResource', gridResource);
