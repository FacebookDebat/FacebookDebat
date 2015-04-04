appRoot.controller('MainController', function ($scope, $timeout, $resource) {
    var feedResource = $resource('/api/feed/posts');
    $scope.posts = [];
    feedResource.query(function (data) {
        angular.forEach(data, function (item) {
            $scope.posts.push(item);
        });
    });

    (serviceTick = function () {
        $timeout(function () {
            feedResource.query(function (data) {
                debugger;
                $scope.posts[0].last_comment
                angular.forEach(data, function (item) {
                    $scope.posts.unshift($scope.newItems.pop());
                });
            });
            serviceTick();
        }, 5000)
    })();
});