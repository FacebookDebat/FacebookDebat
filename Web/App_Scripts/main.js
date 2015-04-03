appRoot.controller('MainController', function ($scope, $timeout, $resource) {
    var InitialFeedResource = $resource('/api/feed/posts');
        $scope.posts = [];
        InitialFeedResource.query(function (data) {
            angular.forEach(data, function (item) {
                $scope.posts.push(item);
            });
        });
        var feedResource = $resource('/api/feed/posts');
        $scope.newItems = [];
        (serviceTick = function () {
            $timeout(function () {

                feedResource.query(function (data) {
                    angular.forEach(data, function (item) {
                        var alreadyexisting = $.grep($scope.posts, function (e) { return e.comment_id == item.comment_id; });
                        if (alreadyexisting.length == 0) {
                            alreadyexisting = $.grep($scope.newItems, function (e) { return e.comment_id == item.comment_id; })
                            if (alreadyexisting.length == 0) {
                                $scope.newItems.unshift(item);
                            }
                        }
                    });
                    sortByKeyDesc($scope.newItems, "date");
                });

                serviceTick();
            }, 5000)
        })();

        (feedTick = function () {
            $timeout(function () {
                if ($scope.newItems.length > 0) {
                    $scope.posts.unshift($scope.newItems.pop());
                }
                feedTick();
            }, 5000)
        })();
    });