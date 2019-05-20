var userIds = Args.user_ids;
var fields = Args.fields;
var offset = 0;
var iters = 25;
var usersPerRequest = 1000;

var allUsers = []; 
var req_params = {
        "user_ids" : userIds,
        "name_case": "nom",
        "fields" : fields,
        "count"  : usersPerRequest,
        "v" : "5.92"
};

var i = 0;
var stop = "False";

while(i < iters && stop == "False")
{
    req_params.offset = offset + i*usersPerRequest;

    var items = API.users.get(req_params);
    
    if (items.length == 0) {
        stop = "True";
    }
    
    if (items.length < usersPerRequest)
    {
        allUsers = allUsers + items;
        stop = "True";
    } else {
        allUsers = allUsers + items;
    }
    
    i = i + 1;
}

return allUsers;