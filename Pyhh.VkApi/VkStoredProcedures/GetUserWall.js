var user = Args.user;

var iters = 25;
var msgPerRequest = 100;

var allPosts = []; 
var req_params = {
        "owner_id" : user,
        "offset" : 0,
        "count"  : msgPerRequest,
        "v" : "5.92"
};

var i = 0;
var stop = "False";

while(i < iters && stop == "False")
{
    req_params.offset = i*msgPerRequest + iters*msgPerRequest*Args.offset;

    var items = API.wall.get(req_params).items; 
    
    if (items.length == 0) {
        return allPosts;
    }
    
    var fromIds = items@.from_id;
    
    var tmp = {};
    tmp.dates = items@.date;
    if (Args.deadline != -1 && tmp.dates[tmp.dates.length - 1] < Args.deadline)
    {
        stop = "True";
        allPosts = allPosts + items;
    } else {
        allPosts = allPosts + items;
    }

    var k = 0;
    
    i = i + 1;
}

return allPosts;