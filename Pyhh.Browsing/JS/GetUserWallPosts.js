let results = [];
const $wallPostsSelector = document.querySelector('#page_wall_posts.page_wall_posts.mark_top');
if ($wallPostsSelector != null) {
    const $postItems = $wallPostsSelector.querySelectorAll('div._post_content');
    if ($postItems.length > 0) {
        $postItems.forEach((item) => {
            var $postDate;
            var $postText;
            var $repost;

            var $postDateObject = item.querySelector('span.rel_date');
            if ($postDateObject != null) {
                $postDate = $postDateObject.innerText;
            }

            var $postTextObject = item.querySelector('div.wall_text');
            if ($postTextObject != null) {
                $repost = Boolean($postTextObject.querySelector('div.copy_quote') != null);

                var $postTextElement = $postTextObject.querySelector('div.wall_post_text');
                if ($postTextElement != null) {
                    $postText = $postTextElement.innerText;
                }
            }

            results.push({
                text: $postText,
                date: $postDate,
                repost: $repost
            });
        });
    }
}

return results;