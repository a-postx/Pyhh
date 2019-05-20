var communityId = Args.community;
var offset = parseInt(Args.offset);
var iters = 25;
var membPerRequest = 1000;

var allIds = []; 
var req_params = {
        "group_id" : communityId,
        "sort": "id_asc",
        "offset" : offset,
        "count"  : membPerRequest,
        "v" : "5.92"
};

var i = 0;
var stop = "False";

while(i < iters && stop == "False")
{
    req_params.offset = offset + i*membPerRequest;

    var items = API.groups.getMembers(req_params).items; 
    
    if (items.length == 0) {
        stop = "True";
    }
    
    if (items.length < membPerRequest)
    {
        allIds = allIds + items;
        stop = "True";
    } else {
        allIds = allIds + items;
    }
    
    i = i + 1;
}

return allIds;